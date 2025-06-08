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
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Ifak.Fast.Mediator.Dashboard;

public class Module : ModelObjectModule<DashboardModel>
{
    private string absolutBaseDir = "";
    private readonly string BundlesPrefix = Guid.NewGuid().ToString().Replace("-", "");
    private int clientPort;
    private ViewType[] viewTypes = [];

    private readonly Dictionary<string, Session> sessions = [];

    private static SynchronizationContext? theSyncContext = null;
    private readonly WebApplication? webHost = null;
    private bool isRunning = false;
    private string configPath = "";

    private TimeRange initialTimeRange = new();
    private long initialStepSizeMS = 0; // 0 means auto

    public override IModelObject? UnnestConfig(IModelObject parent, object? obj) {
        if (obj is DataValue dv && parent is View view) {
            string type = view.Type;
            ViewType? viewType = viewTypes.FirstOrDefault(vt => vt.Name == type);
            if (viewType != null && viewType.ConfigType != null) {
                Type t = viewType.ConfigType;
                object? configObj = dv.Object(t);
                if (configObj is IModelObject mob) {
                    return mob;
                }
            }
        }
        return null;
    }

    public override async Task Init(ModuleInitInfo info,
                                    VariableValue[] restoreVariableValues,
                                    Notifier notifier,
                                    ModuleThread moduleThread) {

        theSyncContext = SynchronizationContext.Current;

        await base.Init(info, restoreVariableValues, notifier, moduleThread);

        configPath = Path.GetDirectoryName(base.modelFileName) ?? "";
        var config = info.GetConfigReader();

        clientPort = info.LoginPort;

        string baseDir = config.GetString("base-dir");
        string host = config.GetString("listen-host");
        int port = config.GetInt("listen-port");
        string certificatePath = config.GetOptionalString("certificate", "");

        string initialTimeRange = config.GetOptionalString("initial-time-range", "Last 6 hours");
        this.initialTimeRange = TimeRange.Parse(initialTimeRange);

        string initialStepSize = config.GetOptionalString("initial-step-size", "");

        if (string.IsNullOrWhiteSpace(initialStepSize) || initialStepSize.Equals("auto", StringComparison.InvariantCultureIgnoreCase)) {
            this.initialStepSizeMS = 0; // auto
        }
        else {
            this.initialStepSizeMS = Duration.Parse(initialStepSize).TotalMilliseconds;
        }

        string strViewAssemblies = config.GetString("view-assemblies");

        const string releaseDebugPlaceHolder = "{RELEASE_OR_DEBUG}";
        if (strViewAssemblies.Contains(releaseDebugPlaceHolder)) {
#if DEBUG
            strViewAssemblies = strViewAssemblies.Replace(releaseDebugPlaceHolder, "Debug");
#else
            strViewAssemblies = strViewAssemblies.Replace(releaseDebugPlaceHolder, "Release");
#endif
        }

        char[] separators = [';', '\n', '\r'];
        string[] viewAssemblies = strViewAssemblies
            .Split(separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToArray();

        absolutBaseDir = Path.GetFullPath(baseDir);
        if (!Directory.Exists(absolutBaseDir)) throw new Exception($"base-dir does not exist: {absolutBaseDir}");

        string[] absoluteViewAssemblies = viewAssemblies.Select(d => Path.GetFullPath(d)).ToArray();
        foreach (string dir in absoluteViewAssemblies) {
            if (!File.Exists(dir)) throw new Exception($"view-assembly does not exist: {dir}");
        }

        viewTypes = ReadAvailableViewTypes(absolutBaseDir, BundlesPrefix, absoluteViewAssemblies);
        DashboardUI uiModel = MakeUiModel(model, viewTypes);

        await base.OnConfigModelChanged(init: false); // required for UnnestConfig to work (viewTypes need to be loaded)

        string faviconFile = config.GetOptionalString("favicon", "");
        byte[] favicon = [];
        if (faviconFile != "") {
            string faviconPath = Path.GetFullPath(faviconFile);
            if (!File.Exists(faviconPath)) {
                Console.Error.WriteLine($"favicon not found: {faviconPath}");
            }
            else {
                favicon = File.ReadAllBytes(faviconPath);
            }
        }

        // Required to suppress warning when launching from Visual Studio:
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", string.Empty);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ContentRootPath = Directory.GetCurrentDirectory(),
            WebRootPath = absolutBaseDir
        });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.AddCors();
        builder.Services.AddHttpContextAccessor();
        builder.Services.Configure<HtmlModificationOptions>(opt => {
            opt.PageTitle = config.GetOptionalString("page-title", "Dashboard");
            opt.LoginTitle = config.GetOptionalString("login-title", "Dashboard Login");
            opt.Header = config.GetOptionalString("header", "Dashboard");
            opt.FavIcon = favicon;
        });

        System.Security.Cryptography.X509Certificates.X509Certificate2? certificate = null;

        if (!string.IsNullOrWhiteSpace(certificatePath)) {
            if (!File.Exists(certificatePath)) {
                throw new Exception($"Certificate file not found: {certificatePath}");
            }
            string? pwd = null;
            certificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(certificatePath, pwd);
        }

        void Configure(Microsoft.AspNetCore.Server.Kestrel.Core.ListenOptions listenOptions) {
            if (certificate != null) {
                listenOptions.UseHttps(certificate);
            }
        }

        bool hasIP = IPAddress.TryParse(host, out IPAddress? ipAddr);

        builder.WebHost.ConfigureKestrel(options => {
            if (hasIP) {
                options.Listen(ipAddr!, port, Configure);
            }
            else if (host == "any") {
                options.ListenAnyIP(port, Configure);
            }
            else {
                options.ListenLocalhost(port, Configure);
            }
        });

        WebApplication app = builder.Build();
        
        //app.UseHttpsRedirection();

        var webSocketOptions = new WebSocketOptions() {
            KeepAliveInterval = TimeSpan.FromSeconds(60)
        };
        app.UseWebSockets(webSocketOptions);

        string regex = $"^{BundlesPrefix}/(.*)";
        var rewriteOptions = new RewriteOptions().AddRewrite(regex, "/$1", skipRemainingRules: true);
        app.UseRewriter(rewriteOptions);

        app.Use(async (context, nextMiddleware) => {
            string path = context.Request.Path;
            if (path == "/") {
                context.Response.OnStarting(() => {
                    context.Response.Headers.Expires = (DateTime.UtcNow + TimeSpan.FromMinutes(1)).ToString("r");
                    return Task.CompletedTask;
                });
            }
            await nextMiddleware();
        });

        var options = new DefaultFilesOptions();
        options.DefaultFileNames.Clear();
        options.DefaultFileNames.Add("App/index.html");
        app.UseDefaultFiles(options);

        app.UseMiddleware<ModifyHtmlMiddleware>();
        app.UseStaticFiles();

        string webAssetsDir = Path.Combine(configPath, Session.WebAssets);

        try {
            IHttpContextAccessor httpContextAccessor = app.Services.GetRequiredService<IHttpContextAccessor>();
            
            app.UseStaticFiles(new StaticFileOptions {
                FileProvider = new MyPhysicalFileProvider(webAssetsDir, httpContextAccessor),
                RequestPath = $"/{Session.WebAssets}",
                OnPrepareResponse = ctx => {
                    if (ctx.File is CompressedFileInfo compressedFile) {
                        ctx.Context.Response.Headers.ContentEncoding = compressedFile.CompressionType;
                        ctx.Context.Response.Headers.Vary = "Accept-Encoding";
                        ctx.Context.Response.Headers.CacheControl = "public, max-age=172800"; // 48 hour caching
                        // Add ETag for cache validation
                        var etag = $"\"{compressedFile.LastModified.Ticks:x}-{compressedFile.Length:x}\"";
                        ctx.Context.Response.Headers.ETag = etag;
                    }
                }
            });
        }
        catch (Exception exp) {
            Console.Error.WriteLine($"Failed to serve directory for web assets: {exp.Message}");
        }

        app.UseCors(builder => builder
                .SetIsOriginAllowed(origin => origin.StartsWith("http://localhost:"))
                .AllowAnyMethod()
                .AllowAnyHeader());

        app.Run((context) => {
            //logger.Info($"HTTP {context.Request.Path} {Thread.CurrentThread.ManagedThreadId}");
            var promise = new TaskCompletionSource();
            theSyncContext!.Post(_ => {
                Task task = HandleClientRequest(context);
                task.ContinueWith(completedTask => promise.CompleteFromTask(completedTask));
            }, null);
            return promise.Task;
        });

        Task _ = app.StartAsync();
    }

    public async override Task InitAbort() {
        try {
            var host = webHost;
            if (host != null) {
                await host.StopAsync();
            }
        }
        catch (Exception) { }
    }
    
    public override async Task Run(Func<bool> fShutdown) {

        await Task.Delay(1000);

        isRunning = true;

        _ = StartCheckForModelFileModificationTask(fShutdown);

        while (!fShutdown()) {

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

            await Task.Delay(1000);
        }

        isRunning = false;

        Task closeTask = Task.WhenAll(sessions.Values.Select(session => session.Close()).ToArray());
        await Task.WhenAny(closeTask, Task.Delay(2000));

        if (webHost != null) {
            _ = webHost.StopAsync();
        }
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

                if (!sessions.TryGetValue(sessionID, out Session? value)) {
                    Task ignored = socket.CloseAsync(WebSocketCloseStatus.ProtocolError, string.Empty, CancellationToken.None);
                    throw new InvalidSessionException();
                }

                Session session = value;

                await session.ReadWebSocket(socket);
            }
        }
        catch (Exception exp) {
            if (exp is not InvalidSessionException) {
                Exception e = exp.GetBaseException() ?? exp;
                LogWarn("Error handling web socket request: " + e.Message);
            }
        }
    }

    private async Task HandleClientRequest(HttpContext context) {

        HttpRequest request = context.Request;
        HttpResponse response = context.Response;

        async Task Respond(ReqResult result) {
            response.StatusCode = result.StatusCode;
            response.ContentLength = result.Bytes.Length;
            response.ContentType = result.ContentType;
            try {
                await result.Bytes.CopyToAsync(response.Body);
            }
            catch (Exception) { }
        }

        if (!isRunning) {
            await Respond(ReqResult.Bad("Dashboard is not running yet."));
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

                    using (ReqResult result = await HandlePost(request)) {
                        await Respond(result);
                    }
                    return;

                default:

                    response.StatusCode = 400;
                    return;
            }
        }
        catch (Exception exp) {
            response.StatusCode = 500;
            LogWarn("Error handling client request", exp);
        }
    }

    private const string Path_Login = "/login";
    private const string Path_Logout = "/logout";
    private const string Path_ViewReq = "/viewRequest/";
    private const string Path_ActivateView = "/activateView";
    private const string Path_DuplicateView = "/duplicateView";
    private const string Path_DuplicateConvertView = "/duplicateConvertView";
    private const string Path_ToggleHeader = "/toggleHeader";
    private const string Path_RenameView = "/renameView";
    private const string Path_MoveView = "/moveView";
    private const string Path_DeleteView = "/deleteView";

    private async Task<ReqResult> HandlePost(HttpRequest request) {

        string path = request.Path;

        try {

            if (path == Path_Login) {

                string? user;
                string? pass;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                    var obj = await StdJson.JObjectFromReaderAsync(reader);
                    user = (string?)obj["user"];
                    pass = (string?)obj["pass"];
                    if (user == null || pass == null) {
                        return ReqResult.Bad("Missing user and password.");
                    }
                }

                var session = new Session(configPath);
                Connection connection;
                try {
                    const int timeoutSeconds = 15 * 60;
                    connection = await HttpConnection.ConnectWithUserLogin("localhost", clientPort, user, pass, null, session, timeoutSeconds);
                }
                catch (Exception exp) {
                    LogWarn(exp.Message);
                    return ReqResult.Bad(exp.Message);
                }
                await session.SetConnection(connection, model, moduleID, viewTypes);
                sessions[session.ID] = session;

                MemberRef mr = MemberRef.Make(moduleID, model.ID, nameof(DashboardModel.Views));
                bool canUpdateViews = await connection.CanUpdateConfig(mr);

                DashboardUI uiModel = MakeUiModel(model, viewTypes, session.UserRole);

                var result = new JObject {
                    ["sessionID"] = session.ID
                };
                string str = StdJson.ObjectToString(uiModel);
                result["model"] = new JRaw(str);
                result["canUpdateViews"] = canUpdateViews;
                result["initialTimeRange"] = new JRaw(StdJson.ObjectToString(initialTimeRange));
                result["initialStepSizeMS"] = initialStepSizeMS;
                return ReqResult.OK(result);
            }
            else if (path.StartsWith(Path_ViewReq)) {

                string viewRequest = path.Substring(Path_ViewReq.Length);

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());

                string content;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                    content = await reader.ReadToEndAsync();
                }
                return await session.OnViewCommand(viewID, viewRequest, DataValue.FromJSON(content));
            }
            else if (path == Path_ActivateView) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());
                bool canUpdateViewConfig = await session.OnActivateView(viewID);
                return ReqResult.OK(new {
                    canUpdateViewConfig
                });
            }
            else if (path == Path_DuplicateView) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());
                string newViewID = await session.OnDuplicateView(viewID);

                return ReqResult.OK(new {
                    newViewID,
                    model = MakeUiModel(model, viewTypes, session.UserRole)
                });
            }
            else if (path == Path_DuplicateConvertView) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());
                string newViewID = await session.OnDuplicateConvertHistoryPlot(viewID);

                return ReqResult.OK(new {
                    newViewID,
                    model = MakeUiModel(model, viewTypes, session.UserRole)
                });
            }
            else if (path == Path_ToggleHeader) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());
                await session.OnToggleHeader(viewID);

                return ReqResult.OK();
            }
            else if (path == Path_RenameView) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());

                string? newViewName;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                    var obj = await StdJson.JObjectFromReaderAsync(reader);
                    newViewName = (string?)obj["newViewName"];
                    if (newViewName == null) {
                        return ReqResult.Bad("Missing newViewName");
                    }
                }

                await session.OnRenameView(viewID, newViewName);

                return ReqResult.OK(new {
                    model = MakeUiModel(model, viewTypes, session.UserRole)
                });
            }
            else if (path == Path_MoveView) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());

                bool up = false;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                    var obj = await StdJson.JObjectFromReaderAsync(reader);
                    up = (bool)obj["up"]!;
                }

                await session.OnMoveView(viewID, up);

                return ReqResult.OK(new {
                    model = MakeUiModel(model, viewTypes, session.UserRole)
                });
            }
            else if (path == Path_DeleteView) {

                (Session session, string viewID) = GetSessionFromQuery(request.QueryString.ToString());
                await session.OnDeleteView(viewID);

                return ReqResult.OK(new {
                    model = MakeUiModel(model, viewTypes, session.UserRole)
                });
            }
            else if (path == Path_Logout) {

                string sessionID;
                using (var reader = new StreamReader(request.Body, Encoding.UTF8)) {
                    sessionID = await reader.ReadToEndAsync();
                }

                if (sessions.TryGetValue(sessionID, out Session? value)) {
                    Session session = value;
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
            LogWarn("HandlePost: " + exp.Message);
            return ReqResult.Bad(exp.Message);
        }
        catch (Exception exp) {
            LogWarn("HandlePost:", exp);
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

        if (!sessions.TryGetValue(sessionID, out Session? value)) {
            throw new InvalidSessionException();
        }
        Session session = value;
        return (session, viewID);
    }

    private static ViewType[] ReadAvailableViewTypes(string absoluteBaseDir, string bundlesPrefx, string[] viewAssemblies) {

        var viewTypes = Reflect.GetAllNonAbstractSubclasses(typeof(ViewBase)).ToList();
        viewTypes.AddRange(viewAssemblies.SelectMany(LoadTypesFromAssemblyFile));

        var result = new List<ViewType>();

        foreach (Type type in viewTypes) {
            Identify? id = type.GetCustomAttribute<Identify>();
            if (id != null) {
                string viewBundle = "ViewBundle_" + id.Bundle;
                string viewBundlePath = Path.Combine(absoluteBaseDir, viewBundle);
                bool url = type == typeof(View_ExtURL);
                if (url || Directory.Exists(viewBundlePath)) {
                    var vt = new ViewType() {
                        Name = id.ID,
                        HtmlPath = $"/{bundlesPrefx}/" + viewBundle + "/" + id.Path, // "/" + dir.Name + "/" + indexFile,
                        Type = type,
                        ConfigType = id.ConfigType,
                        Icon = id.Icon ?? ""
                    };
                    result.Add(vt);
                }
                else {
                    LogWarn($"No ViewBundle folder found for View {id.ID} in {absoluteBaseDir}");
                }
            }
        }
        return [.. result];
    }

    private static Type[] LoadTypesFromAssemblyFile(string fileName) {
        try {
            Type baseClass = typeof(ViewBase);

            var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                    fileName,
                    sharedTypes: [baseClass]);

            return loader.LoadDefaultAssembly()
                .GetExportedTypes()
                .Where(t => t.IsSubclassOf(baseClass) && !t.IsAbstract)
                .ToArray();
        }
        catch (Exception exp) {
            Console.Error.WriteLine($"Failed to load view types from assembly '{fileName}': {exp.Message}");
            Console.Error.Flush();
            return [];
        }
    }

    private static DashboardUI MakeUiModel(DashboardModel model, ViewType[] viewTypes, string? userRole = null) {

        DashboardUI result = new();

        foreach (View v in model.Views) {

            if (v.RestrictVisibility && userRole != null) {
                string[] roles = v.VisibleForRoles.Split(',');
                if (roles.All(r => r != userRole)) {
                    continue;
                }
            }

            ViewType? viewType = viewTypes.FirstOrDefault(vt => vt.Name.Equals(v.Type, StringComparison.InvariantCultureIgnoreCase)) ?? throw new Exception($"No view type '{v.Type}' found!");
            bool url = viewType.Type == typeof(View_ExtURL);

            var viewInstance = new ViewInstance() {
                viewID = v.ID,
                viewName = v.Name,
                viewURL = url ? v.Config.Object<ViewURLConfig>()!.URL : viewType.HtmlPath,
                viewIcon = v.Icon ?? viewType.Icon,
                viewGroup = v.Group,
                viewType = v.Type,
            };

            result.views.Add(viewInstance);
        }
        return result;
    }

    private static void LogWarn(string msg, Exception? exp = null) {
        Exception? exception = exp != null ? (exp.GetBaseException() ?? exp) : null;
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
    public Type? Type { get; set; }
    public Type? ConfigType { get; set; }
}


public class DashboardUI
{
    public List<ViewInstance> views = [];
}

#pragma warning disable IDE1006 // Naming Styles
public class ViewInstance
{
    public string viewID { get; set; } = "";
    public string viewIcon { get; set; } = "";
    public string viewName { get; set; } = "";
    public string viewURL { get; set; } = "";
    public string viewGroup { get; set; } = "";
    public string viewType { get; set; } = "";
}
#pragma warning restore IDE1006 // Naming Styles

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
            promise.SetException(completedTask.Exception!);
        }
        else {
            promise.SetCanceled();
        }
    }

    internal static void CompleteFromTask(this TaskCompletionSource promise, Task completedTask) {

        if (completedTask.IsCompletedSuccessfully) {
            promise.SetResult();
        }
        else if (completedTask.IsFaulted) {
            promise.SetException(completedTask.Exception!);
        }
        else {
            promise.SetCanceled();
        }
    }
}

public class HtmlModificationOptions {
    public string PageTitle { get; set; } = "";
    public string LoginTitle { get; set; } = "";
    public string Header { get; set; } = "";
    public byte[] FavIcon { get; set; } = [];
}

public class ModifyHtmlMiddleware(RequestDelegate next, IOptions<HtmlModificationOptions> options) {

    private readonly RequestDelegate _next = next;
    private readonly HtmlModificationOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context) {

        if (context.Request.Method != "GET") {
            await _next(context);
            return;
        }

        var originalBodyStream = context.Response.Body;

        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        await _next(context);

        context.Response.Body = originalBodyStream;
        responseBody.Seek(0, SeekOrigin.Begin);

        string? path = context.Request.Path.Value;

        if (path == "/App/index.html" && context.Response.StatusCode == 200) {
            string content = await new StreamReader(responseBody).ReadToEndAsync();
            content = ModifyHtmlContent(content);
            context.Response.ContentLength = Encoding.UTF8.GetByteCount(content);
            await context.Response.WriteAsync(content);
        }
        else if (_options.FavIcon.Length > 0 && path == "/App/favicon.ico" && context.Response.StatusCode == 200) {
            context.Response.ContentLength = _options.FavIcon.Length;
            await originalBodyStream.WriteAsync(_options.FavIcon);
        }
        else {
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private string ModifyHtmlContent(string originalHtml) {
        return originalHtml
                .Replace("!PLACEHOLDER_TITLE!",  _options.PageTitle)
                .Replace("!PLACEHOLDER_HEADER!", _options.Header)
                .Replace("!PLACEHOLDER_LOGIN!",  _options.LoginTitle);
    }
}



public sealed class CompressedFileInfo : IFileInfo {
    private readonly IFileInfo _originalFileInfo;
    private readonly IFileInfo _compressedFileInfo;
    private readonly string _compressionType;

    public CompressedFileInfo(IFileInfo originalFileInfo, IFileInfo compressedFileInfo, string compressionType) {
        _originalFileInfo = originalFileInfo;
        _compressedFileInfo = compressedFileInfo;
        _compressionType = compressionType;
    }

    public bool Exists => _compressedFileInfo.Exists;
    public long Length => _compressedFileInfo.Length;
    public string? PhysicalPath => _compressedFileInfo.PhysicalPath;
    public string Name => _originalFileInfo.Name;
    public DateTimeOffset LastModified => _compressedFileInfo.LastModified;
    public bool IsDirectory => false;
    public string CompressionType => _compressionType;

    public Stream CreateReadStream() => _compressedFileInfo.CreateReadStream();
}

public sealed class MyPhysicalFileProvider : IFileProvider {

    private readonly string _root;
    private PhysicalFileProvider? _physicalFileProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MyPhysicalFileProvider(string root, IHttpContextAccessor httpContextAccessor) {
        _root = root;
        _httpContextAccessor = httpContextAccessor;
        EnsurePhysicalFileProvider();
    }

    private void EnsurePhysicalFileProvider() {
        if (Directory.Exists(_root)) {
            _physicalFileProvider = new PhysicalFileProvider(_root);
        }
    }

    public IDirectoryContents GetDirectoryContents(string subpath) {
        EnsurePhysicalFileProvider();
        PhysicalFileProvider? pfp = _physicalFileProvider;
        return pfp != null ? pfp.GetDirectoryContents(subpath) : NotFoundDirectoryContents.Singleton;
    }

    public IFileInfo GetFileInfo(string subpath) {
        EnsurePhysicalFileProvider();
        PhysicalFileProvider? pfp = _physicalFileProvider;
        if (pfp == null) {
            return new NotFoundFileInfo(subpath);
        }

        IFileInfo originalFile = pfp.GetFileInfo(subpath);
        if (!originalFile.Exists) {
            return originalFile;
        }

        // Check if client supports compression
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext != null) {
            var acceptEncoding = httpContext.Request.Headers.AcceptEncoding.ToString();
            
            // Try Brotli first (better compression)
            if (acceptEncoding.Contains("br", StringComparison.OrdinalIgnoreCase)) {
                var brotliFile = pfp.GetFileInfo(subpath + ".br");
                if (brotliFile.Exists) {
                    return new CompressedFileInfo(originalFile, brotliFile, "br");
                }
            }

            // Try Gzip
            if (acceptEncoding.Contains("gzip", StringComparison.OrdinalIgnoreCase)) {
                var gzipFile = pfp.GetFileInfo(subpath + ".gz");
                if (gzipFile.Exists) {
                    return new CompressedFileInfo(originalFile, gzipFile, "gzip");
                }
            }
        }

        return originalFile;
    }

    public IChangeToken Watch(string filter) {
        EnsurePhysicalFileProvider();
        PhysicalFileProvider? pfp = _physicalFileProvider;
        return pfp != null ? pfp.Watch(filter) : NullChangeToken.Singleton;
    }
}