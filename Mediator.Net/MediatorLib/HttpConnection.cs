// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using Ifak.Fast.Mediator.Util;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;
using ModuleInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ModuleInfo>;
using LocationInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.LocationInfo>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;
using ObjectValues = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectValue>;
using MemberValues = System.Collections.Generic.List<Ifak.Fast.Mediator.MemberValue>;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator
{
    public class HttpConnection : Connection
    {
        private string login = "";
        private Timestamp tLogin = Timestamp.Now;

        public static async Task<Connection> ConnectWithUserLogin(string host, int port, string login, string password, string[] roles = null, EventListener listener = null, int timeoutSeconds = 20) {

            if (host == null) throw new ArgumentNullException(nameof(host));
            if (login == null) throw new ArgumentNullException(nameof(login));
            if (password == null) throw new ArgumentNullException(nameof(password));

            var res = new HttpConnection(host, port, TimeSpan.FromSeconds(timeoutSeconds), $"User({login})");
            await res.DoConnectAndLogin(login, password, false, roles ?? new string[0], listener);
            return res;
        }

        public static async Task<Connection> ConnectWithModuleLogin(ModuleInitInfo info, EventListener listener = null, int timeoutSeconds = 60) {
            var res = new HttpConnection(info.LoginServer, info.LoginPort, TimeSpan.FromSeconds(timeoutSeconds), $"Module({info.ModuleID})" /*, info.InProcApi*/);
            await res.DoConnectAndLogin(info.ModuleID, info.LoginPassword, true, new string[0], listener);
            return res;
        }

        protected readonly HttpClient client;
        protected readonly Uri wsUri;

        protected EventManager eventManager = null;
        protected string session = null;
        //private InProcApi inProc = null;

        protected HttpConnection(string host, int port, TimeSpan timeout, string login /*, InProcApi inProc = null*/) {

            //this.inProc = inProc;
            this.login = login;

            Uri baseUri = new Uri("http://" + host + ":" + port + "/Mediator/");
            wsUri = new Uri("ws://" + host + ":" + port + "/Mediator/");
            client = new HttpClient();
            client.Timeout = timeout;
            client.BaseAddress = baseUri;
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        protected async Task DoConnectAndLogin(string login, string password, bool isModule, string[] roles, EventListener listener) {

            var reqLogin = new LoginReq();

            if (isModule) {
                reqLogin.ModuleID = login;
            }
            else {
                reqLogin.Login = login;
                reqLogin.Roles = roles;
            }

            LoginResponse loginResponse = await Post<LoginResponse>(reqLogin);

            string session = loginResponse.Session;
            string challenge = loginResponse.Challenge;
            if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(challenge))
                throw new ConnectivityException("Invalid response");

            long hash = ClientDefs.strHash(password + challenge + password + session);

            var reqAuth = new AuthenticateReq() {
                Session = session,
                Hash = hash
            };

            AuthenticateResponse authResponse = await Post<AuthenticateResponse>(reqAuth);

            this.session = authResponse.Session;
            tLogin = Timestamp.Now;

            if (listener != null) {

                var reqEnablePing = new EnableEventPingReq() {
                    Session = session
                };
                await PostJObject(reqEnablePing);

                eventManager = new EventManager(listener);
                await eventManager.StartWebSocket(this.session, wsUri, OnConnectionBroken);
            }
        }

        public override bool IsClosed => session == null;

        public override async Task Close() {

            string session = this.session;
            if (session == null) return;
            this.session = null;

            eventManager?.Close();
            eventManager = null;

            var request = new LogoutReq() {
                Session = session
            };

            try {
                await PostJObject(request);
            }
            catch (Exception) {
                // Console.Error.WriteLine("Exception in " + nameof(HttpConnection) + "." + nameof(Close) + ": " + exp.Message);
            }

            client.Dispose();
        }

        public override void Dispose() {
            _ = Close();
        }

        protected void OnConnectionBroken(string context, Exception exp) {

            this.session = null;

            ReportConnectionBroken(login, tLogin, context, exp);

            eventManager?.Close();
            eventManager = null;
            client.Dispose();
        }

        private static void ReportConnectionBroken(string login, Timestamp tLogin, string context, Exception exp) {

            if (context.Contains("request because system is shutting down.")) {
                return;
            }

            string now = Timestamp.Now.ToDateTime().ToLocalTime().ToString("HH':'mm':'ss'.'fff", System.Globalization.CultureInfo.InvariantCulture);
            string at = tLogin.ToDateTime().ToLocalTime().ToString("yyyy'-'MM'-'dd\u00A0HH':'mm':'ss", System.Globalization.CultureInfo.InvariantCulture);
            string s = $"{now}: ConnectionBroken in {context}; Login: {login} at {at}";
            if (exp != null) {
                s += Environment.NewLine;
                string indent = new string(' ', now.Length + 1);
                Exception e = exp.GetBaseException() ?? exp;
                s += $"{indent} --> {e.GetType().FullName}: {e.Message}";
            }
            Console.Error.WriteLine(s);
            Console.Error.Flush();
        }

        #region Methods

        public override async Task Ping() {
            var request = MakeSessionRequest<PingReq>();
            await PostJObject(request);
        }

        public override async Task EnableAlarmsAndEvents(Severity minSeverity = Severity.Info) {
            var request = MakeSessionRequest<EnableAlarmsAndEventsReq>();
            request.MinSeverity = minSeverity;
            await PostJObject(request);
        }

        public override async Task DisableAlarmsAndEvents() {
            var request = MakeSessionRequest<DisableAlarmsAndEventsReq>();
            await PostJObject(request);
        }

        public override async Task EnableConfigChangedEvents(params ObjectRef[] objects) {
            var request = MakeSessionRequest<EnableConfigChangedEventsReq>();
            request.Objects = objects;
            await PostJObject(request);
        }

        public override async Task EnableVariableHistoryChangedEvents(params VariableRef[] variables) {
            var request = MakeSessionRequest<EnableVariableHistoryChangedEventsReq>();
            request.Variables = variables;
            await PostJObject(request);
        }

        public override async Task EnableVariableHistoryChangedEvents(params ObjectRef[] idsOfEnabledTreeRoots) {
            var request = MakeSessionRequest<EnableVariableHistoryChangedEventsReq>();
            request.IdsOfEnabledTreeRoots = idsOfEnabledTreeRoots;
            await PostJObject(request);
        }

        public override async Task EnableVariableValueChangedEvents(SubOptions options, params VariableRef[] variables) {
            var request = MakeSessionRequest<EnableVariableValueChangedEventsReq>();
            request.Options = options;
            request.Variables = variables;
            await PostJObject(request);
        }

        public override async Task EnableVariableValueChangedEvents(SubOptions options, params ObjectRef[] idsOfEnabledTreeRoots) {
            var request = MakeSessionRequest<EnableVariableValueChangedEventsReq>();
            request.Options = options;
            request.IdsOfEnabledTreeRoots = idsOfEnabledTreeRoots;
            await PostJObject(request);
        }

        public override async Task DisableChangeEvents(bool disableVarValueChanges, bool disableVarHistoryChanges, bool disableConfigChanges) {
            var request = MakeSessionRequest<DisableChangeEventsReq>();
            request.DisableVarValueChanges = disableVarValueChanges;
            request.DisableVarHistoryChanges = disableVarHistoryChanges;
            request.DisableConfigChanges = disableConfigChanges;
            await PostJObject(request);
        }

        public override async Task<User> GetLoginUser() {
            var request = MakeSessionRequest<GetLoginUserReq>();
            return await Post<User>(request);
        }

        public override async Task<ModuleInfos> GetModules() {
            var request = MakeSessionRequest<GetModulesReq>();
            return await Post<ModuleInfos>(request);
        }

        public override async Task<LocationInfos> GetLocations() {
            var request = MakeSessionRequest<GetLocationsReq>();
            return await Post<LocationInfos>(request);
        }

        public override async Task<ObjectInfos> GetAllObjects(string moduleID) {
            var request = MakeSessionRequest<GetAllObjectsReq>();
            request.ModuleID = moduleID;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectInfos> GetAllObjectsOfType(string moduleID, string className) {
            var request = MakeSessionRequest<GetAllObjectsOfTypeReq>();
            request.ModuleID = moduleID;
            request.ClassName = className;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectInfos> GetAllObjectsWithVariablesOfType(string moduleID, params DataType[] types) {
            var request = MakeSessionRequest<GetAllObjectsWithVariablesOfTypeReq>();
            request.ModuleID = moduleID;
            request.Types = types;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectInfos> GetChildrenOfObjects(params ObjectRef[] objectIDs) {
            var request = MakeSessionRequest<GetChildrenOfObjectsReq>();
            request.ObjectIDs = objectIDs;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<MemberValues> GetMemberValues(MemberRef[] member) {
            if (member == null) throw new ArgumentNullException(nameof(member));
            var request = MakeSessionRequest<GetMemberValuesReq>();
            request.Member = member;
            return await Post<MemberValues>(request);
        }

        public override async Task<MetaInfos> GetMetaInfos(string moduleID) {
            if (moduleID == null) throw new ArgumentNullException(nameof(moduleID));
            var request = MakeSessionRequest<GetMetaInfosReq>();
            request.ModuleID = moduleID;
            return await Post<MetaInfos>(request);
        }

        public override async Task<ObjectInfos> GetObjectsByID(params ObjectRef[] objectIDs) {
            var request = MakeSessionRequest<GetObjectsByIDReq>();
            request.ObjectIDs = objectIDs;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectValues> GetObjectValuesByID(params ObjectRef[] objectIDs) {
            var request = MakeSessionRequest<GetObjectValuesByIDReq>();
            request.ObjectIDs = objectIDs;
            return await Post<ObjectValues>(request);
        }

        public override async Task<ObjectValue> GetParentOfObject(ObjectRef objectID) {
            var request = MakeSessionRequest<GetParentOfObjectReq>();
            request.ObjectID = objectID;
            return await Post<ObjectValue>(request);
        }

        public override async Task<ObjectInfo> GetRootObject(string moduleID) {
            var request = MakeSessionRequest<GetRootObjectReq>();
            request.ModuleID = moduleID;
            return await Post<ObjectInfo>(request);
        }

        public override async Task<long> HistorianCount(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter = QualityFilter.ExcludeNone) {
            var request = MakeSessionRequest<HistorianCountReq>();
            request.Variable = variable;
            request.StartInclusive = startInclusive;
            request.EndInclusive = endInclusive;
            request.Filter = filter;
            return await Post<long>(request);
        }

        public override async Task HistorianDeleteAllVariablesOfObjectTree(ObjectRef objectID) {
            var request = MakeSessionRequest<HistorianDeleteAllVariablesOfObjectTreeReq>();
            request.ObjectID = objectID;
            await PostJObject(request);
        }

        public override async Task HistorianDeleteVariables(params VariableRef[] variables) {
            var request = MakeSessionRequest<HistorianDeleteVariablesReq>();
            request.Variables = variables;
            await PostJObject(request);
        }

        public override async Task<long> HistorianDeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
            var request = MakeSessionRequest<HistorianDeleteIntervalReq>();
            request.Variable = variable;
            request.StartInclusive = startInclusive;
            request.EndInclusive = endInclusive;
            return await Post<long>(request);
        }

        public override async Task<VTTQ?> HistorianGetLatestTimestampDB(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
            var request = MakeSessionRequest<HistorianGetLatestTimestampDBReq>();
            request.Variable = variable;
            request.StartInclusive = startInclusive;
            request.EndInclusive = endInclusive;
            return await Post<VTTQ?>(request);
        }

        public override async Task HistorianModify(VariableRef variable, ModifyMode mode, params VTQ[] data) {
            var request = MakeSessionRequest<HistorianModifyReq>();
            request.Variable = variable;
            request.Data = data;
            request.Mode = mode;
            await PostJObject(request);
        }

        public override async Task<VTTQs> HistorianReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter = QualityFilter.ExcludeNone) {
            var request = MakeSessionRequest<HistorianReadRawReq>();
            request.Variable = variable;
            request.StartInclusive = startInclusive;
            request.EndInclusive = endInclusive;
            request.MaxValues = maxValues;
            request.Bounding = bounding;
            request.Filter = filter;
            return await Post<VTTQs>(request);
        }

        public override async Task<VariableValues> ReadAllVariablesOfObjectTree(ObjectRef objectID) {
            var request = MakeSessionRequest<ReadAllVariablesOfObjectTreeReq>();
            request.ObjectID = objectID;
            return await Post<VariableValues>(request);
        }

        public override async Task<VTQs> ReadVariables(VariableRef[] variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesReq>();
            request.Variables = variables;
            return await Post<VTQs>(request);
        }

        public override async Task<VariableValues> ReadVariablesIgnoreMissing(VariableRef[] variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesIgnoreMissingReq>();
            request.Variables = variables;
            return await Post<VariableValues>(request);
        }

        public override async Task<VTQs> ReadVariablesSync(VariableRef[] variables, Duration? timeout = null) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesSyncReq>();
            request.Variables = variables;
            request.Timeout = timeout;
            Task<VTQs> task = Post<VTQs>(request);
            if (timeout.HasValue) {
                if (task == await Task.WhenAny(task, Task.Delay(timeout.Value.ToTimeSpan()))) {
                    return await task;
                }
                else {
                    throw new Exception("Timeout");
                }
            }
            else {
                return await task;
            }
        }

        public override async Task<VariableValues> ReadVariablesSyncIgnoreMissing(VariableRef[] variables, Duration? timeout = null) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesSyncIgnoreMissingReq>();
            request.Variables = variables;
            request.Timeout = timeout;

            Task<VariableValues> task = Post<VariableValues>(request);
            if (timeout.HasValue) {
                if (task == await Task.WhenAny(task, Task.Delay(timeout.Value.ToTimeSpan()))) {
                    return await task;
                }
                else {
                    throw new Exception("Timeout");
                }
            }
            else {
                return await task;
            }
        }

        public override async Task UpdateConfig(ObjectValue[] updateOrDeleteObjects, MemberValue[] updateOrDeleteMembers, AddArrayElement[] addArrayElements) {
            var request = MakeSessionRequest<UpdateConfigReq>();
            request.UpdateOrDeleteObjects = updateOrDeleteObjects;
            request.UpdateOrDeleteMembers = updateOrDeleteMembers;
            request.AddArrayElements = addArrayElements;
            await PostJObject(request);
        }

        public override Task WriteVariables(VariableValue[] values) {
            var request = MakeSessionRequest<WriteVariablesReq>();
            request.Values = values;
            return PostJObject(request);
        }

        public override Task<WriteResult> WriteVariablesIgnoreMissing(VariableValue[] values) {
            var request = MakeSessionRequest<WriteVariablesIgnoreMissingReq>();
            request.Values = values;
            return Post<WriteResult>(request);
        }

        public override async Task<WriteResult> WriteVariablesSync(VariableValue[] values, Duration? timeout = null) {
            var request = MakeSessionRequest<WriteVariablesSyncReq>();
            request.Values = values;
            request.Timeout = timeout;
            Task<WriteResult> task = Post<WriteResult>(request);
            if (timeout.HasValue) {
                if (task == await Task.WhenAny(task, Task.Delay(timeout.Value.ToTimeSpan()))) {
                    return await task;
                }
                else {
                    throw new Exception("Timeout");
                }
            }
            else {
                return await task;
            }
        }

        public override async Task<WriteResult> WriteVariablesSyncIgnoreMissing(VariableValue[] values, Duration? timeout = null) {
            var request = MakeSessionRequest<WriteVariablesSyncIgnoreMissingReq>();
            request.Values = values;
            request.Timeout = timeout;
            Task<WriteResult> task = Post<WriteResult>(request);
            if (timeout.HasValue) {
                if (task == await Task.WhenAny(task, Task.Delay(timeout.Value.ToTimeSpan()))) {
                    return await task;
                }
                else {
                    throw new Exception("Timeout");
                }
            }
            else {
                return await task;
            }
        }

        public override Task<DataValue> CallMethod(string moduleID, string methodName, params NamedValue[] parameters) {
            var request = MakeSessionRequest<CallMethodReq>();
            request.ModuleID = moduleID;
            request.MethodName = methodName;
            request.Parameters = parameters;
            return Post<DataValue>(request);
        }

        public override Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {
            var request = MakeSessionRequest<BrowseObjectMemberValuesReq>();
            request.Member = member;
            request.ContinueID = continueID;
            return Post<BrowseResult>(request);
        }

        #endregion

        protected async Task PostJObject(RequestBase obj) {

            //if (inProc != null) {
            //    return (JObject)await inProc.AddRequest(obj);
            //}

            string path = obj.GetPath();
            var payload = new StringContent(StdJson.ObjectToString(obj), Encoding.UTF8);

            HttpResponseMessage response;
            try {
                response = await client.PostAsync(path, payload);
            }
            catch (TaskCanceledException exp) {
                OnConnectionBroken($"PostJObject {path} client.PostAsync TaskCanceled", exp);
                throw new ConnectivityException("Time out");
            }
            catch (Exception exp) {
                OnConnectionBroken($"PostJObject {path} client.PostAsync", exp);
                throw new ConnectivityException(exp.Message);
            }

            using (response) {
                if (!response.IsSuccessStatusCode) {
                    await ThrowError(response, $"PostJObject {path}");
                }
            }
        }

        protected async Task<T> Post<T>(RequestBase obj) {

            //if (inProc != null) {
            //    return (T)await inProc.AddRequest(obj);
            //}

            string path = obj.GetPath();
            var requestStream = MemoryManager.GetMemoryStream("HttpConnection.Post");
            try {
                StdJson.ObjectToStream(obj, requestStream);
                requestStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception) {
                requestStream.Dispose();
                throw;
            }

            HttpResponseMessage response = null;

            using (var payload = new StreamContent(requestStream)) {

                try {
                    response = await client.PostAsync(path, payload);
                }
                catch (TaskCanceledException exp) {
                    OnConnectionBroken($"Post<T> {path} client.PostAsync TaskCanceled", exp);
                    throw new ConnectivityException("Time out");
                }
                catch (Exception exp) {
                    OnConnectionBroken($"Post<T> {path} client.PostAsync", exp);
                    throw new ConnectivityException(exp.Message);
                }
            }

            using (response) {
                if (response.IsSuccessStatusCode) {
                    try {
                        Stream stream = await response.Content.ReadAsStreamAsync();
                        using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                            return StdJson.ObjectFromReader<T>(reader);
                        }
                    }
                    catch (TaskCanceledException exp) {
                        OnConnectionBroken($"Post<T> {path} response.Content.ReadAsStreamAsync TaskCanceled", exp);
                        throw new ConnectivityException("Time out");
                    }
                    catch (Exception exp) {
                        OnConnectionBroken($"Post<T> {path} response.Content.ReadAsStreamAsync", exp);
                        throw new ConnectivityException(exp.Message);
                    }
                }
                else {
                    await ThrowError(response, $"Post<T> {path}");
                    return default(T); // never come here
                }
            }
        }

        protected async Task ThrowError(HttpResponseMessage response, string context) {

            string content = null;
            try {
                content = await response.Content.ReadAsStringAsync();
            }
            catch (Exception) {}

            ErrorResult errObj = null;
            try {
                errObj = StdJson.ObjectFromString<ErrorResult>(content);
            }
            catch (Exception) {}

            if (errObj == null || errObj.Error == null) {
                string errMsg = string.IsNullOrWhiteSpace(content) ? response.StatusCode.ToString() : content;
                OnConnectionBroken($"ThrowError {context} '{errMsg}'", null);
                throw new ConnectivityException(errMsg);
            }
            else {
                throw new RequestException(errObj.Error);
            }
        }

        private const string ConnectionClosedMessage = "Connection is closed.";

        private T MakeSessionRequest<T>() where T: RequestBase, new() {
            if (IsClosed) throw new ConnectivityException(ConnectionClosedMessage);
            T t = new T();
            t.Session = session;
            return t;
        }

        public class EventManager
        {
            protected readonly EventListener listener;

            protected CancellationTokenSource webSocketCancel;
            protected ClientWebSocket webSocket;

            public EventManager(EventListener listener) {
                this.listener = listener;
            }

            public async Task StartWebSocket(string session, Uri wsUri, Action<string, Exception> notifyConnectionBroken) {

                webSocketCancel = new CancellationTokenSource();
                webSocket = new ClientWebSocket();
                webSocket.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(0);

                await webSocket.ConnectAsync(wsUri, CancellationToken.None);

                var sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(session));
                await webSocket.SendAsync(sendBuffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: CancellationToken.None);

                _ = ReadWebSocketForEvents(webSocketCancel.Token, notifyConnectionBroken);
            }

            protected async Task ReadWebSocketForEvents(CancellationToken cancelToken, Action<string, Exception> notifyConnectionBroken) {

                byte[] bytesOK = new byte[] { (byte)'O', (byte)'K' };
                ArraySegment<byte> ok = new ArraySegment<byte>(bytesOK);
                var buffer = new ArraySegment<byte>(new byte[8192]);
                var stream = new MemoryStream(8192);

                while (!cancelToken.IsCancellationRequested) {

                    stream.Seek(0, SeekOrigin.Begin);

                    WebSocketReceiveResult result;
                    do {
                        try {
                            result = await webSocket.ReceiveAsync(buffer, cancelToken);
                        }
                        catch (Exception exp) {
                            _ = CloseSocket(); // no need to wait for completion
                            if (!cancelToken.IsCancellationRequested) {
                                notifyConnectionBroken("ReadWebSocketForEvents ReceiveAsync", exp);
                            }
                            _ = listener.OnConnectionClosed();
                            return;
                        }
                        stream.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    stream.Seek(0, SeekOrigin.Begin);

                    if (result.MessageType == WebSocketMessageType.Text) {

                        EventContent eventObj = null;
                        using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true)) {
                            eventObj = StdJson.ObjectFromReader<EventContent>(reader);
                        }

                        try {
                            await DispatchEvent(eventObj);
                        }
                        catch (Exception exp) {
                            Exception exception = exp.GetBaseException() ?? exp;
                            string msg = "Exception in event dispatch: " + exception.Message;
                            if (exception is ConnectivityException) {
                                Console.Out.WriteLine(msg);
                            }
                            else {
                                Console.Error.WriteLine(msg);
                            }
                        }

                        try {
                            await webSocket.SendAsync(ok, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                        catch (Exception exp) {
                            _ = CloseSocket(); // no need to wait for completion
                            if (!cancelToken.IsCancellationRequested) {
                                notifyConnectionBroken("ReadWebSocketForEvents SendAsync(ok)", exp);
                            }
                            _ = listener.OnConnectionClosed();
                            return;
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close) {
                        await CloseSocket();
                        if (!cancelToken.IsCancellationRequested) {
                            notifyConnectionBroken($"ReadWebSocketForEvents Close message received '{result.CloseStatusDescription}'", null);
                        }
                        await listener.OnConnectionClosed();
                        return;
                    }
                }
            }

            private async Task CloseSocket() {
                try {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                }
                catch (Exception) { }
            }

            public void Close() {
                try {
                    webSocketCancel?.Cancel();
                }
                catch (Exception) { }
                try {
                    webSocketCancel?.Dispose();
                }
                catch (Exception) { }
                webSocketCancel = null;
            }

            protected Task DispatchEvent(EventContent theEvent) {

                string eventName = theEvent.Event;

                switch (eventName) {
                    case "OnVariableValueChanged": {
                            List<VariableValue> variables = theEvent.Variables;
                            return listener.OnVariableValueChanged(variables);
                        }

                    case "OnVariableHistoryChanged": {
                            List<HistoryChange> variables = theEvent.Changes;
                            return listener.OnVariableHistoryChanged(variables);
                        }

                    case "OnConfigChanged": {
                            List<ObjectRef> changes = theEvent.ChangedObjects;
                            return listener.OnConfigChanged(changes);
                        }

                    case "OnAlarmOrEvent": {
                            List<AlarmOrEvent> alarmOrEvents = theEvent.Events;
                            return listener.OnAlarmOrEvents(alarmOrEvents);
                        }

                    case "OnPing": {
                            return Task.FromResult(true);
                        }

                    default:
                        Console.Error.WriteLine("Unknown event: " + eventName);
                        return Task.FromResult(true);
                }
            }
        }
    }
}
