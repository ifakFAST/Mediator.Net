// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Ifak.Fast.Json.Linq;
using Ifak.Fast.Mediator.Util;
using NLog;

namespace Ifak.Fast.Mediator
{
    public class MediatorCore {

        private static Logger logger = LogManager.GetLogger("Mediator.Core");

        internal readonly HandleClientRequests reqHandler;
        public ModuleState[] modules = new ModuleState[0];
        internal UserManagement userManagement = new UserManagement();
        internal List<Location> locations = new List<Location>();

        internal readonly HistoryManager history = new HistoryManager();
        private int listenPort = 8080;

        private bool shutdown = false;

        private static MediatorCore theCore = null;
        private static SynchronizationContext theSyncContext = null;

        public MediatorCore() {
            reqHandler = new HandleClientRequests(this);
            if (theCore == null) {
                theCore = this;
            }
        }

        public void RequestShutdown() {
            shutdown = true;
            reqHandler.terminating = true;
        }

        public void ConfigureServices(IServiceCollection services) {

        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory) {

            //loggerFactory.AddConsole(Microsoft.Extensions.Logging.LogLevel.Trace);

            var webSocketOptions = new WebSocketOptions() {
                KeepAliveInterval = TimeSpan.FromSeconds(60),
                ReceiveBufferSize = 8 * 1024
            };
            app.UseWebSockets(webSocketOptions);
            app.Run((context) => {
                var promise = new TaskCompletionSource<bool>();
                theSyncContext.Post(_ => {
                    Task task = theCore.HandleClientRequest(context);
                    task.ContinueWith(completedTask => promise.CompleteFromTask(completedTask));
                }, null);
                return promise.Task;
            });
        }

        internal async Task Run(string configFileName, bool clearDBs) {

            theSyncContext = SynchronizationContext.Current;

            Configuration config = Util.Xml.FromXmlFile<Configuration>(configFileName);
            userManagement = config.UserManagement;
            locations = config.Locations;

            listenPort = config.ClientListenPort;
            string host = config.ClientListenHost;

            var builder = new WebHostBuilder();
            builder.UseKestrel((KestrelServerOptions options) => {
                if (host.Equals("localhost", StringComparison.InvariantCultureIgnoreCase)) {
                    options.ListenLocalhost(listenPort);
                }
                else {
                    options.Listen(IPAddress.Parse(host), listenPort);
                }
            });
            builder.UseStartup<MediatorCore>();
            IWebHost webHost = builder.Build();
            webHost.Start();

            Module[] enabledModules = config.Modules.Where(a => a.Enabled).ToArray();
            this.modules = enabledModules.Select(m => new ModuleState(m, this)).ToArray();

            foreach (ModuleState module in modules) {
                module.CreateInstance();
            }

            await history.Start(enabledModules, GetVariableDescription, reqHandler.OnVariableHistoryChanged, clearDBs);

            try {

                // Some modules (e.g. Alarms&Events) need to be initialized before the others
                var sequentialModules = modules.Where(m => !m.Config.ConcurrentInit).ToArray();
                foreach (var m in sequentialModules) {
                    await InitModule(m);
                }

                var concurrentModules = modules.Where(m => m.Config.ConcurrentInit).ToArray();
                Task[] initTasks = concurrentModules.Select(InitModule).ToArray();
                await Task.WhenAll(initTasks);
            }
            catch (Exception exp) {

                foreach (var module in modules) {
                    if (module.State == State.InitError) {
                        string msg = "Init of module " + module.Config.Name + " failed: " + module.LastError;
                        Log_Event(Severity.Alarm, "InitFailed", msg, module.ID);
                    }
                }
                await Shutdown();
                throw new Exception("Init of modules failed", exp);
            }

            Log_Event(Severity.Info, "SysStartup", "System started successfully");

            logger.Info("All modules initialized successfully.");

            foreach (ModuleState module in modules) {
                StartRunningModule(module);
            }

            while (!shutdown) {
                await Task.Delay(500);
            }

            await Shutdown();

            Task ignored = webHost.StopAsync(); // Don't wait for StopAsync to finish (takes a few seconds)
        }

        private Variable GetVariableDescription(VariableRef varRef) {
            string moduleID = varRef.Object.ModuleID;
            ModuleState module = modules.FirstOrDefault(m => m.ID == moduleID);
            if (module == null) return null;
            return module.GetVarDescription(varRef);
        }

        private async Task InitModule(ModuleState module) {
            Module info = module.Config;
            try {
                logger.Info($"Starting module {module.Name}...");
                VariableValue[] restoreVariableValues = module.GetVariableValues();

                var configItems = info.Config.ToList();

                if (!string.IsNullOrEmpty(info.ExternalCommand)) {
                    configItems.Add(new NamedValue("ExternalCommand", info.ExternalCommand));
                }
                if (!string.IsNullOrEmpty(info.ExternalArgs)) {
                    configItems.Add(new NamedValue("ExternalArgs", info.ExternalArgs));
                }

                var initInfo = new ModuleInitInfo() {
                    ModuleID = module.ID,
                    ModuleName = module.Name,
                    LoginPassword = module.Password,
                    LoginServer = "localhost",
                    LoginPort = listenPort,
                    Configuration = configItems.ToArray()
                };
                await module.Instance.Init(initInfo, restoreVariableValues, module, null);
                ObjectInfo[] allObjs = await module.Instance.GetAllObjects();
                module.SetAllObjects(allObjs);
                module.State = State.InitComplete;
                logger.Info($"Init of module {module.Name} completed.");
            }
            catch (Exception exp) {
                module.State = State.InitError;
                module.LastError = exp.Message;
                throw new Exception($"Startup of module {info.Name} failed: " + exp.Message, exp);
            }
        }

        private void StartRunningModule(ModuleState module) {

            module.State = State.Running;
            Func<bool> fShutdown = () => { return module.State == State.ShutdownStarted; };
            Task runTask = module.Instance
                .Run(fShutdown)
                .ContinueOnMainThread((task) => {
                    if (module.State == State.Running) {
                        string restartReason = "";
                        if (task.IsFaulted) {
                            string msg = $"Module '{module.Name}' threw exception in Run: " + task.Exception.GetBaseException().Message;
                            Log_Exception(Severity.Warning, task.Exception, "ModuleRunError", msg, module.ID);
                            restartReason = "Exception in Run";
                        }
                        else {
                            restartReason = "Early return from Run";
                        }
                        Task.Delay(1000).ContinueOnMainThread( (tt) => {
                            Task ignored = RestartModule(module, restartReason);
                        });
                    }
                });
            module.RunTask = runTask;
        }

        private async Task Shutdown() {
            await ShutdownModules(modules);
            await history.Stop();
        }

        private async Task ShutdownModules(IEnumerable<ModuleState> modules) {

            Task[] shutdownTasks = modules
                .Where(a => a.State == State.InitComplete || a.State == State.InitError || a.State == State.Running)
                .Select(ShutdownModule)
                .ToArray();

            await Task.WhenAll(shutdownTasks);
        }

        private async Task ShutdownModule(ModuleState module) {
            State s = module.State;
            if (s == State.ShutdownStarted || s == State.ShutdownCompleted) return;

            module.State = State.ShutdownStarted;
            logger.Info($"Starting shutdown of module {module.Name}...");
            try {
                if (module.RunTask != null) {
                    await module.RunTask;
                }
                if (s == State.InitComplete || s == State.InitError) {
                    await module.Instance.InitAbort();
                }
                await module.FlushVariables();
                logger.Info($"Shutdown of module {module.Name} completed.");
            }
            catch (Exception exp) {
                string msg = $"Shutdown exception in module '{module.Name}': " + exp.Message;
                Log_Exception(Severity.Warning, exp, "ModuleShutdownError", msg, module.ID);
            }
            module.State = State.ShutdownCompleted;
            module.Instance = null;
        }

        internal async Task RestartModule(ModuleState module, string reason, int tryCounter = 0) {

            Log_Warn("ModuleRestart", $"Restarting module '{module.Name}'. Reason: {reason}", module.ID);

            const int TimeoutSeconds = 10;
            try {
                Task tShutdown = ShutdownModule(module);
                Task t = await Task.WhenAny(tShutdown, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
                if (t != tShutdown) {
                    string msg = $"Shutdown request for module '{module.Name}' failed to complete within {TimeoutSeconds} seconds.";
                    Log_Warn("ShutdownTimeout", msg, module.ID);
                    // go ahead and hope for the best...
                }
                if (shutdown) return;
                module.CreateInstance();
                await InitModule(module);
                StartRunningModule(module);
            }
            catch (Exception exp) {
                Log_Exception(Severity.Alarm, exp, "ModuleRestartError", $"Restart of module '{module.Name}' failed: {exp.Message}", module.ID);
                int delayMS = Math.Min(10*1000, (tryCounter + 1) * 1000);
                await Task.Delay(delayMS);
                Task ignored = RestartModule(module, reason, tryCounter + 1);
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

                if (request.Path == "/Mediator/" && context.WebSockets.IsWebSocketRequest) {
                    WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await HandleClientWebSocket(webSocket);
                    return;
                }

                if (request.Method != "POST" || !HandleClientRequests.IsValid(request.Path)) {
                    logger.Warn("Invalid request " + request.Path.ToUriComponent());
                    response.StatusCode = 400; // BAD Request
                    return;
                }

                //logger.Info("Client request: " + request.Path);

                JObject obj = null;
                using (var body = request.Body) {
                    using (var reader = new StreamReader(body, Encoding.UTF8)) {
                        obj = await StdJson.JObjectFromReaderAsync(reader);
                        //logger.Info("ReqStr: " + StdJson.ObjectToString(obj, true));
                    }
                }

                using (ReqResult result = await reqHandler.Handle(request.Path, obj)) {

                    //logger.Info(result.AsString());

                    response.StatusCode = result.StatusCode;
                    response.ContentLength = result.Bytes.Length;
                    response.ContentType = "application/json";

                    try {
                        await result.Bytes.CopyToAsync(response.Body);
                    }
                    catch (Exception) {
                        response.StatusCode = 500;
                    }
                }
            }
            catch (Exception exp) {
                response.StatusCode = 500;
                logger.Warn(exp.GetBaseException(), "Error handling client request");
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

                    var session = Encoding.UTF8.GetString(receiveBuffer, 0, count);
                    await reqHandler.HandleNewWebSocketSession(session, socket);
                    // we only arrive here when the web socket is not used anymore, i.e. the session is closed!
                }
            }
            catch (Exception exp) {
                logger.Warn(exp.GetBaseException(), "Error handling web socket request");
            }
        }

        internal void Notify_VariableValuesChanged(ModuleState module, IList<VariableValue> values) {
            if (logger.IsDebugEnabled) {
                logger.Debug("VariableValuesChanged:\n\t" + string.Join("\n\t", values.Select(x => x.ToString())));
            }
            module.UpdateVariableValues(values);
        }

        internal void Notify_ConfigChanged(ModuleState module, IList<ObjectRef> changedObjects) {

            try {
                Task<ObjectInfo[]> task = module.Instance.GetAllObjects();
                task.ContinueOnMainThread(t => {
                    if (t.IsFaulted) {
                        var ignored = RestartModule(module, "GetAllObjects() failed in Notify_ConfigChanged: " + t.Exception?.Message);
                    }
                    else {
                        ObjectInfo[] allObjs = t.Result;
                        module.SetAllObjects(allObjs);
                    }
                });
            }
            catch (Exception exp) {
                var ignored = RestartModule(module, "GetAllObjects() failed in Notify_ConfigChanged: " + exp.Message);
            }

            reqHandler.OnConfigChanged(changedObjects, module.GetObjectParent);
        }

        internal void Notify_AlarmOrEvent(ModuleState module, AlarmOrEventInfo e) {

            var ae = new AlarmOrEvent() {
                ModuleID = module.ID,
                ModuleName = module.Name,
                Time = e.Time,
                IsSystem = false,
                Severity = e.Severity,
                Type = e.Type,
                Message = e.Message,
                Details = e.Details,
                AffectedObjects = e.AffectedObjects,
                Initiator = e.Initiator
            };
            reqHandler.OnAlarmOrEvent(ae);

            string msg = e.Message;

            switch (e.Severity) {

                case Severity.Info:
                    module.logger.Info(msg);
                    break;

                case Severity.Warning:
                    module.logger.Warn(msg);
                    break;

                case Severity.Alarm:
                    module.logger.Error(msg);
                    break;
            }
        }

        private void Log_Exception(Severity severity, Exception exp, string type, string msg, string moduleID) {
            Exception expBase = exp.GetBaseException() ?? exp;
            string details = expBase.StackTrace;
            Log_Event(severity, type, msg, moduleID, details);
        }

        private void Log_Warn(string type, string msg, string moduleID = "", string details = "") {
            Log_Event(Severity.Warning, type, msg, moduleID, details);
        }

        private void Log_Event(Severity severity, string type, string msg, string moduleID = "", string details = "") {
            var module = modules.FirstOrDefault(m => m.ID == moduleID);

            var ae = new AlarmOrEvent() {
                ModuleID = moduleID,
                ModuleName = module != null ? module.Name : "",
                Time = Timestamp.Now,
                IsSystem = true,
                Severity = severity,
                Type = type,
                Message = msg,
                Details = details,
            };

            reqHandler.OnAlarmOrEvent(ae);

            switch (severity) {

                case Severity.Info:
                    logger.Info(msg);
                    break;

                case Severity.Warning:
                    logger.Warn(msg);
                    break;

                case Severity.Alarm:
                    logger.Error(msg);
                    break;
            }
        }
    }

    public class ModuleState : Notifier
    {
        public Logger logger;
        public Module Config { get; private set; }
        private readonly MediatorCore core;
        private readonly ModuleVariables variables;

        public ModuleState(Module config, MediatorCore core) {
            this.logger = LogManager.GetLogger(config.Name);
            this.Config = config;
            this.core = core;
            this.State = State.Created;
            this.Password = Guid.NewGuid().ToString();
            this.variables = new ModuleVariables(config.ID, config.Name, config.VariablesFileName);
            this.variables.Load();
        }

        public IList<ObjectInfo> AllObjects => allObjects;
        private List<ObjectInfo> allObjects = new List<ObjectInfo>();
        private Dictionary<ObjectRef, ObjectInfo> mapObjects = new Dictionary<ObjectRef, ObjectInfo>();

        public void SetAllObjects(ObjectInfo[] allObjs) {
            allObjects = new List<ObjectInfo>(allObjs);
            mapObjects.Clear();
            foreach (ObjectInfo obj in allObjects) {
                mapObjects[obj.ID] = obj;
            }
            variables.Sync(allObjects);
        }
        public VTQ GetVarValue(VariableRef varRef) => variables.GetVarValue(varRef);
        public Variable GetVarDescription(VariableRef varRef) {
            try {
                string varName = varRef.Name;
                ObjectInfo objInfo = mapObjects[varRef.Object];
                return objInfo.Variables.First(v => v.Name == varName);
            }
            catch (Exception) {
                return null;
            }
        }
        public void ValidateVariableValuesOrThrow(IList<VariableValue> values) => variables.ValidateVariableValuesOrThrow(values);
        public void ValidateVariableRefsOrThrow(IList<VariableRef> varRefs) => variables.ValidateVariableRefsOrThrow(varRefs);
        public int UpdateVariableValues(IList<VariableValue> values) {
            if (values.Count > 0) {
                VaribleValuePrev[] valuesWithPrev = variables.UpdateVariableValues(values);

                //if (values.Count == 1) {
                //    logger.Info("UpdateVariable " + values[0].Variable.ToString() + ": " + previousValues[0].ToString() + " => " + values[0].Value.ToString());
                //}
                //else {
                //    for (int i = 0; i < values.Count; ++i) {
                //        logger.Info("UpdateVariable[" + i + "] " + values[0].Variable.ToString() + ": " + previousValues[0].ToString() + " => " + values[0].Value.ToString());
                //    }
                //}

                //for (int i = 0; i < values.Count; ++i) {
                //    logger.Info("Up[" + i.ToString("00") + "] " + values[i].ToString() + ": " + valuesWithPrev[i].PreviousValue.ToString() + " => " + values[i].Value.ToString());
                //}

                core.reqHandler.OnVariableValuesChanged(valuesWithPrev, GetObjectParent);
                return core.history.OnVariableValuesChanged(Config.ID, valuesWithPrev);
            }
            else {
                return 0;
            }
        }
        internal ObjectRef? GetObjectParent(ObjectRef o) {
            if (!mapObjects.ContainsKey(o)) return null;
            ObjectInfo info = mapObjects[o];
            if (info.Parent.HasValue) return info.Parent.Value.Object;
            return null;
        }

        public Task FlushVariables() => variables.Shutdown();
        public VariableValue[] GetVariableValues() => variables.GetVariableValues();
        public string ID => Config.ID;
        public string Name => Config.Name;
        public bool Enabled => Config.Enabled;
        public State State { get; set; }
        public string Password { get; private set; }
        public string LastError { get; set; }
        public SingleThreadModule Instance { get; set; }
        public Task RunTask { get; set; }

        public void CreateInstance() {

            string implAssembly = Config.ImplAssembly;
            const string releaseDebugPlaceHolder = "{RELEASE_OR_DEBUG}";
            if (implAssembly.Contains(releaseDebugPlaceHolder)) {
#if DEBUG
                implAssembly = implAssembly.Replace(releaseDebugPlaceHolder, "Debug");
#else
                implAssembly = implAssembly.Replace(releaseDebugPlaceHolder, "Release");
#endif
            }
            ModuleBase rawModule = Util.Reflect.CreateInstanceOrThrow<ModuleBase>(Config.ImplClass, implAssembly);
            Instance = new SingleThreadModule(rawModule, Config.Name);
            State = State.Created;
        }

        private readonly SynchronizationContext syncContext = SynchronizationContext.Current;

        public void Notify_VariableValuesChanged(IList<VariableValue> values) {
            syncContext.Post(delegate (object state) { core.Notify_VariableValuesChanged(this, values.ToArray()); }, null);
        }

        public void Notify_ConfigChanged(IList<ObjectRef> changedObjects) {
            syncContext.Post(delegate (object state) { core.Notify_ConfigChanged(this, changedObjects.ToArray()); }, null);
        }

        public void Notify_AlarmOrEvent(AlarmOrEventInfo alarmOrEventInfo) {
            syncContext.Post(delegate (object state) { core.Notify_AlarmOrEvent(this, alarmOrEventInfo); }, null);
        }
    }

    public enum State
    {
        Created,
        InitError,
        InitComplete,
        Running,
        ShutdownStarted,
        ShutdownCompleted
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
