// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;
using ModuleInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ModuleInfo>;
using LocationInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.LocationInfo>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;
using ObjectValues = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectValue>;
using MemberValues = System.Collections.Generic.List<Ifak.Fast.Mediator.MemberValue>;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using Ifak.Fast.Mediator.BinSeri;

namespace Ifak.Fast.Mediator
{
    public class HandleClientRequests : InProcApi
    {
        private static Logger logger = LogManager.GetLogger("HandleClientRequests");

        private bool terminating = false;

        public void setTerminating() {
            terminating = true;
            foreach (SessionInfo session in sessions.Values) {
                session.terminating = true;
            }
        }

        public const string PathPrefix = "/Mediator/";

        public static bool IsLogout(string path) => path == RequestDefinitions.Logout.HttpPath;

        private readonly MediatorCore core;

        public HandleClientRequests(MediatorCore core) {
            this.core = core;
        }

        public void Start() {
            _ = PurgeExpiredSessions();
            _ = ProcessReqLoop();
        }

        private async Task PurgeExpiredSessions() {

            while (!terminating) {

                await Task.Delay(TimeSpan.FromMinutes(5));

                bool needPurge = sessions.Values.Any(session => session.IsExpired);
                if (needPurge) {
                    var sessionItems = sessions.Values.Where(session => session.IsExpired).ToList();
                    foreach (var session in sessionItems) {
                        logger.Info($"Terminating expired session: {session.ID}. Origin: {session.Origin}, Start: {session.StartTime}, Last activity: {session.LastActivity}");
                        TerminateSession(session, "Session expired");
                    }
                }
            }
        }

        private readonly Dictionary<string, SessionInfo> sessions = new Dictionary<string, SessionInfo>();

        public async Task HandleNewWebSocketSession(string session, WebSocket socket) {

            if (!sessions.ContainsKey(session)) {
                logger.Warn("Invalid session id in websocket request: " + session);
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid session id", CancellationToken.None);
                return;
            }

            SessionInfo info = sessions[session];
            if (!info.Valid) {
                logger.Warn("Unauthenticated session id in websocket request");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Unauthenticated session id", CancellationToken.None);
                return;
            }

            if (info.HasEventSocket) {
                logger.Warn("A websocket is already assigned to this session");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "A websocket is already assigned to this session", CancellationToken.None);
                return;
            }
            var tcs = new TaskCompletionSource<object?>();
            info.SetEventSocket(socket, tcs);
            await tcs.Task;
        }

        public async Task<ReqResult> Handle(RequestBase request, bool startingUp) {

            int id = request.GetID();

            switch (id) {

                case LoginReq.ID: {

                        var req = (LoginReq)request;
                        string password = "";
                        string moduleID = req.ModuleID;
                        bool isModuleSession = !string.IsNullOrEmpty(moduleID);

                        var origin = new Origin();

                        if (isModuleSession) {

                            ModuleState module = core.modules.FirstOrDefault(m => m.ID == moduleID);
                            if (module == null) {
                                return Result_BAD($"Invalid login (unknown module id {moduleID})");
                            }
                            origin.Type = OriginType.Module;
                            origin.ID = moduleID!;
                            origin.Name = module.Name;
                            password = module.Password;
                        }
                        else {

                            if (startingUp) {
                                return Result_BAD("Mediator start up not completed yet.");
                            }

                            string login = req.Login ?? "";
                            string[] roles = req.Roles ?? new string[0];

                            User user = core.userManagement.Users.FirstOrDefault(u => u.Login == login);

                            if (user == null || user.Inactive) {
                                return Result_BAD("Invalid login: " + login);
                            }

                            string[] invalidRoles = roles.Where(r => user.Roles.All(rr => rr != r)).ToArray();

                            if (invalidRoles.Length > 0) {
                                return Result_BAD("Invalid roles: " + string.Join(", ", invalidRoles));
                            }

                            if (string.IsNullOrEmpty(user.EncryptedPassword)) {
                                return Result_BAD("EncryptedPassword may not be empty!");
                            }

                            origin.Type = OriginType.User;
                            origin.ID = user.ID;
                            origin.Name = user.Name;
                            password = SimpleEncryption.Decrypt(user.EncryptedPassword);
                        }

                        string session = Guid.NewGuid().ToString();
                        string challenge = Guid.NewGuid().ToString();

                        var response = new LoginResponse() {
                            Session = session,
                            Challenge = challenge,
                            MediatorVersion = VersionInfo.ifakFAST_Str(),
                            BinaryVersion = BinSeri.Common.CurrentBinaryVersion,
                            BinMethods = RequestDefinitions.Definitions.Where(x => x.IsBinSerializable).Select(x => x.NumericID).ToArray(),
                            EventDataVersion = HttpConnection.CurrentEventDataFormatVersion,
                        };

                        sessions[session] = new SessionInfo(session, challenge, origin, password, TerminateSession);
                        return Result_OK(response);
                    }

                case AuthenticateReq.ID: {

                        var req = (AuthenticateReq)request;
                        string session = req.Session ?? "";
                        if (!sessions.ContainsKey(session)) {
                            return Result_BAD("Invalid session");
                        }

                        SessionInfo info = sessions[session];
                        long hash = req.Hash;
                        string password = info.Password;
                        long myHash = ClientDefs.strHash(password + info.Challenge + password + session);
                        if (hash != myHash) {
                            sessions.Remove(session);
                            return Result_BAD("Invalid password");
                        }

                        if (req.SelectedBinaryVersion < 0 || req.SelectedBinaryVersion > BinSeri.Common.CurrentBinaryVersion) {
                            sessions.Remove(session);
                            return Result_BAD("Invalid binary version");
                        }

                        if (req.SelectedEventDataVersion < 0 || req.SelectedEventDataVersion > HttpConnection.CurrentEventDataFormatVersion) {
                            sessions.Remove(session);
                            return Result_BAD("Invalid event data version");
                        }

                        info.Valid = true;
                        info.UpdateLastActivity();
                        info.BinaryVersion = req.SelectedBinaryVersion;
                        info.EventDataVersion = req.SelectedEventDataVersion;

                        var response = new AuthenticateResponse() {
                            Session = session,
                        };

                        // logger.Info($"New session: {session} Origin: {info.Origin}");
                        return Result_OK(response);
                    }


                default:
                    return await HandleRegular(request);
            }
        }

        private async Task<ReqResult> HandleRegular(RequestBase request) {

            string session = request.Session ?? "";
            if (!sessions.ContainsKey(session)) {
                string msg = $"Aborting request {request.GetPath()} because of invalid or expired session: {session}";
                logger.Info(msg);
                return Result_ConnectivityErr(msg);
            }

            SessionInfo info = sessions[session];
            if (!info.Valid) return Result_ConnectivityErr("Invalid session");
            info.UpdateLastActivity();

            if (info.BinaryVersion < 1) {
                request.ReturnBinaryResponse = false;
            }

            try {

                int numID = request.GetID();

                switch (numID) {

                    case ReadVariablesReq.ID: {
                            var req = (ReadVariablesReq)request;
                            VTQs res = DoReadVariables(req.Variables);

                            Action<object, Stream>? serializer = null;
                            if (req.ReturnBinaryResponse) {
                                serializer = (obj, stream) => VTQ_Serializer.Serialize(stream, (VTQs)obj, info.BinaryVersion);
                            }

                            return Result_OK(res, serializer);
                        }

                    case ReadVariablesIgnoreMissingReq.ID: {
                            var req = (ReadVariablesIgnoreMissingReq)request;
                            VariableValues vvs = DoReadVariablesIgnoreMissing(req.Variables);

                            Action<object, Stream>? serializer = null;
                            if (req.ReturnBinaryResponse) {
                                serializer = (obj, stream) => VariableValue_Serializer.Serialize(stream, (VariableValues)obj, info.BinaryVersion);
                            }

                            return Result_OK(vvs, serializer);
                        }

                    case ReadVariablesSyncReq.ID: {
                            var req = (ReadVariablesSyncReq)request;
                            VariableValues vvs = await DoReadVariablesSync(req.Variables, req.Timeout, info, ignoreMissing: false);

                            int N = vvs.Count;
                            VTQs res = new VTQs(N);
                            for (int i = 0; i < N; ++i) {
                                res.Add(vvs[i].Value);
                            }

                            Action<object, Stream>? serializer = null;
                            if (req.ReturnBinaryResponse) {
                                serializer = (obj, stream) => VTQ_Serializer.Serialize(stream, (VTQs)obj, info.BinaryVersion);
                            }

                            return Result_OK(res, serializer);
                        }

                    case ReadVariablesSyncIgnoreMissingReq.ID: {
                            var req = (ReadVariablesSyncIgnoreMissingReq)request;
                            VariableValues vvs = await DoReadVariablesSync(req.Variables, req.Timeout, info, ignoreMissing: true);

                            Action<object, Stream>? serializer = null;
                            if (req.ReturnBinaryResponse) {
                                serializer = (obj, stream) => VariableValue_Serializer.Serialize(stream, (VariableValues)obj, info.BinaryVersion);
                            }

                            return Result_OK(vvs, serializer);
                        }

                    case WriteVariablesReq.ID: {
                            var req = (WriteVariablesReq)request;
                            await DoWriteVariables(req.Values, info, ignoreMissing: false);
                            return Result_OK();
                        }

                    case WriteVariablesIgnoreMissingReq.ID: {
                            var req = (WriteVariablesIgnoreMissingReq)request;
                            WriteResult res = await DoWriteVariables(req.Values, info, ignoreMissing: true);
                            return Result_OK(res);
                        }

                    case WriteVariablesSyncReq.ID: {
                            var req = (WriteVariablesSyncReq)request;
                            return await DoWriteVariablesSync(req.Values, req.Timeout, info, ignoreMissing: false);
                        }

                    case WriteVariablesSyncIgnoreMissingReq.ID: {
                            var req = (WriteVariablesSyncIgnoreMissingReq)request;
                            return await DoWriteVariablesSync(req.Values, req.Timeout, info, ignoreMissing: true);
                        }

                    case ReadAllVariablesOfObjectTreeReq.ID: {
                            var req = (ReadAllVariablesOfObjectTreeReq)request;
                            ObjectRef obj = req.ObjectID;
                            string mod = obj.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(mod);

                            ObjectInfo? objInfo = module.GetObjectInfo(obj);
                            if (objInfo == null) throw new Exception($"Invalid object ref: {obj}");
                            ObjectInfos allObj = module.AllObjects;
                            HashSet<ObjectRef> objsWithChildren = module.ObjectsWithChildren;
                            var varRefs = new List<VariableRef>();
                            GetAllVarRefsOfObjTree(allObj, objInfo, objsWithChildren, varRefs);
                            VariableValues result = new VariableValues(varRefs.Count);
                            for (int i = 0; i < varRefs.Count; ++i) {
                                VariableRef vr = varRefs[i];
                                VTQ vtq = module.GetVarValue(vr);
                                result.Add(VariableValue.Make(vr, vtq));
                            }

                            Action<object, Stream>? serializer = null;
                            if (req.ReturnBinaryResponse) {
                                serializer = (obj, stream) => VariableValue_Serializer.Serialize(stream, (VariableValues)obj, info.BinaryVersion);
                            }

                            return Result_OK(result, serializer);
                        }

                    case GetModulesReq.ID: {

                            Func<ModuleState, bool> hasNumericVariables = (m) => {
                                return m.AllObjects.Any(obj => obj.Variables != null && obj.Variables.Any(v => v.IsNumeric || v.Type == DataType.Bool));
                            };

                            ModuleInfos res = core.modules.Select(m => new ModuleInfo() {
                                ID = m.ID,
                                Name = m.Name,
                                Enabled = m.Enabled,
                                HasNumericVariables = hasNumericVariables(m),
                            }).ToList();

                            return Result_OK(res);
                        }

                    case GetLocationsReq.ID: {

                            LocationInfos res = core.locations.Select(m => new LocationInfo() {
                                ID = m.ID,
                                Name = m.Name,
                                LongName = m.LongName,
                                Parent = m.Parent,
                                Config = m.Config
                            }).ToList();

                            return Result_OK(res);
                        }

                    case GetLoginUserReq.ID: {

                            string id = info.Origin.ID;
                            if (id == null || info.Origin.Type != OriginType.User) {
                                return Result_BAD("Not a user login");
                            }
                            User user = core.userManagement.Users.FirstOrDefault(u => u.ID == id);
                            if (user == null) {
                                return Result_BAD("Current user not found");
                            }
                            return Result_OK(user);
                        }

                    case GetRootObjectReq.ID: {
                            var req = (GetRootObjectReq)request;
                            string moduleID = req.ModuleID ?? throw new Exception("Missing moduleID");
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            var res = module.AllObjects.FirstOrDefault(obj => !obj.Parent.HasValue);
                            return Result_OK(res);
                        }

                    case GetAllObjectsReq.ID: {
                            var req = (GetAllObjectsReq)request;
                            string moduleID = req.ModuleID ?? throw new Exception("Missing moduleID");
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            ObjectInfos res = module.AllObjects;
                            return Result_OK(res);
                        }

                    case GetAllObjectsOfTypeReq.ID: {
                            var req = (GetAllObjectsOfTypeReq)request;
                            string moduleID = req.ModuleID ?? throw new Exception("Missing moduleID");
                            string className = req.ClassName ?? "";
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            ObjectInfos res = module.AllObjects.Where(x => x.ClassName == className).ToList();
                            return Result_OK(res);
                        }

                    case GetObjectsByIDReq.ID: {
                            var req = (GetObjectsByIDReq)request;
                            ObjectRef[] objectIDs = req.ObjectIDs ?? new ObjectRef[0];
                            ObjectInfos result = objectIDs.Select(id => {
                                ModuleState module = ModuleFromIdOrThrow(id.ModuleID);
                                foreach (ObjectInfo inf in module.AllObjects) {
                                    if (inf.ID == id) {
                                        return inf;
                                    }
                                }
                                throw new Exception("No object found with id " + id.ToString());
                            }).ToList();
                            return Result_OK(result);
                        }

                    case GetChildrenOfObjectsReq.ID: {
                            var req = (GetChildrenOfObjectsReq)request;
                            ObjectRef[] objectIDs = req.ObjectIDs ?? new ObjectRef[0];
                            ObjectInfos result = objectIDs.SelectMany(id => {
                                ModuleState module = ModuleFromIdOrThrow(id.ModuleID);
                                if (module.AllObjects.All(x => x.ID != id)) throw new Exception("No object found with id " + id.ToString());
                                return module.AllObjects.Where(x => x.Parent.HasValue && x.Parent.Value.Object == id).ToArray();
                            }).ToList();
                            return Result_OK(result);
                        }

                    case GetAllObjectsWithVariablesOfTypeReq.ID: {
                            var req = (GetAllObjectsWithVariablesOfTypeReq)request;
                            string moduleID = req.ModuleID ?? throw new Exception("Missing moduleID");
                            DataType[] types = req.Types ?? new DataType[0];
                            Func<Variable, bool> varHasType = (variable) => {
                                DataType type = variable.Type;
                                return types.Any(t => t == type);
                            };
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            ObjectInfos res = module.AllObjects
                                .Where(x => x.Variables != null && x.Variables.Any(varHasType))
                                .ToList();
                            return Result_OK(res);
                        }

                    case GetObjectValuesByIDReq.ID: {
                            var req = (GetObjectValuesByIDReq)request;
                            ObjectRef[] objectIDs = req.ObjectIDs ?? new ObjectRef[0];

                            if (objectIDs.Length <= 1 || objectIDs.All(o => o.ModuleID == objectIDs[0].ModuleID)) {
                                ModuleState module = ModuleFromIdOrThrow(objectIDs[0].ModuleID);
                                foreach (ObjectRef id in objectIDs) {
                                    if (module.AllObjects.All(x => x.ID != id)) throw new Exception("No object found with id " + id.ToString());
                                }
                                ObjectValue[] res = await RestartOnExp(module, m => m.GetObjectValuesByID(objectIDs));
                                return Result_OK(res);
                            }

                            Task<ObjectValue[]>[] tasks = objectIDs.Select(id => {
                                ModuleState module = ModuleFromIdOrThrow(id.ModuleID);
                                if (module.AllObjects.All(x => x.ID != id)) throw new Exception("No object found with id " + id.ToString());
                                return RestartOnExp(module, m => m.GetObjectValuesByID(new ObjectRef[] { id }));
                            }).ToArray();

                            ObjectValue[][] allValues = await Task.WhenAll(tasks);
                            ObjectValues allValuesFlat = allValues.SelectMany(x => x).ToList();

                            return Result_OK(allValuesFlat);
                        }

                    case GetMemberValuesReq.ID: {
                            var req = (GetMemberValuesReq)request;
                            MemberRef[] member = req.Member ?? new MemberRef[0];

                            if (member.Length <= 1 || member.All(o => o.Object.ModuleID == member[0].Object.ModuleID)) {
                                ModuleState module = ModuleFromIdOrThrow(member[0].Object.ModuleID);
                                foreach (MemberRef mem in member) {
                                    if (module.AllObjects.All(x => x.ID != mem.Object)) throw new Exception("No object found with id " + mem.Object.ToString());
                                }
                                MemberValue[] res = await RestartOnExp(module, m => m.GetMemberValues(member));
                                return Result_OK(res);
                            }

                            Task<MemberValue[]>[] tasks = member.Select(mem => {
                                ModuleState module = ModuleFromIdOrThrow(mem.Object.ModuleID);
                                if (module.AllObjects.All(x => x.ID != mem.Object)) throw new Exception("No object found with id " + mem.Object.ToString());
                                return RestartOnExp(module, m => m.GetMemberValues(new MemberRef[] { mem }));
                            }).ToArray();

                            MemberValue[][] allValues = await Task.WhenAll(tasks);
                            MemberValues allValuesFlat = allValues.SelectMany(x => x).ToList();

                            return Result_OK(allValuesFlat);
                        }

                    case GetParentOfObjectReq.ID: {
                            var req = (GetParentOfObjectReq)request;
                            ObjectRef objectID = req.ObjectID;

                            ModuleState module = ModuleFromIdOrThrow(objectID.ModuleID);
                            foreach (ObjectInfo o in module.AllObjects) {
                                if (o.ID == objectID) {
                                    if (!o.Parent.HasValue) throw new Exception("Object has no parent: " + objectID);
                                    ObjectRef parent = o.Parent.Value.Object;
                                    ObjectValue[] values = await RestartOnExp(module, m => m.GetObjectValuesByID(new ObjectRef[] { parent }));
                                    if (values == null || values.Length < 1) {
                                        return Result_BAD("No object value for parent " + parent);
                                    }
                                    return Result_OK(values[0]);
                                }
                            }
                            return Result_BAD("No object found with id " + objectID);
                        }

                    case UpdateConfigReq.ID: {

                            var req = (UpdateConfigReq)request;

                            ObjectValue[] updateOrDeleteObjects = req.UpdateOrDeleteObjects ?? new ObjectValue[0];
                            MemberValue[] updateOrDeleteMembers = req.UpdateOrDeleteMembers ?? new MemberValue[0];
                            AddArrayElement[] addArrayElements = req.AddArrayElements ?? new AddArrayElement[0];

                            string[] moduleIDs = updateOrDeleteObjects.Select(x => x.Object.ModuleID)
                                .Concat(updateOrDeleteMembers.Select(x => x.Member.Object.ModuleID))
                                .Concat(addArrayElements.Select(x => x.ArrayMember.Object.ModuleID))
                                .Distinct().ToArray();

                            ModuleState[] modules = moduleIDs.Select(ModuleFromIdOrThrow).ToArray(); // ensure fail fast in case of any invalid module reference

                            Task<Result>[] tasks = moduleIDs.Select(moduleID => {
                                var myObjects = updateOrDeleteObjects.Where(x => x.Object.ModuleID == moduleID).ToArray();
                                var myMembers = updateOrDeleteMembers.Where(x => x.Member.Object.ModuleID == moduleID).ToArray();
                                var myAddArrays = addArrayElements.Where(x => x.ArrayMember.Object.ModuleID == moduleID).ToArray();

                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                return RestartOnExp(module, m => m.UpdateConfig(info.Origin, myObjects, myMembers, myAddArrays));
                            }).ToArray();

                            Result res = Result.FromResults(await Task.WhenAll(tasks));

                            foreach (var m in modules) { // ensure object cache is already updated when UpdateConfig returns
                                m.SetAllObjects(await m.Instance.GetAllObjects());
                            }

                            if (res.IsOK) return Result_OK();
                            return Result_BAD(res.Error ?? "UpdateConfig failed");
                        }

                    case EnableVariableValueChangedEventsReq.ID: {

                            var req = (EnableVariableValueChangedEventsReq)request;

                            SubOptions options = req.Options;
                            VariableRef[] variables = req.Variables ?? new VariableRef[0];
                            ObjectRef[] idsOfEnabledTreeRoots = req.IdsOfEnabledTreeRoots ?? new ObjectRef[0];

                            foreach (ObjectRef obj in idsOfEnabledTreeRoots) {
                                info.VariablesChangedEventTrees[obj] = options;
                            }

                            foreach (VariableRef vr in variables) {
                                info.VariableRefs[vr] = options;
                            }

                            return Result_OK();
                        }

                    case EnableVariableHistoryChangedEventsReq.ID: {

                            var req = (EnableVariableHistoryChangedEventsReq)request;

                            VariableRef[] variables = req.Variables ?? new VariableRef[0];
                            ObjectRef[] idsOfEnabledTreeRoots = req.IdsOfEnabledTreeRoots ?? new ObjectRef[0];

                            foreach (ObjectRef obj in idsOfEnabledTreeRoots) {
                                info.VariablesHistoryChangedEventTrees.Add(obj);
                            }

                            foreach (VariableRef vr in variables) {
                                info.VariableHistoryRefs.Add(vr);
                            }

                            return Result_OK();
                        }

                    case EnableConfigChangedEventsReq.ID: {

                            var req = (EnableConfigChangedEventsReq)request;

                            ObjectRef[] objects = req.Objects ?? new ObjectRef[0];

                            foreach (ObjectRef obj in objects) {
                                info.ConfigChangeObjects.Add(obj);
                            }

                            return Result_OK();
                        }

                    case DisableChangeEventsReq.ID: {

                            var req = (DisableChangeEventsReq)request;

                            bool disableVarValueChanges = req.DisableVarValueChanges;
                            bool disableVarHistoryChanges = req.DisableVarHistoryChanges;
                            bool disableConfigChanges = req.DisableConfigChanges;

                            if (disableVarValueChanges) {
                                info.VariablesChangedEventTrees.Clear();
                                info.VariableRefs.Clear();
                            }

                            if (disableVarHistoryChanges) {
                                info.VariablesHistoryChangedEventTrees.Clear();
                                info.VariableHistoryRefs.Clear();
                            }

                            if (disableConfigChanges) {
                                info.ConfigChangeObjects.Clear();
                            }

                            return Result_OK();
                        }

                    case EnableAlarmsAndEventsReq.ID: {

                            var req = (EnableAlarmsAndEventsReq)request;

                            Severity minSeverity = req.MinSeverity;
                            info.AlarmsAndEventsEnabled = true;
                            info.MinSeverity = minSeverity;
                            return Result_OK();
                        }

                    case DisableAlarmsAndEventsReq.ID: {
                            info.AlarmsAndEventsEnabled = false;
                            return Result_OK();
                        }

                    case HistorianReadRawReq.ID: {

                            var req = (HistorianReadRawReq)request;

                            VariableRef variable = req.Variable;
                            Timestamp tStart = req.StartInclusive;
                            Timestamp tEnd = req.EndInclusive;
                            int maxValues = req.MaxValues;
                            BoundingMethod bounding = req.Bounding;
                            QualityFilter filter = req.Filter;

                            VTTQs vttqs = await core.history.HistorianReadRaw(variable, tStart, tEnd, maxValues, bounding, filter);

                            Action<object, Stream>? serializer = null;
                            if (req.ReturnBinaryResponse) {
                                serializer = (obj, stream) => VTTQ_Serializer.Serialize(stream, (VTTQs)obj, info.BinaryVersion);
                            }

                            return Result_OK(vttqs, serializer);
                        }

                    case HistorianCountReq.ID: {

                            var req = (HistorianCountReq)request;

                            VariableRef variable = req.Variable;
                            Timestamp tStart = req.StartInclusive;
                            Timestamp tEnd = req.EndInclusive;
                            QualityFilter filter = req.Filter;

                            long count = await core.history.HistorianCount(variable, tStart, tEnd, filter);
                            return Result_OK(count);
                        }
                    case HistorianDeleteIntervalReq.ID: {

                            var req = (HistorianDeleteIntervalReq)request;

                            VariableRef variable = req.Variable;
                            Timestamp tStart = req.StartInclusive;
                            Timestamp tEnd = req.EndInclusive;

                            long count = await core.history.HistorianDeleteInterval(variable, tStart, tEnd);
                            return Result_OK(count);
                        }

                    case HistorianModifyReq.ID: {

                            var req = (HistorianModifyReq)request;

                            VariableRef variable = req.Variable;
                            VTQ[] data = req.Data ?? new VTQ[0];
                            ModifyMode mode = req.Mode;
                            await core.history.HistorianModify(variable, data, mode);
                            return Result_OK();
                        }

                    case HistorianDeleteAllVariablesOfObjectTreeReq.ID: {

                            var req = (HistorianDeleteAllVariablesOfObjectTreeReq)request;

                            ObjectRef obj = req.ObjectID;
                            string mod = obj.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(mod);
                            ObjectInfo? objInfo = module.GetObjectInfo(obj);
                            if (objInfo == null) throw new Exception($"Invalid object ref: {obj}");
                            ObjectInfos allObj = module.AllObjects;
                            HashSet<ObjectRef> objsWithChildren = module.ObjectsWithChildren;
                            var varRefs = new List<VariableRef>();
                            GetAllVarRefsOfObjTree(allObj, objInfo, objsWithChildren, varRefs);
                            await core.history.DeleteVariables(varRefs);
                            return Result_OK();
                        }

                    case HistorianDeleteVariablesReq.ID: {

                            var req = (HistorianDeleteVariablesReq)request;

                            VariableRef[] variables = req.Variables ?? new VariableRef[0];
                            await core.history.DeleteVariables(variables);
                            return Result_OK();
                        }

                    case HistorianGetLatestTimestampDBReq.ID: {

                            var req = (HistorianGetLatestTimestampDBReq)request;

                            VariableRef variable = req.Variable;
                            Timestamp tStart = req.StartInclusive;
                            Timestamp tEnd = req.EndInclusive;

                            VTTQ? res = await core.history.HistorianGetLatestTimestampDb(variable, tStart, tEnd);
                            return Result_OK(res);
                        }

                    case CallMethodReq.ID: {

                            var req = (CallMethodReq)request;

                            string moduleID   = req.ModuleID;
                            string methodName = req.MethodName;
                            NamedValue[] parameters = req.Parameters ?? new NamedValue[0];

                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            Result<DataValue> res = await RestartOnExp(module, m => m.OnMethodCall(info.Origin, methodName, parameters));

                            if (res.IsOK)
                                return Result_OK(res.Value);
                            else
                                return Result_BAD(res.Error ?? $"MethodCall '{methodName}' failed");
                        }

                    case BrowseObjectMemberValuesReq.ID: {

                            var req = (BrowseObjectMemberValuesReq)request;

                            MemberRef member = req.Member;
                            int? continueID = req.ContinueID;

                            string moduleID = member.Object.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            BrowseResult res = await RestartOnExp(module, m => m.BrowseObjectMemberValues(member, continueID));

                            return Result_OK(res);
                        }

                    case GetMetaInfosReq.ID: {

                            var req = (GetMetaInfosReq)request;

                            string moduleID = req.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            try {
                                MetaInfos meta = await module.Instance.GetMetaInfo();
                                return Result_OK(meta);
                            }
                            catch (Exception exp) {
                                return Result_BAD(exp.Message);
                            }
                        }

                    case PingReq.ID: {
                            return Result_OK();
                        }

                    case EnableEventPingReq.ID: {
                            info.eventPingEnabled = true;
                            return Result_OK();
                        }

                    case LogoutReq.ID: {
                            LogoutSession(info);
                            return Result_OK();
                        }

                    default:
                        return Result_BAD("Invalid request path");
                }
            }
            catch (Exception exp) {
                return Result_BAD(exp.Message);
            }
        }

        private void LogoutSession(SessionInfo session) {
            session.LogoutCompleted = true;
            sessions.Remove(session.ID);
            if (session.HasEventSocket) {
                session.EventSocketTCS?.TrySetResult(null);
                session.SetEventSocket(null, null);
            }
        }

        private void TerminateSession(SessionInfo session, string reason) {
            session.LogoutCompleted = true;
            if (session.HasEventSocket) {
                Task tt = SendClose(session.EventSocket, reason);
                tt.ContinueOnMainThread(t => {
                    session.EventSocketTCS?.TrySetResult(null);
                    session.SetEventSocket(null, null);
                });
            }
            _ = RemoveSessionDelayed(session.ID);
        }

        private async Task SendClose(WebSocket? eventSocket, string reason) {
            try {
                await eventSocket!.CloseAsync(WebSocketCloseStatus.NormalClosure, reason, CancellationToken.None);
            }
            catch (Exception) { }
        }

        private async Task RemoveSessionDelayed(string sessionID) {
            await Task.Delay(5000);
            sessions.Remove(sessionID);
        }

        private async Task<ReqResult> DoWriteVariablesSync(VariableValues? values, Duration? timeout, SessionInfo info, bool ignoreMissing) {

            if (values == null) return Result_OK(WriteResult.OK);

            VariableError[]? ignoredVars = null;
            if (ignoreMissing) {
                VariableValues filteredValues = values.Where(VarExists).ToList();
                if (filteredValues.Count < values.Count) {
                    ignoredVars = values.Where(VarMissing).Select(v => new VariableError(v.Variable, $"Variable {v.Variable.ToString()} does not exist.")).ToArray();
                }
                values = filteredValues;
            }

            var set = new HashSet<string>();
            for (int i = 0; i < values.Count; ++i) {
                set.Add(values[i].Variable.Object.ModuleID);
            }
            string[] moduleIDs = set.ToArray();

            if (!ignoreMissing) {
                foreach (string moduleID in moduleIDs) {
                    ModuleState module = ModuleFromIdOrThrow(moduleID);
                    VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                    module.ValidateVariableValuesOrThrow(moduleValues);
                }
            }
            int maxBufferCount = 0;
            Task<WriteResult>[] tasks = moduleIDs.Select(moduleID => {
                ModuleState module = ModuleFromIdOrThrow(moduleID);
                VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                int count = module.UpdateVariableValues(moduleValues);
                maxBufferCount = Math.Max(maxBufferCount, count);
                VariableValue[] filtereModuleValues = moduleValues.Where(vv => module.GetVarDescription(vv.Variable)?.Writable ?? false).ToArray();
                VariableError[]? writableErrors = null;
                if (filtereModuleValues.Length < moduleValues.Length) {
                    VariableValue[] removedModuleValues = moduleValues.Where(vv => !(module.GetVarDescription(vv.Variable)?.Writable ?? false)).ToArray();
                    moduleValues = filtereModuleValues;
                    writableErrors = removedModuleValues.Select(vv => new VariableError(vv.Variable, $"Variable {vv.Variable.ToString()} is not writable.")).ToArray();
                }
                if (moduleValues.Length > 0) {
                    Task<WriteResult> t = RestartOnExp(module, m => m.WriteVariables(info.Origin, moduleValues, timeout, sync: true));
                    if (writableErrors == null) return t;
                    return t.ContinueOnMainThread((task) => {
                        WriteResult writeResult = task.Result;
                        if (writeResult.IsOK()) {
                            return WriteResult.Failure(writableErrors);
                        }
                        else {
                            var list = writeResult.FailedVariables.ToList();
                            list.AddRange(writableErrors);
                            return WriteResult.Failure(list.ToArray());
                        }
                    });
                }
                else {
                    return Task.FromResult(writableErrors == null ? WriteResult.OK : WriteResult.Failure(writableErrors));
                }
            }).ToArray();

            WriteResult[] res = await Task.WhenAll(tasks);

            if (maxBufferCount > 1000) {
                int waitTime = Math.Min(maxBufferCount - 900, 8000);
                var Now = Timestamp.Now;
                if (Now - timeLastWriteVariablesSyncThrottle > Duration.FromMinutes(1)) {
                    timeLastWriteVariablesSyncThrottle = Now;
                    logger.Info("Throttling WriteVariablesSync because of excessive history DB queue length ({0})", maxBufferCount);
                }
                await Task.Delay(waitTime);
            }

            var writeResult = WriteResult.FromResults(res);
            if (ignoredVars != null) {
                if (writeResult.IsOK()) {
                    writeResult = WriteResult.Failure(ignoredVars);
                }
                else {
                    var list = writeResult.FailedVariables.ToList();
                    list.AddRange(ignoredVars);
                    writeResult = WriteResult.Failure(list.ToArray());
                }
            }
            return Result_OK(writeResult);
        }

        private async Task<WriteResult> DoWriteVariables(VariableValues? values, SessionInfo info, bool ignoreMissing) {
            if (values == null) return WriteResult.OK;

            VariableError[]? ignoredVars = null;
            if (ignoreMissing) {
                VariableValues filteredValues = values.Where(VarExists).ToList();
                if (filteredValues.Count < values.Count) {
                    ignoredVars = values.Where(VarMissing).Select(v => new VariableError(v.Variable, $"Variable {v.Variable} does not exist.")).ToArray();
                }
                values = filteredValues;
            }

            var set = new HashSet<string>();
            for (int i = 0; i < values.Count; ++i) {
                set.Add(values[i].Variable.Object.ModuleID);
            }
            string[] moduleIDs = set.ToArray();

            if (!ignoreMissing) {
                foreach (string moduleID in moduleIDs) {
                    ModuleState module = ModuleFromIdOrThrow(moduleID);
                    VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                    module.ValidateVariableValuesOrThrow(moduleValues);
                }
            }

            int maxBufferCount = 0;
            foreach (string moduleID in moduleIDs) {
                ModuleState module = ModuleFromIdOrThrow(moduleID);
                VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                int count = module.UpdateVariableValues(moduleValues);
                moduleValues = moduleValues.Where(vv => module.GetVarDescription(vv.Variable)?.Writable ?? false).ToArray();
                maxBufferCount = Math.Max(maxBufferCount, count);
                if (moduleValues.Length > 0) {
                    var ignored = RestartOnExp(module, m => m.WriteVariables(info.Origin, moduleValues, null, sync: false));
                }
            }

            if (maxBufferCount > 1000) {
                int waitTime = Math.Min(maxBufferCount - 900, 8000);
                var Now = Timestamp.Now;
                if (Now - timeLastWriteVariablesThrottle > Duration.FromMinutes(1)) {
                    timeLastWriteVariablesThrottle = Now;
                    logger.Info("Throttling WriteVariables because of excessive history DB queue length ({0})", maxBufferCount);
                }
                await Task.Delay(waitTime);
            }

            return ignoredVars == null ? WriteResult.OK : WriteResult.Failure(ignoredVars);
        }

        private async Task<VariableValues> DoReadVariablesSync(List<VariableRef>? variables, Duration? timeout, SessionInfo info, bool ignoreMissing) {

            if (variables == null) return new VariableValues(0);

            if (ignoreMissing && !variables.All(VarExists)) {
                variables = variables.Where(VarExists).ToList();
            }

            var set = new HashSet<string>();
            for (int i = 0; i < variables.Count; ++i) {
                set.Add(variables[i].Object.ModuleID);
            }
            List<string> moduleIDs = set.ToList();

            if (!ignoreMissing) {
                foreach (string moduleID in moduleIDs) {
                    ModuleState module = ModuleFromIdOrThrow(moduleID);
                    VariableRef[] moduleVars = variables.Where(v => v.Object.ModuleID == moduleID).ToArray();
                    module.ValidateVariableRefsOrThrow(moduleVars);
                }
            }

            Task<VTQ[]>[] tasks = moduleIDs.Select(moduleID => {
                ModuleState module = ModuleFromIdOrThrow(moduleID);
                VariableRef[] moduleVars = variables.Where(v => v.Object.ModuleID == moduleID).ToArray();
                if (moduleVars.All(v => module.GetVarDescription(v)!.SyncReadable)) {
                    return RestartOnExp(module, m => m.ReadVariables(info.Origin, moduleVars, timeout));
                }
                else {
                    VTQ[] vtqRes = new VTQ[moduleVars.Length];
                    var listIdx = new List<int>(moduleVars.Length - 1);
                    var syncVars = new List<VariableRef>(moduleVars.Length - 1);
                    for (int i = 0; i < moduleVars.Length; ++i) {
                        VariableRef v = moduleVars[i];
                        if (module.GetVarDescription(v)!.SyncReadable) {
                            listIdx.Add(i);
                            syncVars.Add(v);
                        }
                        else {
                            vtqRes[i] = module.GetVarValue(v);
                        }
                    }
                    if (syncVars.Count > 0) {
                        Task<VTQ[]> syncValues = RestartOnExp(module, m => m.ReadVariables(info.Origin, syncVars.ToArray(), timeout));
                        return syncValues.ContinueOnMainThread<VTQ[], VTQ[]>((task) => {
                            VTQ[] syncValues = task.Result;
                            for (int i = 0; i < listIdx.Count; ++i) {
                                vtqRes[listIdx[i]] = syncValues[i];
                            }
                            return vtqRes;
                        });
                    }
                    else {
                        return Task.FromResult(vtqRes);
                    }
                }
            }).ToArray();


            VTQ[][] res = await Task.WhenAll(tasks);
            int[] ii = new int[moduleIDs.Count];
            var result = new VariableValues(variables.Count);
            foreach (VariableRef vref in variables) {
                string mid = vref.Object.ModuleID;
                int mIdx = moduleIDs.IndexOf(mid);
                VTQ[] vtqs = res[mIdx];
                int i = ii[mIdx];
                result.Add(VariableValue.Make(vref, vtqs[i]));
                ii[mIdx] = i + 1;
            }

            return result;
        }

        private VTQs DoReadVariables(List<VariableRef>? variables) {
            if (variables == null) return new VTQs(0);
            int N = variables.Count;
            var res = new VTQs(N);
            for (int i = 0; i < N; ++i) {
                VariableRef variable = variables[i];
                string mod = variable.Object.ModuleID;
                ModuleState module = ModuleFromIdOrThrow(mod);
                res.Add(module.GetVarValue(variable));
            }
            return res;
        }

        private VariableValues DoReadVariablesIgnoreMissing(List<VariableRef>? variables) {
            if (variables == null) return new VariableValues(0);
            int N = variables.Count;
            var res = new VariableValues(N);
            for (int i = 0; i < N; ++i) {
                VariableRef variable = variables[i];
                if (VarExists(variable)) {
                    string mod = variable.Object.ModuleID;
                    ModuleState module = ModuleFromIdOrThrow(mod);
                    res.Add(VariableValue.Make(variable, module.GetVarValue(variable)));
                }
            }
            return res;
        }

        private Task<T> RestartOnExp<T>(ModuleState ms, Func<SingleThreadModule, Task<T>> f) {

            Task<T> res;

            try {
                res = f(ms.Instance);
            }
            catch (Exception exp) {
                if (!(exp is NoRestartException)) {
                    var ignored = core.RestartModule(ms, exp.Message);
                }
                throw;
            }

            res.ContinueOnMainThread(tt => {

                if (tt.IsFaulted) {
                    Exception exp = tt.Exception!.GetBaseException() ?? tt.Exception;
                    if (!(exp is NoRestartException)) {
                        var ignored = core.RestartModule(ms, exp.Message);
                    }
                }

            });

            return res;
        }

        private bool VarExists(VariableRef variable) {
            string mod = variable.Object.ModuleID;
            ModuleState module = ModuleFromIdOrThrow(mod);
            return module.HasVarValue(variable);
        }

        private bool VarMissing(VariableValue variableValue) => !VarExists(variableValue);

        private bool VarExists(VariableValue variableValue) {
            VariableRef variable = variableValue.Variable;
            string mod = variable.Object.ModuleID;
            ModuleState module = ModuleFromIdOrThrow(mod);
            return module.HasVarValue(variable);
        }

        private Timestamp timeLastWriteVariablesThrottle = Timestamp.Empty;
        private Timestamp timeLastWriteVariablesSyncThrottle = Timestamp.Empty;

        internal void OnVariableValuesChanged(IList<VariableValuePrev> origValues, Func<ObjectRef, ObjectRef?> parentMap) {

            if (terminating) return;
            IList<VariableValuePrev> values = Compact(origValues);

            foreach (var session in sessions.Values) {

                if (session.HasEventSocket) {

                    var filtered = new VariableValues(values.Count);

                    for (int i = 0; i < values.Count; ++i) {
                        VariableValue v = values[i].Value;
                        VariableRef vr = v.Variable;
                        SubOptions? sub = null;
                        if (session.VariableRefs.ContainsKey(vr)) {
                            sub = session.VariableRefs[vr];
                        }
                        else {
                            sub = checkObjectInTree(v.Variable.Object, session.VariablesChangedEventTrees, parentMap);
                        }
                        bool ok = sub.HasValue && (sub.Value.Mode == ListenMode.AllUpdates || values[i].PreviousValue.V != v.Value.V || values[i].PreviousValue.Q != v.Value.Q);
                        if (ok) {
                            if (sub!.Value.SendValueWithEvent) {
                                filtered.Add(v);
                            }
                            else {
                                filtered.Add(new VariableValue(vr, v.Value.WithValue(DataValue.Empty)));
                            }
                        }
                    }

                    if (filtered.Count > 0) {
                        session.SendEvent_VariableValuesChanged(filtered);
                    }
                }
            }
        }

        private static IList<VariableValuePrev> Compact(IList<VariableValuePrev> values) {

            var groups = values.GroupBy(x => x.Value.Variable).ToArray();
            if (groups.Length == values.Count) {
                return values;
            }

            VariableValuePrev[] resValues = new VariableValuePrev[groups.Length];

            for (int i = 0; i < groups.Length; ++i) {

                IGrouping<VariableRef, VariableValuePrev> group = groups[i];

                Timestamp minT = Timestamp.Max;
                Timestamp maxT = Timestamp.Empty;

                VariableValuePrev? minVal = null;
                VariableValuePrev? maxVal = null;

                foreach (VariableValuePrev v in group) {
                    Timestamp time = v.Value.Value.T;
                    if (time > maxT) {
                        maxT = time;
                        maxVal = v;
                    }
                    if (time < minT) {
                        minT = time;
                        minVal = v;
                    }
                }
                resValues[i] = new VariableValuePrev(maxVal!.Value, minVal!.PreviousValue);
            }
            return resValues;
        }

        internal void OnVariableHistoryChanged(IList<HistoryChange> changes) {

            if (terminating) return;

            string? modID = null;
            Func<ObjectRef, ObjectRef?>? parentMap = null;

            foreach (var session in sessions.Values) {

                if (session.HasEventSocket) {

                    var filtered = new List<HistoryChange>(changes.Count);

                    for (int i = 0; i < changes.Count; ++i) {
                        HistoryChange change = changes[i];
                        VariableRef vr = change.Variable;
                        bool ok = false;
                        if (session.VariableHistoryRefs.Contains(vr)) {
                            ok = true;
                        }
                        else {

                            if (modID != vr.Object.ModuleID) {
                                modID = vr.Object.ModuleID;
                                var module = ModuleFromIdOrThrow(modID);
                                parentMap = module.GetObjectParent;
                            }

                            ok = checkObjectInTree(vr.Object, session.VariablesHistoryChangedEventTrees, parentMap!);
                        }

                        if (ok) {
                            filtered.Add(change);
                        }
                    }

                    if (filtered.Count > 0) {
                        session.SendEvent_VariableHistoryChanged(filtered);
                    }
                }
            }
        }

        internal void OnConfigChanged(List<ObjectRef> changedObjects, Func<ObjectRef, ObjectRef?> parentMap) {

            if (terminating) return;

            SessionInfo[] relevantSessions = sessions.Values.Where(s => s.HasEventSocket && s.ConfigChangeObjects.Count > 0).ToArray();

            if (relevantSessions.Length == 0) return;

            var changedObjectsWithAllParents = new HashSet<ObjectRef>();

            foreach (ObjectRef changed in changedObjects) {
                AddParents(changed, parentMap, changedObjectsWithAllParents);
            }

            foreach (var session in relevantSessions) {

                List<ObjectRef> filtered = session.ConfigChangeObjects
                    .Where(obj => changedObjectsWithAllParents.Contains(obj) || IsInTree(obj, changedObjects, parentMap))
                    .ToList();

                if (filtered.Count > 0) {
                    session.SendEvent_ConfigChanged(filtered);
                }
            }
        }

        internal void OnAlarmOrEvent(AlarmOrEvent ae) {

            if (terminating) return;

            SessionInfo[] relevantSessions = sessions.Values.Where(s => s.AlarmsAndEventsEnabled && s.HasEventSocket && (int)s.MinSeverity <= (int)ae.Severity).ToArray();

            foreach (var session in relevantSessions) {
                session.SendEvent_AlarmOrEvents(ae);
            }
        }

        private void AddParents(ObjectRef obj, Func<ObjectRef, ObjectRef?> parentMap, HashSet<ObjectRef> res) {
            res.Add(obj);
            ObjectRef? parent = parentMap(obj);
            if (parent.HasValue) {
                AddParents(parent.Value, parentMap, res);
            }
        }

        private bool IsInTree(ObjectRef obj, List<ObjectRef> treeRoots, Func<ObjectRef, ObjectRef?> parentMap) {
            if (treeRoots.Contains(obj)) return true;
            ObjectRef? parent = parentMap(obj);
            if (parent.HasValue) {
                return IsInTree(parent.Value, treeRoots, parentMap);
            }
            else {
                return false;
            }
        }

        private SubOptions? checkObjectInTree(ObjectRef obj, Dictionary<ObjectRef, SubOptions> trees, Func<ObjectRef, ObjectRef?> parentMap) {

            if (trees.Count == 0)
                return null;

            if (trees.ContainsKey(obj))
                return trees[obj];

            ObjectRef? parent = parentMap(obj);
            if (parent.HasValue) {
                return checkObjectInTree(parent.Value, trees, parentMap);
            }
            else {
                return null;
            }
        }

        private bool checkObjectInTree(ObjectRef obj, HashSet<ObjectRef> trees, Func<ObjectRef, ObjectRef?> parentMap) {

            if (trees.Count == 0)
                return false;

            if (trees.Contains(obj))
                return true;

            ObjectRef? parent = parentMap(obj);
            if (parent.HasValue) {
                return checkObjectInTree(parent.Value, trees, parentMap);
            }
            else {
                return false;
            }
        }

        private void GetAllVarRefsOfObjTree(ObjectInfos all, ObjectInfo objInfo, HashSet<ObjectRef> objsWithChildren, List<VariableRef> res) {

            ObjectRef objID = objInfo.ID;

            if (objInfo.Variables != null) {
                foreach (Variable v in objInfo.Variables) {
                    res.Add(new VariableRef(objID, v.Name));
                }
            }

            if (objsWithChildren.Contains(objID)) {
                foreach (ObjectInfo info in all) {
                    if (info.Parent.HasValue && info.Parent.Value.Object == objID) {
                        GetAllVarRefsOfObjTree(all, info, objsWithChildren, res);
                    }
                }
            }
        }

        private ReqResult Result_OK() {
            return ReqResult.OK(null);
        }

        private ReqResult Result_OK(object obj, Action<object, Stream>? serializer = null) {
            return ReqResult.OK(obj, serializer);
        }

        private ReqResult Result_BAD(string errMsg) {
            return ReqResult.Err(errMsg);
        }

        private ReqResult Result_ConnectivityErr(string errMsg) {
            //byte[] bytes = Encoding.UTF8.GetBytes(errMsg);
            //return new ReqResult(400, new MemoryStream(bytes));
            return ReqResult.Connectivity(errMsg);
        }

        private ModuleState ModuleFromIdOrThrow(string moduleID) {
            var moduls = core.modules;
            var res = moduls.FirstOrDefault(m => m.ID == moduleID);
            if (res == null) throw new Exception("Unknown module id: " + moduleID);
            return res;
        }

        public Task<object?> AddRequest(RequestBase req) {
            var promise = new TaskCompletionSource<object?>();
            ReqItem it = new ReqItem() {
                Req = req,
                Promise = promise
            };
            queue.Post(it);
            return promise.Task;
        }

        private readonly AsyncQueue<ReqItem> queue = new AsyncQueue<ReqItem>();

        private async Task ProcessReqLoop() {

            while (true) {

                ReqItem req = await queue.ReceiveAsync();
                // logger.Info($"Got Request {req.Req.GetPath()} Thread: {Thread.CurrentThread.ManagedThreadId}");

                if (terminating) {
                    if (req.Req is LogoutReq) {
                        req.Promise.SetResult(null);
                    }
                    else {
                        var e = new ConnectivityException($"Can not respond to {req.Req.GetPath()} request because system is shutting down.");
                        req.Promise.SetException(e);
                    }
                    continue;
                }

                var promise = req.Promise;
                try {
                    Task<ReqResult> t = Handle(req.Req, startingUp: false);
                    _ = t.ContinueWith(task => {
                        try {
                            ReqResult reqResult = task.Result;
                            // logger.Info($"Completed Request {req.Req.GetPath()} Thread: {Thread.CurrentThread.ManagedThreadId}");
                            if (reqResult.ResultCode == ReqRes.OK) {
                                promise.SetResult(reqResult.Obj);
                            }
                            else if (reqResult.ResultCode == ReqRes.Error) {
                                var e = new RequestException((reqResult.Obj as string) ?? "RequestException");
                                promise.SetException(e);
                            }
                            else if (reqResult.ResultCode == ReqRes.ConnectivityErr) {
                                var e = new ConnectivityException((reqResult.Obj as string) ?? "ConnectivityException");
                                promise.SetException(e);
                            }
                        }
                        catch (Exception exp) {
                            var e = exp.GetBaseException() ?? exp;
                            promise.TrySetException(e);
                        }
                    });
                }
                catch (Exception exp) {
                    var e = exp.GetBaseException() ?? exp;
                    promise.TrySetException(e);
                }
            }
        }

        public struct ReqItem
        {
            public RequestBase Req { get; set; }
            public TaskCompletionSource<object?> Promise { get; set; }
        }

        public class SessionInfo
        {
            public string ID { get; set; }
            public bool terminating = false;
            public bool eventPingEnabled = false;

            public byte BinaryVersion { get; set; }
            public byte EventDataVersion { get; set; }

            private Timestamp lastActivity = Timestamp.Now;
            private Timestamp lastEventActivity = Timestamp.Now;

            public Timestamp LastActivity => lastActivity;

            public void UpdateLastActivity() {
                lastActivity = Timestamp.Now;
            }

            private void UpdateLastEventActivity() {
                Timestamp now = Timestamp.Now;
                lastActivity = now;
                lastEventActivity = now;
            }

            public bool IsExpired => (Timestamp.Now - lastActivity) > Duration.FromHours(1);

            public Origin Origin { get; private set; }
            public string Challenge { get; private set; }
            public string Password { get; private set; }
            public bool Valid { get; set; } = false;

            public bool LogoutCompleted { get; set; } = false;

            public WebSocket? EventSocket { get; set; }
            public TaskCompletionSource<object?>? EventSocketTCS { get; set; }

            public void SetEventSocket(WebSocket? socket, TaskCompletionSource<object?>? eventSocketTCS) {
                EventSocket = socket;
                EventSocketTCS = eventSocketTCS;
                if (socket != null && eventPingEnabled) {
                    _ = PingTask();
                }
            }

            private Action<SessionInfo, string> terminate;

            public readonly Timestamp StartTime;

            public SessionInfo(string id, string challenge, Origin origin, string password, Action<SessionInfo, string> terminate) {
                ID = id;
                Challenge = challenge;
                Origin = origin;
                Password = password;
                this.terminate = terminate;
                StartTime = Timestamp.Now;
            }

            private void Terminate() {
                if (!LogoutCompleted) {
                    terminate(this, "");
                }
            }

            public bool HasEventSocket => EventSocket != null;

            public Dictionary<ObjectRef, SubOptions> VariablesChangedEventTrees { get; private set; } = new Dictionary<ObjectRef, SubOptions>();
            public Dictionary<VariableRef, SubOptions> VariableRefs { get; private set; } = new Dictionary<VariableRef, SubOptions>();
            public HashSet<ObjectRef> VariablesHistoryChangedEventTrees { get; private set; } = new HashSet<ObjectRef>();
            public HashSet<VariableRef> VariableHistoryRefs { get; private set; } = new HashSet<VariableRef>();
            public HashSet<ObjectRef> ConfigChangeObjects { get; private set; } = new HashSet<ObjectRef>();
            public bool AlarmsAndEventsEnabled { get; set; } = false;
            public Severity MinSeverity { get; set; } = Severity.Info;

            private bool forceEventBuffering = false;

            private Timestamp lastTimeVarValues = Timestamp.Empty;
            private Dictionary<VariableRef, VariableValue> bufferVarValues = new Dictionary<VariableRef, VariableValue>();

            private Timestamp lastTimeVarHistory = Timestamp.Empty;
            private Dictionary<VariableRef, HistoryChange> bufferVarHistory = new Dictionary<VariableRef, HistoryChange>();

            private Timestamp lastTimeConfigChanged = Timestamp.Empty;
            private HashSet<ObjectRef> bufferConfigChanges = new HashSet<ObjectRef>();

            private Timestamp lastTimeEvents = Timestamp.Empty;
            private List<AlarmOrEvent> bufferEvents = new List<AlarmOrEvent>();

            public void SendEvent_VariableValuesChanged(List<VariableValue> values) {
                if (forceEventBuffering) {
                    foreach (VariableValue vv in values) {
                        bufferVarValues[vv.Variable] = vv;
                    }
                    if (lastTimeVarValues.IsEmpty) {
                        lastTimeVarValues = Timestamp.Now;
                    }
                }
                else {
                    _ = SendVariables(values);
                }
            }

            public void SendEvent_VariableHistoryChanged(List<HistoryChange> changes) {
                if (forceEventBuffering) {
                    foreach (HistoryChange h in changes) {
                        VariableRef key = h.Variable;
                        if (bufferVarHistory.ContainsKey(key)) {
                            HistoryChange hh = bufferVarHistory[key];
                            hh.ChangeStart = Timestamp.MinOf(h.ChangeStart, hh.ChangeStart);
                            hh.ChangeEnd = Timestamp.MaxOf(h.ChangeEnd, hh.ChangeEnd);
                            hh.ChangeType = h.ChangeType == hh.ChangeType ? hh.ChangeType : HistoryChangeType.Mixed;
                            bufferVarHistory[key] = hh;
                        }
                        else {
                            bufferVarHistory[key] = h;
                        }
                    }
                    if (lastTimeVarHistory.IsEmpty) {
                        lastTimeVarHistory = Timestamp.Now;
                    }
                }
                else {
                    _ = SendVariableHistory(changes);
                }
            }

            public void SendEvent_ConfigChanged(List<ObjectRef> changes) {
                if (forceEventBuffering) {
                    foreach (ObjectRef obj in changes) {
                        bufferConfigChanges.Add(obj);
                    }
                    if (lastTimeConfigChanged.IsEmpty) {
                        lastTimeConfigChanged = Timestamp.Now;
                    }
                }
                else {
                    _ = SendConfigChanged(changes);
                }
            }

            public void SendEvent_AlarmOrEvents(AlarmOrEvent ae) {
                if (forceEventBuffering) {
                    if (bufferEvents.Count > 10000) {
                        logger.Warn("bufferEvents.Count > 10000");
                        bufferEvents.Clear();
                    }
                    else {
                        bufferEvents.Add(ae);
                        if (lastTimeEvents.IsEmpty) {
                            lastTimeEvents = Timestamp.Now;
                        }
                    }
                }
                else {
                    _ = SendAlarmOrEvents(new List<AlarmOrEvent>() { ae });
                }
            }

            private async Task SendVariables(List<VariableValue> values) {

                try {
                    forceEventBuffering = true;
                    var evnt = new EventContent() {
                        Event = EventType.OnVariableValueChanged,
                        Variables = values
                    };
                    await SendWebSocket(evnt);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private async Task SendVariableHistory(List<HistoryChange> values) {

                try {
                    forceEventBuffering = true;
                    var evnt = new EventContent() {
                        Event = EventType.OnVariableHistoryChanged,
                        Changes = values
                    };
                    await SendWebSocket(evnt);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private async Task SendConfigChanged(List<ObjectRef> changes) {

                try {
                    forceEventBuffering = true;
                    var evnt = new EventContent() {
                        Event = EventType.OnConfigChanged,
                        ChangedObjects = changes
                    };
                    await SendWebSocket(evnt);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private async Task SendAlarmOrEvents(List<AlarmOrEvent> events) {

                try {
                    forceEventBuffering = true;
                    var evnt = new EventContent() {
                        Event = EventType.OnAlarmOrEvent,
                        Events = events
                    };
                    await SendWebSocket(evnt);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private void CheckPendingEvents() {

                if (terminating || LogoutCompleted) return;

                int idx = MinIndexNotEmpty(lastTimeVarValues, lastTimeVarHistory, lastTimeConfigChanged, lastTimeEvents);
                if (idx < 0) return;

                switch (idx) {
                    case 0: {
                            var nextValues = bufferVarValues.Values.ToList();
                            bufferVarValues.Clear();
                            lastTimeVarValues = Timestamp.Empty;
                            _ = SendVariables(nextValues);
                            break;
                        }
                    case 1: {
                            var nextValues = bufferVarHistory.Values.ToList();
                            bufferVarHistory.Clear();
                            lastTimeVarHistory = Timestamp.Empty;
                            _ = SendVariableHistory(nextValues);
                            break;
                        }
                    case 2: {
                            var nextValues = bufferConfigChanges.ToList();
                            bufferConfigChanges.Clear();
                            lastTimeConfigChanged = Timestamp.Empty;
                            _ = SendConfigChanged(nextValues);
                            break;
                        }
                    case 3: {
                            var nextValues = bufferEvents.ToList();
                            bufferEvents.Clear();
                            lastTimeEvents = Timestamp.Empty;
                            _ = SendAlarmOrEvents(nextValues);
                            break;
                        }
                }
            }

            private int MinIndexNotEmpty(params Timestamp[] timestamps) {

                if (timestamps.All(t => t.IsEmpty)) return -1;

                int startIdx = 0;
                for (int i = 0; i < timestamps.Length; ++i) {
                    Timestamp t = timestamps[i];
                    if (t.NonEmpty) {
                        startIdx = i;
                        break;
                    }
                }

                Timestamp minT = timestamps[startIdx];
                int minIdx = startIdx;
                for (int n = startIdx + 1; n < timestamps.Length; ++n) {
                    Timestamp t = timestamps[n];
                    if (t.NonEmpty && t < minT) {
                        minT = t;
                        minIdx = n;
                    }
                }
                return minIdx;
            }

            private readonly ArraySegment<byte> ackByteBuffer = new ArraySegment<byte>(new byte[32]);
            private readonly MemoryStream streamSend = new MemoryStream(4*1024);

            private async Task SendWebSocket(EventContent evnt) {

                if (LogoutCompleted) {
                    return;
                }

                WebSocket? socket = EventSocket;
                if (socket == null) { return; }

                if (socket.State != WebSocketState.Open) {
                    await Task.Delay(500);
                    if (LogoutCompleted || terminating) return;
                    logger.Warn($"SendWebSocket: Will not send event because socket.State = {socket.State}. Terminating session...");
                    Terminate();
                    return;
                }

                try {

                    var stream = streamSend;

                    stream.Position = 0;
                    stream.SetLength(0);

                    WebSocketMessageType type;
                    if (EventDataVersion == 1) {
                        type = WebSocketMessageType.Binary;
                        WriteBinaryEventContent(evnt, stream, BinaryVersion);
                    }
                    else {
                        type = WebSocketMessageType.Text;
                        StdJson.ObjectToStream(evnt, stream);
                    }

                    ArraySegment<byte> segment;
                    stream.TryGetBuffer(out segment);

                    try {
                        await socket.SendAsync(segment, type, true, CancellationToken.None);
                    }
                    catch (Exception exp) {
                        if (LogoutCompleted || terminating) return;
                        Exception e = exp.GetBaseException() ?? exp;
                        var state = socket.State;
                        await Task.Delay(500);
                        if (LogoutCompleted || terminating) return;
                        logger.Warn(e, $"SendWebSocket: EventSocket.SendAsync, State = {state}. Terminating session...");
                        Terminate();
                        return;
                    }
                }
                catch (Exception exp) {
                    if (LogoutCompleted || terminating) return;
                    Exception e = exp.GetBaseException() ?? exp;
                    logger.Warn(e, "Exception in SendWebSocket. Terminating session...");
                    Terminate();
                    return;
                }

                try {
                    await socket.ReceiveAsync(ackByteBuffer, CancellationToken.None);
                    UpdateLastEventActivity();
                }
                catch (Exception exp) {
                    if (LogoutCompleted || terminating) return;
                    Exception e = exp.GetBaseException() ?? exp;
                    var state = socket.State;
                    await Task.Delay(500);
                    if (LogoutCompleted || terminating) return;
                    logger.Warn(e, $"SendWebSocket: EventSocket.ReceiveAsync ACK, State = {state}. Terminating session...");
                    Terminate();
                    return;
                }
            }

            private void WriteBinaryEventContent(EventContent evt, Stream stream, byte binaryVersion) {

                stream.WriteByte(1); // event data format
                stream.WriteByte(binaryVersion);
                stream.WriteByte((byte)evt.Event);

                switch (evt.Event) {

                    case EventType.OnVariableValueChanged:
                        VariableValue_Serializer.Serialize(stream, evt.Variables!, binaryVersion);
                        break;

                    case EventType.OnVariableHistoryChanged:
                        StdJson.ObjectToStream(evt.Changes!, stream);
                        break;

                    case EventType.OnConfigChanged:
                        StdJson.ObjectToStream(evt.ChangedObjects!, stream);
                        break;

                    case EventType.OnAlarmOrEvent:
                        StdJson.ObjectToStream(evt.Events!, stream);
                        break;

                    case EventType.OnPing:
                        break;
                }
            }

            private async Task PingTask() {

                TimeSpan interval = TimeSpan.FromMinutes(1);

                await Task.Delay(interval);

                var evnt = new EventContent() {
                    Event = EventType.OnPing
                };

                while (HasEventSocket && !terminating && !LogoutCompleted) {

                    Duration x = Timestamp.Now - lastEventActivity;
                    if (x > interval && !forceEventBuffering) {

                        try {
                            forceEventBuffering = true;
                            await SendWebSocket(evnt);
                        }
                        finally {
                            forceEventBuffering = false;
                        }
                        CheckPendingEvents();
                    }

                    await Task.Delay(interval);
                }
            }
        }
    }

    public enum ReqRes
    {
        OK,
        Error,
        ConnectivityErr
    }

    public sealed class ReqResult : IDisposable {

        private Action<object, Stream> serializer = (o, s) => StdJson.ObjectToStream(o, s);

        public static ReqResult OK(object? obj, Action<object, Stream>? serializer = null) {
            var res = new ReqResult(ReqRes.OK, 200, obj, serializer: serializer);
            if (serializer != null) {
                res.ContentType = "application/octet-stream";
            }
            return res;
        }

        public static ReqResult Err(string errMsg) {
            string js = "{ \"error\": " + StdJson.ValueToString(errMsg) + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(js);
            return new ReqResult(ReqRes.Error, 400, errMsg, new MemoryStream(bytes));
        }

        public static ReqResult Connectivity(string errMsg) {
            byte[] bytes = Encoding.UTF8.GetBytes(errMsg);
            return new ReqResult(ReqRes.ConnectivityErr, 400, errMsg, new MemoryStream(bytes));
        }

        private ReqResult(ReqRes resRes, int statusCode, object? obj, MemoryStream? memStream = null, Action<object, Stream>? serializer = null) {
            StatusCode = statusCode;
            ResultCode = resRes;
            Obj = obj;
            this.memStream = memStream;
            if (serializer != null) {
                this.serializer = serializer;
            }
        }

        public object? Obj { get; private set; }

        private MemoryStream? memStream;

        public MemoryStream Bytes {
            get {
                if (memStream != null) return memStream;
                if (Obj == null) {
                    memStream = new MemoryStream(0);
                    return memStream;
                }
                var res = MemoryManager.GetMemoryStream("HandleClientRequests.Result_OK");
                try {
                    serializer(Obj, res);
                    res.Seek(0, SeekOrigin.Begin);
                }
                catch (Exception) {
                    res.Dispose();
                    throw;
                }
                memStream = res;
                return res;
            }
        }

        public ReqRes ResultCode { get; private set; }

        public int StatusCode { get; private set; }

        public string ContentType { get; private set; } = "application/json";

        public string AsString() => Encoding.UTF8.GetString(Bytes.ToArray());

        public void Dispose() {
            memStream?.Dispose();
        }
    }
}
