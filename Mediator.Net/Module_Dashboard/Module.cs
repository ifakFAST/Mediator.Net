// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Ifak.Fast.Mediator.Dashboard
{
    public class Module : ModelObjectModule<DashboardModel>
    {
        private bool shutdown = false;
        private string absolutBaseDir = "";
        private int clientPort;
        private ViewType[] viewTypes = new ViewType[0];
        private DashboardUI uiModel = new DashboardUI();

        private readonly Dictionary<string, Session> sessions = new Dictionary<string, Session>();

        private static Module theModule = null;
        private static SynchronizationContext theSyncContext = null;
        private IWebHost webHost = null;

        public Module() {
            if (theModule == null) {
                theModule = this;
            }
        }

        public override async Task Init(ModuleInitInfo info,
                                        VariableValue[] restoreVariableValues,
                                        Notifier notifier,
                                        ModuleThread moduleThread) {

            theSyncContext = SynchronizationContext.Current;

            await base.Init(info, restoreVariableValues, notifier, moduleThread);

            var config = info.GetConfigReader();

            clientPort = info.LoginPort;

            string baseDir = config.GetString("base-dir");
            string host = config.GetString("listen-host");
            int port = config.GetInt("listen-port");

            string strViewAssemblies = config.GetString("view-assemblies");

            const string releaseDebugPlaceHolder = "{RELEASE_OR_DEBUG}";
            if (strViewAssemblies.Contains(releaseDebugPlaceHolder)) {
#if DEBUG
                strViewAssemblies = strViewAssemblies.Replace(releaseDebugPlaceHolder, "Debug");
#else
                strViewAssemblies = strViewAssemblies.Replace(releaseDebugPlaceHolder, "Release");
#endif
            }

            string[] viewAssemblies = strViewAssemblies.Split(";", StringSplitOptions.RemoveEmptyEntries);

            absolutBaseDir = Path.GetFullPath(baseDir);
            if (!Directory.Exists(absolutBaseDir)) throw new Exception($"base-dir does not exist: {absolutBaseDir}");

            string[] absoluteViewAssemblies = viewAssemblies.Select(d => Path.GetFullPath(d)).ToArray();
            foreach (string dir in absoluteViewAssemblies) {
                if (!File.Exists(dir)) throw new Exception($"view-assembly does not exist: {dir}");
            }

            viewTypes = ReadAvailableViewTypes(absolutBaseDir, absoluteViewAssemblies);
            uiModel = MakeUiModel(model, viewTypes);

            var builder = new WebHostBuilder();
            builder.UseKestrel((KestrelServerOptions options) => {
                if (host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase)) {
                    options.ListenLocalhost(port);
                }
                else {
                    options.Listen(IPAddress.Parse(host), port);
                }
            });
            builder.UseWebRoot(absolutBaseDir);
            builder.UseStartup<Module>();
            webHost = builder.Build();
            webHost.Start();
        }

        public async override Task InitAbort() {
            try {
                await webHost.StopAsync();
            }
            catch (Exception) { }
        }

        public void ConfigureServices(IServiceCollection services) {

            services.AddCors(options => { // necessary to allow web frontend debugging
                options.AddPolicy("AllowLocalhost8080", builder => {
                    builder.WithOrigins("http://localhost:8080").AllowAnyMethod().AllowAnyHeader();
                });
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory) {

            //loggerFactory.AddConsole(Microsoft.Extensions.Logging.LogLevel.Trace);

            app.UseCors("AllowLocalhost8080");

            var webSocketOptions = new WebSocketOptions() {
                KeepAliveInterval = TimeSpan.FromSeconds(60),
                ReceiveBufferSize = 4 * 1024
            };
            app.UseWebSockets(webSocketOptions);

            var options = new DefaultFilesOptions();
            options.DefaultFileNames.Clear();
            options.DefaultFileNames.Add("App/index.html");
            app.UseDefaultFiles(options);

            app.UseStaticFiles();

            app.Run((context) => {
                var promise = new TaskCompletionSource<bool>();
                theSyncContext.Post(_ => {
                    Task task = theModule.HandleClientRequest(context);
                    task.ContinueWith(completedTask => promise.CompleteFromTask(completedTask));
                }, null);
                return promise.Task;
            });
        }

        public override async Task Run(Func<bool> fShutdown) {

            while (!fShutdown()) {

                await Task.Delay(1000);

                bool needPurge = sessions.Values.Any(session => session.IsAbandoned);
                if (needPurge) {
                    var sessionItems = sessions.Values.ToList();
                    foreach (var session in sessionItems) {
                        if (session.IsAbandoned) {
                            Console.WriteLine("Closing abandoned session: " + session.ID);
                            var ignored2 = session.Close();
                            sessions.Remove(session.ID);
                        }
                    }
                }
            }

            shutdown = true;

            Task closeTask = Task.WhenAll(sessions.Values.Select(session => session.Close()).ToArray());
            await Task.WhenAny(closeTask, Task.Delay(2000));

            Task ignored = webHost.StopAsync();
        }

        private async Task HandleClientWebSocket(WebSocket socket) {

            try {

                const int maxMessageSize = 1024;
                byte[] receiveBuffer = new byte[maxMessageSize];

                WebSocketReceiveResult receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);

                if (receiveResult.MessageType == WebSocketMessageType.Close) {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
                }
                else if (receiveResult.MessageType == WebSocketMessageType.Binary) {
                    await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Cannot accept binary frame", CancellationToken.None);
                }
                else {

                    int count = receiveResult.Count;

                    while (!receiveResult.EndOfMessage) {

                        if (count >= maxMessageSize) {
                            string closeMessage = string.Format("Maximum message size: {0} bytes.", maxMessageSize);
                            await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, closeMessage, CancellationToken.None);
                            return;
                        }
                        receiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, count, maxMessageSize - count), CancellationToken.None);
                        count += receiveResult.Count;
                    }

                    var sessionID = Encoding.UTF8.GetString(receiveBuffer, 0, count);

                    if (!sessions.ContainsKey(sessionID)) {
                        Task ignored = socket.CloseAsync(WebSocketCloseStatus.ProtocolError, string.Empty, CancellationToken.None);
                        throw new InvalidSessionException();
                    }

                    Session session = sessions[sessionID];

                    await session.ReadWebSocket(socket);
                }
            }
            catch (Exception exp) {
                if (!(exp is InvalidSessionException)) {
                    Exception e = exp.GetBaseException() ?? exp;
                    logWarn("Error handling web socket request: " + e.Message);
                }
            }
        }

        private async Task HandleClientRequest(HttpContext context) {

            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            if (shutdown) {
                response.StatusCode = 400; // BAD Request
                return;
            }

            try {

                if (request.Path == "/websocket/" && context.WebSockets.IsWebSocketRequest) {
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await HandleClientWebSocket(webSocket);
                    return;
                }

                switch (request.Method) {

                    case "POST":

                        using (ReqResult result = await HandlePost(request, response)) {
                            response.StatusCode = result.StatusCode;
                            response.ContentLength = result.Bytes.Length;
                            response.ContentType = "application/json";
                            try {
                                await result.Bytes.CopyToAsync(response.Body);
                            }
                            catch (Exception) { }
                        }
                        return;

                    default:

                        response.StatusCode = 400;
                        return;
                }
            }
            catch (Exception exp) {
                response.StatusCode = 500;
                logWarn("Error handling client request", exp);
            }
        }

        private const string Path_Login = "/login";
        private const string Path_Logout = "/logout";
        private const string Path_ViewReq = "/viewRequest/";
        private const string Path_ActivateView = "/activateView";

        private async Task<ReqResult> HandlePost(HttpRequest request, HttpResponse response) {

            string path = request.Path;

            try {

                if (path == Path_Login) {

                    string user;
                    string pass;
                    using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                        var obj = await StdJson.JObjectFromReaderAsync(reader);
                        user = (string)obj["user"];
                        pass = (string)obj["pass"];
                        if (user == null || pass == null) {
                            return ReqResult.Bad("Missing user and password.");
                        }
                    }

                    var session = new Session();
                    Connection connection = null;
                    try {
                        connection = await HttpConnection.ConnectWithUserLogin("localhost", clientPort, user, pass, null, session);
                    }
                    catch (Exception exp) {
                        logWarn(exp.Message);
                        return ReqResult.Bad(exp.Message);
                    }
                    await session.SetConnection(connection, model, moduleID, viewTypes);
                    sessions[session.ID] = session;

                    var result = new JObject();
                    result["sessionID"] = session.ID;
                    string str = StdJson.ObjectToString(uiModel);
                    JRaw raw = new JRaw(str);
                    result["model"] = raw;
                    return ReqResult.OK(result);
                }
                else if (path.StartsWith(Path_ViewReq)) {

                    string viewRequest = path.Substring(Path_ViewReq.Length);

                    (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());

                    string content = null;
                    using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                        content = await reader.ReadToEndAsync();
                    }
                    return await session.OnViewCommand(viewID, viewRequest, DataValue.FromJSON(content));
                }
                else if (path == Path_ActivateView) {

                    (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());
                    await session.OnActivateView(viewID);
                    return ReqResult.OK();
                }
                else if (path == Path_Logout) {

                    string sessionID = null;
                    using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                        sessionID = await reader.ReadToEndAsync();
                    }

                    if (sessions.ContainsKey(sessionID)) {
                        Session session = sessions[sessionID];
                        var ignored = session.Close();
                        sessions.Remove(sessionID);
                    }

                    return ReqResult.OK();
                }
                else {
                    return ReqResult.Bad("Invalid path: " + path);
                }
            }
            catch (InvalidSessionException exp) {
                logWarn("HandlePost: " + exp.Message);
                return ReqResult.Bad(exp.Message);
            }
            catch (Exception exp) {
                logWarn("HandlePost:", exp);
                return ReqResult.Bad(exp.Message);
            }
        }

        private (Session sesssion, string viewID) GetSessionFromQuery(string query) {
            int i = query.IndexOf('_');
            if (i <= 0) {
                throw new Exception("Invalid context");
            }
            string sessionID = query.Substring(1, i - 1);
            string viewID = query.Substring(i + 1);

            if (!sessions.ContainsKey(sessionID)) {
                throw new InvalidSessionException();
            }
            Session session = sessions[sessionID];
            return (session, viewID);
        }

        private static ViewType[] ReadAvailableViewTypes(string absoluteBaseDir, string[] viewAssemblies) {

            var viewTypes = Reflect.GetAllNonAbstractSubclasses(typeof(ViewBase)).ToList();
            viewTypes.AddRange(viewAssemblies.SelectMany(LoadTypesFromAssemblyFile));

            var result = new List<ViewType>();

            foreach (Type type in viewTypes) {
                Identify id = type.GetCustomAttribute<Identify>();
                if (id != null) {
                    string viewBundle = "ViewBundle_" + id.Bundle;
                    string viewBundlePath = Path.Combine(absoluteBaseDir, viewBundle);
                    bool url = type == typeof(View_ExtURL);
                    if (url || Directory.Exists(viewBundlePath)) {
                        var vt = new ViewType() {
                            Name = id.ID,
                            HtmlPath = "/" + viewBundle + "/" + id.Path, // "/" + dir.Name + "/" + indexFile,
                            Type = type,
                            Icon = id.Icon ??  ""
                        };
                        result.Add(vt);
                    }
                    else {
                        logWarn($"No ViewBundle folder found for View {id.ID} in {absoluteBaseDir}");
                    }
                }
            }
            return result.ToArray();
        }

        private static Type[] LoadTypesFromAssemblyFile(string fileName) {
            try {
                Type baseClass = typeof(ViewBase);

                var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                        fileName,
                        sharedTypes: new Type[] { baseClass });

                return loader.LoadDefaultAssembly()
                    .GetExportedTypes()
                    .Where(t => t.IsSubclassOf(baseClass) && !t.IsAbstract)
                    .ToArray();
            }
            catch (Exception exp) {
                Console.Error.WriteLine($"Failed to load view types from assembly '{fileName}': {exp.Message}");
                Console.Error.Flush();
                return new Type[0];
            }
        }

        private static DashboardUI MakeUiModel(DashboardModel model, ViewType[] viewTypes) {

            DashboardUI result = new DashboardUI();

            foreach (View v in model.Views) {

                ViewType viewType = viewTypes.FirstOrDefault(vt => vt.Name.Equals(v.Type, StringComparison.InvariantCultureIgnoreCase));
                if (viewType == null) throw new Exception($"No view type '{v.Type}' found!");

                bool url = viewType.Type == typeof(View_ExtURL);

                var viewInstance = new ViewInstance() {
                    viewID = v.ID,
                    viewName = v.Name,
                    viewURL = url ? v.Config.Object<ViewURLConfig>().URL : viewType.HtmlPath,
                    viewIcon = viewType.Icon,
                    viewGroup = v.Group
                };

                result.views.Add(viewInstance);
            }
            return result;
        }

        private static void logWarn(string msg, Exception exp = null) {
            Exception exception = exp != null ? (exp.GetBaseException() ?? exp) : null;
            if (exception != null)
                Console.Out.WriteLine(msg + " " + exception.Message + "\n" + exception.StackTrace);
            else
                Console.Out.WriteLine(msg);
        }
    }

    public class ViewType
    {
        public string Name { get; set; } = "";
        public string HtmlPath { get; set; } = "";
        public string Icon { get; set; } = "";
        public Type Type { get; set; }
    }


    public class DashboardUI
    {
        public List<ViewInstance> views = new List<ViewInstance>();
    }

    public class ViewInstance
    {
        public string viewID { get; set; } = "";
        public string viewIcon { get; set; } = "";
        public string viewName { get; set; } = "";
        public string viewURL { get; set; } = "";
        public string viewGroup { get; set; } = "";
    }

    public class InvalidSessionException : Exception
    {
        public InvalidSessionException() : base("Invalid Session ID") { }
    }

    internal static class TaskUtil
    {
        internal static void CompleteFromTask(this TaskCompletionSource<bool> promise, Task completedTask) {

            if (completedTask.IsCompletedSuccessfully) {
                promise.SetResult(true);
            }
            else if (completedTask.IsFaulted) {
                promise.SetException(completedTask.Exception);
            }
            else {
                promise.SetCanceled();
            }
        }
    }
}