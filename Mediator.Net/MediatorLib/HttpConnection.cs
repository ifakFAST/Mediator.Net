// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
using VariableRefs = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableRef>;
using Ifak.Fast.Mediator.BinSeri;

namespace Ifak.Fast.Mediator
{
    public class HttpConnection : Connection
    {
        private string login = "";
        private Timestamp tLogin = Timestamp.Now;
        private byte binaryVersion;
        private string role = "";

        private readonly bool[] MapBinaryMessages = new bool[64];

        private readonly MediaTypeWithQualityHeaderValue mediaJSON = new MediaTypeWithQualityHeaderValue("application/json");
        private readonly MediaTypeWithQualityHeaderValue mediaBinary = new MediaTypeWithQualityHeaderValue("application/octet-stream");

        public const byte CurrentEventDataFormatVersion = 1;

        public static async Task<Connection> ConnectWithUserLogin(string host, int port, string login, string password, string[]? roles = null, EventListener? listener = null, int timeoutSeconds = 20) {

            if (host == null) throw new ArgumentNullException(nameof(host));
            if (login == null) throw new ArgumentNullException(nameof(login));
            if (password == null) throw new ArgumentNullException(nameof(password));

            var res = new HttpConnection(host, port, TimeSpan.FromSeconds(timeoutSeconds), $"User({login})");
            await res.DoConnectAndLogin(login, password, false, roles ?? new string[0], listener);
            return res;
        }

        public static async Task<Connection> ConnectWithModuleLogin(ModuleInitInfo info, EventListener? listener = null, int timeoutSeconds = 60) {
            var res = new HttpConnection(info.LoginServer, info.LoginPort, TimeSpan.FromSeconds(timeoutSeconds), $"Module({info.ModuleID})", info.InProcApi);
            await res.DoConnectAndLogin(info.ModuleID, info.LoginPassword, true, new string[0], listener);
            return res;
        }

        protected readonly HttpClient client;
        protected readonly Uri wsUri;

        protected EventManager? eventManager = null;
        protected string? session = null;
        private InProcApi? inProc = null;

        protected HttpConnection(string host, int port, TimeSpan timeout, string login, InProcApi? inProc = null) {

            this.inProc = inProc;
            this.login = login;

            Uri baseUri = new Uri("http://" + host + ":" + port + "/Mediator/");
            wsUri = new Uri("ws://" + host + ":" + port + "/Mediator/");
            client = new HttpClient();
            client.Timeout = timeout;
            client.BaseAddress = baseUri;
        }

        protected async Task DoConnectAndLogin(string login, string password, bool isModule, string[]? roles, EventListener? listener) {

            var reqLogin = new LoginReq();

            if (isModule) {
                reqLogin.ModuleID = login;
            }
            else {
                reqLogin.Login = login;
                reqLogin.Roles = roles ?? new string[0];
            }
            reqLogin.MediatorVersion = VersionInfo.ifakFAST_Str();

            LoginResponse loginResponse = await Post<LoginResponse>(reqLogin);
            string session = loginResponse.Session;
            string challenge = loginResponse.Challenge;
            role = loginResponse.Role ?? "";
            if (string.IsNullOrEmpty(session) || string.IsNullOrEmpty(challenge))
                throw new ConnectivityException("Invalid response");

            long hash = ClientDefs.strHash(password + challenge + password + session);

            binaryVersion = Math.Min(loginResponse.BinaryVersion, BinSeri.Common.CurrentBinaryVersion);
            byte eventDataVersion = Math.Min(loginResponse.EventDataVersion, CurrentEventDataFormatVersion);

            if (loginResponse.BinMethods != null) {
                foreach (int id in loginResponse.BinMethods) {
                    if (id >= 0 && id < MapBinaryMessages.Length) {
                        MapBinaryMessages[id] = true;
                    }
                }
            }

            var reqAuth = new AuthenticateReq() {
                Session = session,
                Hash = hash,
                SelectedBinaryVersion = binaryVersion,
                SelectedEventDataVersion = eventDataVersion,
            };

            AuthenticateResponse authResponse = await Post<AuthenticateResponse>(reqAuth);

            this.session = authResponse.Session;
            tLogin = Timestamp.Now;

            if (listener != null) {

                var reqEnablePing = new EnableEventPingReq() {
                    Session = session
                };
                await PostVoid(reqEnablePing, ignoreError: true);

                eventManager = new EventManager(listener, eventDataVersion);
                await eventManager.StartWebSocket(this.session, wsUri, OnConnectionBroken);
            }
        }

        public override string UserRole => role;

        public override bool IsClosed => session == null;

        public override async Task Close() {

            string? session = this.session;
            if (session == null) return;
            this.session = null;

            eventManager?.Close();
            eventManager = null;

            var request = new LogoutReq() {
                Session = session
            };

            try {
                await PostVoid(request);
            }
            catch (Exception) {
                // Console.Error.WriteLine("Exception in " + nameof(HttpConnection) + "." + nameof(Close) + ": " + exp.Message);
            }

            client.Dispose();
        }

        public override void Dispose() {
            _ = Close();
        }

        protected void OnConnectionBroken(string context, string path, Exception? exp) {

            this.session = null;

            ReportConnectionBroken(login, tLogin, context, path, exp);

            eventManager?.Close();
            eventManager = null;
            client.Dispose();
        }

        private static void ReportConnectionBroken(string login, Timestamp tLogin, string context, string path, Exception? exp) {

            if (context.Contains("request because system is shutting down.")) {
                return;
            }
            if (path == LoginReq.Path) {
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
            await PostVoid(request);
        }

        public override async Task EnableAlarmsAndEvents(Severity minSeverity = Severity.Info) {
            var request = MakeSessionRequest<EnableAlarmsAndEventsReq>();
            request.MinSeverity = minSeverity;
            await PostVoid(request);
        }

        public override async Task DisableAlarmsAndEvents() {
            var request = MakeSessionRequest<DisableAlarmsAndEventsReq>();
            await PostVoid(request);
        }

        public override async Task EnableConfigChangedEvents(params ObjectRef[] objects) {
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            var request = MakeSessionRequest<EnableConfigChangedEventsReq>();
            request.Objects = objects;
            await PostVoid(request);
        }

        public override async Task EnableVariableHistoryChangedEvents(params VariableRef[] variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<EnableVariableHistoryChangedEventsReq>();
            request.Variables = variables;
            await PostVoid(request);
        }

        public override async Task EnableVariableHistoryChangedEvents(params ObjectRef[] idsOfEnabledTreeRoots) {
            if (idsOfEnabledTreeRoots == null) throw new ArgumentNullException(nameof(idsOfEnabledTreeRoots));
            var request = MakeSessionRequest<EnableVariableHistoryChangedEventsReq>();
            request.IdsOfEnabledTreeRoots = idsOfEnabledTreeRoots;
            await PostVoid(request);
        }

        public override async Task EnableVariableValueChangedEvents(SubOptions options, params VariableRef[] variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<EnableVariableValueChangedEventsReq>();
            request.Options = options;
            request.Variables = variables;
            await PostVoid(request);
        }

        public override async Task EnableVariableValueChangedEvents(SubOptions options, params ObjectRef[] idsOfEnabledTreeRoots) {
            if (idsOfEnabledTreeRoots == null) throw new ArgumentNullException(nameof(idsOfEnabledTreeRoots));
            var request = MakeSessionRequest<EnableVariableValueChangedEventsReq>();
            request.Options = options;
            request.IdsOfEnabledTreeRoots = idsOfEnabledTreeRoots;
            await PostVoid(request);
        }

        public override async Task DisableChangeEvents(bool disableVarValueChanges, bool disableVarHistoryChanges, bool disableConfigChanges) {
            var request = MakeSessionRequest<DisableChangeEventsReq>();
            request.DisableVarValueChanges = disableVarValueChanges;
            request.DisableVarHistoryChanges = disableVarHistoryChanges;
            request.DisableConfigChanges = disableConfigChanges;
            await PostVoid(request);
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
            if (types == null) throw new ArgumentNullException(nameof(types));
            var request = MakeSessionRequest<GetAllObjectsWithVariablesOfTypeReq>();
            request.ModuleID = moduleID;
            request.Types = types;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectInfos> GetChildrenOfObjects(params ObjectRef[] objectIDs) {
            if (objectIDs == null) throw new ArgumentNullException(nameof(objectIDs));
            var request = MakeSessionRequest<GetChildrenOfObjectsReq>();
            request.ObjectIDs = objectIDs;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectInfos> GetChildrenOfObjectsRecursive(ObjectRef[] objectIDs, string[] classNames) {
            if (objectIDs == null) throw new ArgumentNullException(nameof(objectIDs));
            if (classNames == null) throw new ArgumentNullException(nameof(classNames));
            var request = MakeSessionRequest<GetChildrenOfObjectsRecursiveReq>();
            request.ObjectIDs = objectIDs;
            request.ClassNames = classNames;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<MemberValues> GetMemberValues(MemberRef[] member, bool ignoreMissing) {
            if (member == null) throw new ArgumentNullException(nameof(member));
            var request = MakeSessionRequest<GetMemberValuesReq>();
            request.Member = member;
            request.IgnoreMissing = ignoreMissing;
            return await Post<MemberValues>(request);
        }

        public override async Task<MetaInfos> GetMetaInfos(string moduleID) {
            if (moduleID == null) throw new ArgumentNullException(nameof(moduleID));
            var request = MakeSessionRequest<GetMetaInfosReq>();
            request.ModuleID = moduleID;
            return await Post<MetaInfos>(request);
        }

        public override async Task<ObjectInfos> GetObjectsByID(ObjectRef[] objectIDs, bool ignoreMissing) {
            if (objectIDs == null) throw new ArgumentNullException(nameof(objectIDs));
            var request = MakeSessionRequest<GetObjectsByIDReq>();
            request.ObjectIDs = objectIDs;
            request.IgnoreMissing = ignoreMissing;
            return await Post<ObjectInfos>(request);
        }

        public override async Task<ObjectValues> GetObjectValuesByID(ObjectRef[] objectIDs, bool ignoreMissing) {
            if (objectIDs == null) throw new ArgumentNullException(nameof(objectIDs));
            var request = MakeSessionRequest<GetObjectValuesByIDReq>();
            request.ObjectIDs = objectIDs;
            request.IgnoreMissing = ignoreMissing;
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
            await PostVoid(request);
        }

        public override async Task ResetAllVariablesOfObjectTree(ObjectRef objectID) {
            var request = MakeSessionRequest<ResetAllVariablesOfObjectTreeReq>();
            request.ObjectID = objectID;
            await PostVoid(request);
        }

        public override async Task HistorianDeleteVariables(params VariableRef[] variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<HistorianDeleteVariablesReq>();
            request.Variables = variables;
            await PostVoid(request);
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
            if (data == null) throw new ArgumentNullException(nameof(data));
            var request = MakeSessionRequest<HistorianModifyReq>();
            request.Variable = variable;
            request.Data = data;
            request.Mode = mode;
            await PostVoid(request);
        }

        public override async Task<VTTQs> HistorianReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter = QualityFilter.ExcludeNone) {
            var request = MakeSessionRequest<HistorianReadRawReq>();
            request.Variable = variable;
            request.StartInclusive = startInclusive;
            request.EndInclusive = endInclusive;
            request.MaxValues = maxValues;
            request.Bounding = bounding;
            request.Filter = filter;
            return await Post<VTTQs>(request, binaryDeserializer: BinSeri.VTTQ_Serializer.Deserialize);
        }

        public override async Task<VTQs> HistorianReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter rawFilter = QualityFilter.ExcludeNone) {
            var request = MakeSessionRequest<HistorianReadAggregatedIntervalsReq>();
            request.Variable = variable;
            request.IntervalBounds = intervalBounds;
            request.Aggregation = aggregation;
            request.Filter = rawFilter;
            return await Post<VTQs>(request, binaryDeserializer: BinSeri.VTQ_Serializer.Deserialize);
        }

        public override async Task<VariableValues> ReadAllVariablesOfObjectTree(ObjectRef objectID) {
            var request = MakeSessionRequest<ReadAllVariablesOfObjectTreeReq>();
            request.ObjectID = objectID;
            return await Post<VariableValues>(request, binaryDeserializer: BinSeri.VariableValue_Serializer.Deserialize);
        }

        public override async Task<VTQs> ReadVariables(VariableRefs variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesReq>();
            request.Variables = variables;
            return await Post<VTQs>(request, binaryDeserializer: BinSeri.VTQ_Serializer.Deserialize);
        }

        public override async Task<VariableValues> ReadVariablesIgnoreMissing(VariableRefs variables) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesIgnoreMissingReq>();
            request.Variables = variables;
            return await Post<VariableValues>(request, binaryDeserializer: BinSeri.VariableValue_Serializer.Deserialize);
        }

        public override async Task<VTQs> ReadVariablesSync(VariableRefs variables, Duration? timeout = null) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesSyncReq>();
            request.Variables = variables;
            request.Timeout = timeout;
            Task<VTQs> task = Post<VTQs>(request, binaryDeserializer: BinSeri.VTQ_Serializer.Deserialize);
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

        public override async Task<VariableValues> ReadVariablesSyncIgnoreMissing(VariableRefs variables, Duration? timeout = null) {
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            var request = MakeSessionRequest<ReadVariablesSyncIgnoreMissingReq>();
            request.Variables = variables;
            request.Timeout = timeout;

            Task<VariableValues> task = Post<VariableValues>(request, binaryDeserializer: BinSeri.VariableValue_Serializer.Deserialize);
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

        public override async Task<bool[]> CanUpdateConfig(MemberRef[] members) {
            var request = MakeSessionRequest<CanUpdateConfigReq>();
            request.Members = members;
            return await Post<bool[]>(request);
        }

        public override async Task UpdateConfig(ObjectValue[]? updateOrDeleteObjects, MemberValue[]? updateOrDeleteMembers, AddArrayElement[]? addArrayElements) {
            var request = MakeSessionRequest<UpdateConfigReq>();
            request.UpdateOrDeleteObjects = updateOrDeleteObjects ?? new ObjectValue[0];
            request.UpdateOrDeleteMembers = updateOrDeleteMembers ?? new MemberValue[0];
            request.AddArrayElements = addArrayElements ?? new AddArrayElement[0];
            await PostVoid(request);
        }

        public override Task WriteVariables(VariableValues values) {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var request = MakeSessionRequest<WriteVariablesReq>();
            request.Values = values;
            return PostVoid(request);
        }

        public override Task<WriteResult> WriteVariablesIgnoreMissing(VariableValues values) {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var request = MakeSessionRequest<WriteVariablesIgnoreMissingReq>();
            request.Values = values;
            return Post<WriteResult>(request);
        }

        public override async Task<WriteResult> WriteVariablesSync(VariableValues values, Duration? timeout = null) {
            if (values == null) throw new ArgumentNullException(nameof(values));
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

        public override async Task<WriteResult> WriteVariablesSyncIgnoreMissing(VariableValues values, Duration? timeout = null) {
            if (values == null) throw new ArgumentNullException(nameof(values));
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
            request.Parameters = parameters ?? new NamedValue[0];
            return Post<DataValue>(request);
        }

        public override Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {
            var request = MakeSessionRequest<BrowseObjectMemberValuesReq>();
            request.Member = member;
            request.ContinueID = continueID;
            return Post<BrowseResult>(request);
        }

        #endregion

        protected async Task PostVoid(RequestBase obj, bool ignoreError = false) {

            if (inProc != null) {
                await inProc.AddRequest(obj);
            }
            else {
                await PostInternal<bool>(obj, binaryDeserializer: null, expectReturn: false, ignoreError: ignoreError);
            }
        }

        protected async Task<T> Post<T>(RequestBase obj, Func<Stream, T>? binaryDeserializer = null) {

            if (inProc != null) {
                return (T)(await inProc.AddRequest(obj))!;
            }
            else {
                return await PostInternal(obj, binaryDeserializer, expectReturn: true);
            }
        }

        protected async Task<T> PostInternal<T>(RequestBase obj, Func<Stream, T>? binaryDeserializer, bool expectReturn, bool ignoreError = false) {

            MediaTypeHeaderValue contentType;
            string path = obj.GetPath();
            var requestStream = MemoryManager.GetMemoryStream(path);
            try {

                if (obj is BinSerializable bin && MapBinaryMessages[obj.GetID()]) {
                    using (var writer = new BinaryWriter(requestStream, Encoding.UTF8, leaveOpen: true)) {
                        bin.BinSerialize(writer, binaryVersion);
                    }
                    contentType = mediaBinary;
                }
                else {
                    StdJson.ObjectToStream(obj, requestStream);
                    contentType = mediaJSON;
                }
                requestStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception) {
                requestStream.Dispose();
                throw;
            }

            HttpResponseMessage response;

            using (var payload = new StreamContent(requestStream)) {
                payload.Headers.ContentType = contentType;
                try {
                    response = await PostAsync(path, payload, acceptBinary: binaryDeserializer != null);
                }
                catch (TaskCanceledException exp) {
                    OnConnectionBroken($"Post<T> {path} client.PostAsync TaskCanceled", path, exp);
                    throw new ConnectivityException("Time out");
                }
                catch (Exception exp) {
                    OnConnectionBroken($"Post<T> {path} client.PostAsync", path, exp);
                    throw new ConnectivityException(exp.Message);
                }
            }

            using (response) {

                if (response.IsSuccessStatusCode) {

                    if (expectReturn) {
                        try {
                            using (Stream stream = await response.Content.ReadAsStreamAsync()) {
                                if (binaryDeserializer != null && IsBinaryContentType(response.Content)) {
                                    return binaryDeserializer(stream);
                                }
                                else {
                                    using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                                        return StdJson.ObjectFromReader<T>(reader) ?? throw new Exception("Unexpected null return value");
                                    }
                                }
                            }
                        }
                        catch (TaskCanceledException exp) {
                            OnConnectionBroken($"Post<T> {path} response.Content.ReadAsStreamAsync TaskCanceled", path, exp);
                            throw new ConnectivityException("Time out");
                        }
                        catch (Exception exp) {
                            OnConnectionBroken($"Post<T> {path} response.Content.ReadAsStreamAsync", path, exp);
                            throw new ConnectivityException(exp.Message);
                        }
                    }
                    else {
                        return default!;
                    }
                }
                else {

                    if (!ignoreError) {
                        await ThrowError(response, $"Post<T> {path}", path);
                    }
                    return default!; // never come here
                }
            }
        }

        private Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, bool acceptBinary) {
            HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, new Uri(requestUri, UriKind.Relative));
            msg.Content = content;
            msg.Headers.Accept.Add(mediaJSON);
            if (acceptBinary) {
                msg.Headers.Accept.Add(mediaBinary);
            }
            return client.SendAsync(msg);
        }

        private static bool IsBinaryContentType(HttpContent content) {
            return content.Headers.ContentType.MediaType == "application/octet-stream";
        }

        protected async Task ThrowError(HttpResponseMessage response, string context, string path) {

            string? content = null;
            try {
                content = await response.Content.ReadAsStringAsync();
            }
            catch (Exception) {}

            ErrorResult? errObj = null;
            try {
                errObj = StdJson.ObjectFromString<ErrorResult>(content);
            }
            catch (Exception) {}

            if (errObj == null || errObj.Error == null) {
                string errMsg = content == null || string.IsNullOrWhiteSpace(content) ? response.StatusCode.ToString() : content;
                OnConnectionBroken($"ThrowError {context} '{errMsg}'", path, null);
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
            t.Session = session ?? "";
            return t;
        }

        public class EventManager
        {
            protected readonly EventListener listener;
            private byte dataVersion;

            protected CancellationTokenSource? webSocketCancel;
            protected ClientWebSocket? webSocket;

            public EventManager(EventListener listener, byte dataVersion) {
                this.listener = listener;
                this.dataVersion = dataVersion;
            }

            public async Task StartWebSocket(string session, Uri wsUri, Action<string, string, Exception?> notifyConnectionBroken) {

                webSocketCancel = new CancellationTokenSource();
                webSocket = new ClientWebSocket();
                webSocket.Options.KeepAliveInterval = TimeSpan.FromMilliseconds(0);

                await webSocket.ConnectAsync(wsUri, CancellationToken.None);

                var sendBuffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(session));
                await webSocket.SendAsync(sendBuffer, WebSocketMessageType.Text, endOfMessage: true, cancellationToken: CancellationToken.None);

                _ = ReadWebSocketForEvents(webSocketCancel.Token, notifyConnectionBroken);
            }

            protected async Task ReadWebSocketForEvents(CancellationToken cancelToken, Action<string, string, Exception?> notifyConnectionBroken) {

                ClientWebSocket webSocket = this.webSocket!;

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
                                notifyConnectionBroken("ReadWebSocketForEvents ReceiveAsync", "", exp);
                            }
                            _ = listener.OnConnectionClosed();
                            return;
                        }
                        stream.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (!result.EndOfMessage);

                    stream.Seek(0, SeekOrigin.Begin);

                    var msgType = result.MessageType;

                    if (msgType == WebSocketMessageType.Binary || msgType == WebSocketMessageType.Text) {

                        EventContent? eventObj;

                        if (dataVersion == 1) {
                            eventObj = ReadBinaryEventContent(stream);
                        }
                        else {
                            eventObj = ObjectFromJsonStream<EventContent>(stream);
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
                                notifyConnectionBroken("ReadWebSocketForEvents SendAsync(ok)", "", exp);
                            }
                            _ = listener.OnConnectionClosed();
                            return;
                        }
                    }
                    else if (result.MessageType == WebSocketMessageType.Close) {
                        await CloseSocket();
                        if (!cancelToken.IsCancellationRequested) {
                            notifyConnectionBroken($"ReadWebSocketForEvents Close message received '{result.CloseStatusDescription}'", "", null);
                        }
                        await listener.OnConnectionClosed();
                        return;
                    }
                }
            }

            private T? ObjectFromJsonStream<T>(Stream stream) {
                using (var reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true)) {
                    return StdJson.ObjectFromReader<T>(reader);
                }
            }

            private async Task CloseSocket() {
                try {
                    await webSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
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

            protected Task DispatchEvent(EventContent? theEvent) {

                if (theEvent == null) return Task.FromResult(true); ;

                switch (theEvent.Event) {

                    case EventType.OnVariableValueChanged: {
                            List<VariableValue> variables = theEvent.Variables ?? new VariableValues();
                            return listener.OnVariableValueChanged(variables);
                        }

                    case EventType.OnVariableHistoryChanged: {
                            List<HistoryChange> variables = theEvent.Changes ?? new List<HistoryChange>();
                            return listener.OnVariableHistoryChanged(variables);
                        }

                    case EventType.OnConfigChanged: {
                            List<ObjectRef> changes = theEvent.ChangedObjects ?? new List<ObjectRef>();
                            return listener.OnConfigChanged(changes);
                        }

                    case EventType.OnAlarmOrEvent: {
                            List<AlarmOrEvent> alarmOrEvents = theEvent.Events ?? new List<AlarmOrEvent>();
                            return listener.OnAlarmOrEvents(alarmOrEvents);
                        }

                    case EventType.OnPing: {
                            return Task.FromResult(true);
                        }

                    default:
                        Console.Error.WriteLine("Unknown event: " + theEvent.Event);
                        return Task.FromResult(true);
                }
            }

            private EventContent? ReadBinaryEventContent(Stream stream) {

                int eventFormatVersion = stream.ReadByte();
                if (eventFormatVersion != 1) {
                    Console.Error.WriteLine($"ReadBinaryEventContent: Invalid event format version: {eventFormatVersion}");
                    return null;
                }
                int binVer = stream.ReadByte();
                if (binVer < 1 || binVer > Common.CurrentBinaryVersion) {
                    Console.Error.WriteLine($"ReadBinaryEventContent: Invalid binary version: {binVer}");
                    return null;
                }
                int eventID = stream.ReadByte();
                if (eventID < 0 || eventID > (int)EventType.OnPing) {
                    Console.Error.WriteLine($"ReadBinaryEventContent: Invalid event id: {eventID}");
                    return null;
                }
                EventType what = (EventType)eventID;

                // Console.WriteLine($"Binary Event {what}");

                switch (what) {

                    case EventType.OnVariableValueChanged:
                        return new EventContent() {
                            Event = what,
                            Variables = VariableValue_Serializer.Deserialize(stream),
                        };

                    case EventType.OnVariableHistoryChanged:
                        return new EventContent() {
                            Event = what,
                            Changes = ObjectFromJsonStream<List<HistoryChange>>(stream),
                        };

                    case EventType.OnConfigChanged:
                        return new EventContent() {
                            Event = what,
                            ChangedObjects = ObjectFromJsonStream<List<ObjectRef>>(stream),
                        };

                    case EventType.OnAlarmOrEvent:
                        return new EventContent() {
                            Event = what,
                            Events = ObjectFromJsonStream<List<AlarmOrEvent>>(stream),
                        };

                    case EventType.OnPing:
                        return new EventContent() {
                            Event = what,
                        };

                    default:
                        return null;
                }
            }
        }
    }
}
