// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    public class HandleClientRequests
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
        const string Req_Login = PathPrefix + "Login";
        const string Req_Auth = PathPrefix + "Authenticate";
        const string Req_GetModules = PathPrefix + "GetModules";
        const string Req_GetLocations = PathPrefix + "GetLocations";
        const string Req_GetLoginUser = PathPrefix + "GetLoginUser";
        const string Req_GetRootObject = PathPrefix + "GetRootObject";
        const string Req_GetAllObjects = PathPrefix + "GetAllObjects";
        const string Req_GetAllObjectsOfType = PathPrefix + "GetAllObjectsOfType";
        const string Req_GetObjectsByID = PathPrefix + "GetObjectsByID";
        const string Req_GetChildrenOfObjects = PathPrefix + "GetChildrenOfObjects";
        const string Req_GetAllObjectsWithVariablesOfType = PathPrefix + "GetAllObjectsWithVariablesOfType";
        const string Req_GetObjectValuesByID = PathPrefix + "GetObjectValuesByID";
        const string Req_GetMemberValues = PathPrefix + "GetMemberValues";
        const string Req_GetMetaInfos = PathPrefix + "GetMetaInfos";
        const string Req_GetParentOfObject = PathPrefix + "GetParentOfObject";
        const string Req_ReadVariables = PathPrefix + "ReadVariables";
        const string Req_ReadVariablesIgnoreMissing = PathPrefix + "ReadVariablesIgnoreMissing";
        const string Req_ReadVariablesSync = PathPrefix + "ReadVariablesSync";
        const string Req_ReadVariablesSyncIgnoreMissing = PathPrefix + "ReadVariablesSyncIgnoreMissing";
        const string Req_WriteVariables = PathPrefix + "WriteVariables";
        const string Req_WriteVariablesIgnoreMissing = PathPrefix + "WriteVariablesIgnoreMissing";
        const string Req_WriteVariablesSync = PathPrefix + "WriteVariablesSync";
        const string Req_WriteVariablesSyncIgnoreMissing = PathPrefix + "WriteVariablesSyncIgnoreMissing";
        const string Req_ReadAllVariablesOfObjectTree = PathPrefix + "ReadAllVariablesOfObjectTree";
        const string Req_UpdateConfig = PathPrefix + "UpdateConfig";
        const string Req_CallMethod = PathPrefix + "CallMethod";
        const string Req_Browse = PathPrefix + "BrowseObjectMemberValues";
        const string Req_Logout = PathPrefix + "Logout";
        const string Req_EnableVariableValueChangedEvents = PathPrefix + "EnableVariableValueChangedEvents";
        const string Req_EnableVariableHistoryChangedEvents = PathPrefix + "EnableVariableHistoryChangedEvents";
        const string Req_EnableConfigChangedEvents = PathPrefix + "EnableConfigChangedEvents";
        const string Req_DisableChangeEvents = PathPrefix + "DisableChangeEvents";
        const string Req_EnableAlarmsAndEvents = PathPrefix + "EnableAlarmsAndEvents";
        const string Req_DisableAlarmsAndEvents = PathPrefix + "DisableAlarmsAndEvents";
        const string Req_HistorianReadRaw = PathPrefix + "HistorianReadRaw";
        const string Req_HistorianCount = PathPrefix + "HistorianCount";
        const string Req_HistorianDeleteInterval = PathPrefix + "HistorianDeleteInterval";
        const string Req_HistorianModify = PathPrefix + "HistorianModify";
        const string Req_HistorianDeleteAllVariablesOfObjectTree = PathPrefix + "HistorianDeleteAllVariablesOfObjectTree";
        const string Req_HistorianDeleteVariables = PathPrefix + "HistorianDeleteVariables";
        const string Req_HistorianGetLatestTimestampDB = PathPrefix + "HistorianGetLatestTimestampDB";

        public static readonly HashSet<string> Requests = new HashSet<string>() {
            Req_Login,
            Req_Auth,
            Req_GetModules,
            Req_GetLocations,
            Req_GetLoginUser,
            Req_GetRootObject,
            Req_GetAllObjects,
            Req_GetAllObjectsOfType,
            Req_GetObjectsByID,
            Req_GetChildrenOfObjects,
            Req_GetAllObjectsWithVariablesOfType,
            Req_GetObjectValuesByID,
            Req_GetMemberValues,
            Req_GetParentOfObject,
            Req_ReadVariables,
            Req_ReadVariablesIgnoreMissing,
            Req_ReadVariablesSync,
            Req_ReadVariablesSyncIgnoreMissing,
            Req_WriteVariables,
            Req_WriteVariablesIgnoreMissing,
            Req_WriteVariablesSync,
            Req_WriteVariablesSyncIgnoreMissing,
            Req_ReadAllVariablesOfObjectTree,
            Req_UpdateConfig,
            Req_CallMethod,
            Req_Browse,
            Req_EnableVariableValueChangedEvents,
            Req_EnableVariableHistoryChangedEvents,
            Req_EnableConfigChangedEvents,
            Req_DisableChangeEvents,
            Req_EnableAlarmsAndEvents,
            Req_DisableAlarmsAndEvents,
            Req_HistorianReadRaw,
            Req_HistorianCount,
            Req_HistorianDeleteInterval,
            Req_HistorianModify,
            Req_HistorianDeleteAllVariablesOfObjectTree,
            Req_HistorianDeleteVariables,
            Req_HistorianGetLatestTimestampDB,
            Req_GetMetaInfos,
            Req_Logout
        };

        public static bool IsValid(string absolutePath) {
            return Requests.Contains(absolutePath);
        }

        private readonly MediatorCore core;

        public HandleClientRequests(MediatorCore core) {
            this.core = core;
        }

        private Dictionary<string, SessionInfo> sessions = new Dictionary<string, SessionInfo>();

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

            if (info.EventSocket != null) {
                logger.Warn("A websocket is already assigned to this session");
                await socket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "A websocket is already assigned to this session", CancellationToken.None);
                return;
            }
            var tcs = new TaskCompletionSource<object>();
            info.EventSocket = socket;
            info.EventSocketTCS = tcs;
            await tcs.Task;
        }

        public async Task<ReqResult> Handle(string path, JObject req) {

            switch (path) {

                case Req_Login: {

                        string password = "";
                        string moduleID = (string)req["moduleID"];
                        bool isModuleSession = moduleID != null;

                        var origin = new Origin();

                        if (isModuleSession) {

                            ModuleState module = core.modules.FirstOrDefault(m => m.ID == moduleID);
                            if (module == null) {
                                return Result_BAD($"Invalid login (unknown module id {moduleID})");
                            }
                            origin.Type = OriginType.Module;
                            origin.ID = moduleID;
                            origin.Name = module.Name;
                            password = module.Password;
                        }
                        else {

                            string login = (string)req["login"];
                            string[] roles = StdJson.ObjectFromJToken<string[]>(req["roles"]);

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
                        var obj = new JObject();
                        obj["session"] = session;
                        obj["challenge"] = challenge;
                        sessions[session] = new SessionInfo() {
                            Challenge = challenge,
                            Origin = origin,
                            Password = password
                        };
                        return Result_OK(obj);
                    }

                case Req_Auth: {

                        string session = ((string)req["session"]) ?? "";
                        if (!sessions.ContainsKey(session)) {
                            return Result_BAD("Invalid session");
                        }

                        SessionInfo info = sessions[session];
                        long hash = (long)(double)req["hash"];
                        string password = info.Password;
                        long myHash = ClientDefs.strHash(password + info.Challenge + password + session);
                        if (hash != myHash) {
                            sessions.Remove(session);
                            return Result_BAD("Invalid password");
                        }
                        info.Valid = true;
                        var obj = new JObject();
                        obj["session"] = session;
                        return Result_OK(obj);
                    }


                default:
                    return await HandleRegular(path, req);
            }
        }

        private async Task<ReqResult> HandleRegular(string path, JObject req) {

            string session = ((string)req["session"]) ?? "";
            if (!sessions.ContainsKey(session)) return Result_BAD("Missing session");

            SessionInfo info = sessions[session];
            if (!info.Valid) return Result_BAD("Invalid session");

            try {

                switch (path) {

                    case Req_GetModules: {

                            Func<ModuleState, bool> hasNumericVariables = (m) => {
                                return m.AllObjects.Any(obj => obj.Variables != null && obj.Variables.Any(v => v.IsNumeric || v.Type == DataType.Bool));
                            };

                            ModuleInfo[] res = core.modules.Select(m => new ModuleInfo() {
                                ID = m.ID,
                                Name = m.Name,
                                Enabled = m.Enabled,
                                HasNumericVariables = hasNumericVariables(m),
                            }).ToArray();

                            return Result_OK(res);
                        }

                    case Req_GetLocations: {
                            LocationInfo[] res = core.locations.Select(m => new LocationInfo() {
                                ID = m.ID,
                                Name = m.Name,
                                LongName = m.LongName,
                                Parent = m.Parent,
                                Config = m.Config
                            }).ToArray();
                            return Result_OK(res);
                        }

                    case Req_GetLoginUser: {
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

                    case Req_GetRootObject: {
                            string moduleID = ((string)req["moduleID"]) ?? "";
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            var res = module.AllObjects.FirstOrDefault(obj => !obj.Parent.HasValue);
                            return Result_OK(res);
                        }

                    case Req_GetAllObjects: {
                            string moduleID = ((string)req["moduleID"]) ?? "";
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            var res = module.AllObjects;
                            return Result_OK(res);
                        }

                    case Req_GetAllObjectsOfType: {
                            string moduleID = ((string)req["moduleID"]) ?? "";
                            string className = ((string)req["className"]) ?? "";
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            var res = module.AllObjects.Where(x => x.ClassName == className).ToArray();
                            return Result_OK(res);
                        }

                    case Req_GetObjectsByID: {

                            JToken tokenIDs = req["objectIDs"];
                            if (tokenIDs == null) throw new Exception("Missing objectIDs");
                            ObjectRef[] objectIDs = StdJson.ObjectFromJToken<ObjectRef[]>(tokenIDs);
                            ObjectInfo[] result = objectIDs.Select(id => {
                                ModuleState module = ModuleFromIdOrThrow(id.ModuleID);
                                foreach (ObjectInfo inf in module.AllObjects) {
                                    if (inf.ID == id) {
                                        return inf;
                                    }
                                }
                                throw new Exception("No object found with id " + id.ToString());
                            }).ToArray();
                            return Result_OK(result);
                        }

                    case Req_GetChildrenOfObjects: {

                            JToken tokenIDs = req["objectIDs"];
                            if (tokenIDs == null) throw new Exception("Missing objectIDs");
                            ObjectRef[] objectIDs = StdJson.ObjectFromJToken<ObjectRef[]>(tokenIDs);

                            ObjectInfo[] result = objectIDs.SelectMany(id => {
                                ModuleState module = ModuleFromIdOrThrow(id.ModuleID);
                                if (module.AllObjects.All(x => x.ID != id)) throw new Exception("No object found with id " + id.ToString());
                                return module.AllObjects.Where(x => x.Parent.HasValue && x.Parent.Value.Object == id).ToArray();
                            }).ToArray();
                            return Result_OK(result);
                        }

                    case Req_GetAllObjectsWithVariablesOfType: {
                            string moduleID = ((string)req["moduleID"]) ?? "";
                            JToken tokenTypes = req["types"];
                            string[] types = StdJson.ObjectFromJToken<string[]>(tokenTypes);
                            Func<Variable, bool> varHasType = (variable) => {
                                string type = variable.Type.ToString();
                                return types.Any(t => t == type);
                            };
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            var res = module.AllObjects
                                .Where(x => x.Variables != null && x.Variables.Any(varHasType))
                                .ToArray();
                            return Result_OK(res);
                        }

                    case Req_GetObjectValuesByID: {

                            JToken tokenIDs = req["objectIDs"];
                            if (tokenIDs == null) throw new Exception("Missing objectIDs");
                            ObjectRef[] objectIDs = StdJson.ObjectFromJToken<ObjectRef[]>(tokenIDs);

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
                            ObjectValue[] allValuesFlat = allValues.SelectMany(x => x).ToArray();

                            return Result_OK(allValuesFlat);
                        }

                    case Req_GetMemberValues: {

                            JToken tokenMember = req["member"];
                            if (tokenMember == null) throw new Exception("Missing member");
                            MemberRef[] member = StdJson.ObjectFromJToken<MemberRef[]>(tokenMember);

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
                            MemberValue[] allValuesFlat = allValues.SelectMany(x => x).ToArray();

                            return Result_OK(allValuesFlat);
                        }

                    case Req_GetParentOfObject: {
                            string strObjID = (string)req["objectID"];
                            if (strObjID == null) throw new Exception("Missing objectID");
                            ObjectRef objectID = ObjectRef.FromEncodedString(strObjID);
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

                    case Req_ReadVariables: {

                            VariableValue[] vvs = DoReadVariables(req, ignoreMissing: false);
                            return Result_OK(vvs.Select(vv => vv.Value).ToArray());
                        }

                    case Req_ReadVariablesIgnoreMissing: {

                            VariableValue[] vvs = DoReadVariables(req, ignoreMissing: true);
                            return Result_OK(vvs);
                        }

                    case Req_ReadVariablesSync: {

                            List<VariableValue> vvs = await DoReadVariablesSync(req, info, ignoreMissing: false);
                            return Result_OK(vvs.Select(vv => vv.Value).ToArray());
                        }

                    case Req_ReadVariablesSyncIgnoreMissing: {

                            List<VariableValue> vvs = await DoReadVariablesSync(req, info, ignoreMissing: true);
                            return Result_OK(vvs);
                        }

                    case Req_WriteVariables: {
                            await DoWriteVariables(req, info, ignoreMissing: false);
                            return Result_OK();
                        }

                    case Req_WriteVariablesIgnoreMissing: {
                            WriteResult res = await DoWriteVariables(req, info, ignoreMissing: true);
                            return Result_OK(res);
                        }

                    case Req_WriteVariablesSync: {
                            return await DoWriteVariablesSync(req, info, ignoreMissing: false);
                        }

                    case Req_WriteVariablesSyncIgnoreMissing: {
                            return await DoWriteVariablesSync(req, info, ignoreMissing: true);
                        }

                    case Req_ReadAllVariablesOfObjectTree: {
                            string objectID = (string)req["objectID"];
                            if (objectID == null) throw new Exception("Missing objectID");
                            ObjectRef obj = ObjectRef.FromEncodedString(objectID);
                            string mod = obj.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(mod);
                            IList<ObjectInfo> allObj = module.AllObjects;
                            var varRefs = new List<VariableRef>();
                            GetAllVarRefsOfObjTree(allObj, obj, varRefs);
                            VariableValue[] result = varRefs.Select(vr => {
                                VTQ vtq = module.GetVarValue(vr);
                                return new VariableValue(vr, vtq);
                            }).ToArray();
                            return Result_OK(result);
                        }

                    case Req_UpdateConfig: {

                            JToken tokenUpdateOrDeleteObjects = req["updateOrDeleteObjects"];
                            JToken tokenUpdateOrDeleteMembers = req["updateOrDeleteMembers"];
                            JToken tokenAddArrayElements = req["addArrayElements"];
                            ObjectValue[] updateOrDeleteObjects = tokenUpdateOrDeleteObjects == null ? new ObjectValue[0] : StdJson.ObjectFromJToken<ObjectValue[]>(tokenUpdateOrDeleteObjects);
                            MemberValue[] updateOrDeleteMembers = tokenUpdateOrDeleteMembers == null ? new MemberValue[0] : StdJson.ObjectFromJToken<MemberValue[]>(tokenUpdateOrDeleteMembers);
                            AddArrayElement[] addArrayElements = tokenAddArrayElements == null ? new AddArrayElement[0] : StdJson.ObjectFromJToken<AddArrayElement[]>(tokenAddArrayElements);

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
                            return Result_BAD(res.Error);
                        }

                    case Req_EnableVariableValueChangedEvents: {

                            JToken tokenOptions = req["options"];
                            JToken tokenVariables = req["variables"];
                            JToken tokenTreeRoots = req["idsOfEnabledTreeRoots"];

                            SubOptions options = StdJson.ObjectFromJToken<SubOptions>(tokenOptions);
                            VariableRef[] variables = tokenVariables == null ? new VariableRef[0] : StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);
                            ObjectRef[] idsOfEnabledTreeRoots = tokenTreeRoots == null ? new ObjectRef[0] : StdJson.ObjectFromJToken<ObjectRef[]>(tokenTreeRoots);

                            foreach (ObjectRef obj in idsOfEnabledTreeRoots) {
                                info.VariablesChangedEventTrees[obj] = options;
                            }

                            foreach (VariableRef vr in variables) {
                                info.VariableRefs[vr] = options;
                            }

                            return Result_OK();
                        }

                    case Req_EnableVariableHistoryChangedEvents: {

                            JToken tokenVariables = req["variables"];
                            JToken tokenTreeRoots = req["idsOfEnabledTreeRoots"];

                            VariableRef[] variables = tokenVariables == null ? new VariableRef[0] : StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);
                            ObjectRef[] idsOfEnabledTreeRoots = tokenTreeRoots == null ? new ObjectRef[0] : StdJson.ObjectFromJToken<ObjectRef[]>(tokenTreeRoots);

                            foreach (ObjectRef obj in idsOfEnabledTreeRoots) {
                                info.VariablesHistoryChangedEventTrees.Add(obj);
                            }

                            foreach (VariableRef vr in variables) {
                                info.VariableHistoryRefs.Add(vr);
                            }

                            return Result_OK();
                        }

                    case Req_EnableConfigChangedEvents: {

                            JToken tokenObjects = req["objects"];
                            ObjectRef[] objects = tokenObjects == null ? new ObjectRef[0] : StdJson.ObjectFromJToken<ObjectRef[]>(tokenObjects);

                            foreach (ObjectRef obj in objects) {
                                info.ConfigChangeObjects.Add(obj);
                            }

                            return Result_OK();
                        }

                    case Req_DisableChangeEvents: {

                            bool disableVarValueChanges = (bool)req["disableVarValueChanges"];
                            bool disableVarHistoryChanges = (bool)req["disableVarHistoryChanges"];
                            bool disableConfigChanges = (bool)req["disableConfigChanges"];

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

                    case Req_EnableAlarmsAndEvents: {
                            Severity minSeverity = (Severity)Enum.Parse(typeof(Severity), (string)req["minSeverity"]);
                            info.AlarmsAndEventsEnabled = true;
                            info.MinSeverity = minSeverity;
                            return Result_OK();
                        }

                    case Req_DisableAlarmsAndEvents: {
                            info.AlarmsAndEventsEnabled = false;
                            return Result_OK();
                        }

                    case Req_HistorianReadRaw: {

                            VariableRef variable = StdJson.ObjectFromJToken<VariableRef>(req["variable"]);
                            Timestamp tStart = Timestamp.FromISO8601((string)req["startInclusive"]);
                            Timestamp tEnd   = Timestamp.FromISO8601((string)req["endInclusive"]);
                            int maxValues = (int)req["maxValues"];
                            BoundingMethod bounding = (BoundingMethod)Enum.Parse(typeof(BoundingMethod), (string)req["bounding"]);
                            QualityFilter filter = QualityFilter.ExcludeNone;
                            string strFilter = (string)req["filter"];
                            if (strFilter != null) {
                                filter = (QualityFilter)Enum.Parse(typeof(QualityFilter), strFilter);
                            }
                            IList<VTTQ> vttqs = await core.history.HistorianReadRaw(variable, tStart, tEnd, maxValues, bounding, filter);
                            return Result_OK(vttqs);
                        }

                    case Req_HistorianCount: {

                            VariableRef variable = StdJson.ObjectFromJToken<VariableRef>(req["variable"]);
                            Timestamp tStart = Timestamp.FromISO8601((string)req["startInclusive"]);
                            Timestamp tEnd = Timestamp.FromISO8601((string)req["endInclusive"]);
                            QualityFilter filter = QualityFilter.ExcludeNone;
                            string strFilter = (string)req["filter"];
                            if (strFilter != null) {
                                filter = (QualityFilter)Enum.Parse(typeof(QualityFilter), strFilter);
                            }

                            long count = await core.history.HistorianCount(variable, tStart, tEnd, filter);
                            return Result_OK(count);
                        }
                    case Req_HistorianDeleteInterval: {

                            VariableRef variable = StdJson.ObjectFromJToken<VariableRef>(req["variable"]);
                            Timestamp tStart = Timestamp.FromISO8601((string)req["startInclusive"]);
                            Timestamp tEnd = Timestamp.FromISO8601((string)req["endInclusive"]);

                            long count = await core.history.HistorianDeleteInterval(variable, tStart, tEnd);
                            return Result_OK(count);
                        }

                    case Req_HistorianModify: {

                            VariableRef variable = StdJson.ObjectFromJToken<VariableRef>(req["variable"]);
                            VTQ[] data = StdJson.ObjectFromJToken<VTQ[]>(req["data"]);
                            ModifyMode mode = (ModifyMode)Enum.Parse(typeof(ModifyMode), (string)req["mode"]);

                            await core.history.HistorianModify(variable, data, mode);
                            return Result_OK();
                        }

                    case Req_HistorianDeleteAllVariablesOfObjectTree: {

                            string objectID = (string)req["objectID"];
                            if (objectID == null) throw new Exception("Missing objectID");
                            ObjectRef obj = ObjectRef.FromEncodedString(objectID);
                            string mod = obj.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(mod);
                            IList<ObjectInfo> allObj = module.AllObjects;
                            var varRefs = new List<VariableRef>();
                            GetAllVarRefsOfObjTree(allObj, obj, varRefs);
                            await core.history.DeleteVariables(varRefs);
                            return Result_OK();
                        }

                    case Req_HistorianDeleteVariables: {
                            JToken tokenVariables = req["variables"];
                            if (tokenVariables == null) throw new Exception("Missing variables");
                            VariableRef[] variables = StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);
                            await core.history.DeleteVariables(variables);
                            return Result_OK();
                        }

                    case Req_HistorianGetLatestTimestampDB: {

                            VariableRef variable = StdJson.ObjectFromJToken<VariableRef>(req["variable"]);
                            Timestamp tStart = Timestamp.FromISO8601((string)req["startInclusive"]);
                            Timestamp tEnd = Timestamp.FromISO8601((string)req["endInclusive"]);

                            VTTQ? res = await core.history.HistorianGetLatestTimestampDb(variable, tStart, tEnd);
                            return Result_OK(res);
                        }

                    case Req_CallMethod: {

                            string moduleID   = (string)req["moduleID"];
                            string methodName = (string)req["methodName"];
                            NamedValue[] parameters = StdJson.ObjectFromJToken<NamedValue[]>(req["parameters"]);

                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            Result<DataValue> res = await RestartOnExp(module, m => m.OnMethodCall(info.Origin, methodName, parameters));

                            if (res.IsOK)
                                return Result_OK(res.Value);
                            else
                                return Result_BAD(res.Error);
                        }

                    case Req_Browse: {

                            MemberRef member = StdJson.ObjectFromJToken<MemberRef>(req["member"]);
                            int? continueID = null;

                            JToken id;
                            if (req.TryGetValue("continueID", out id)) {
                                continueID = StdJson.ObjectFromJToken<int>(id);
                            }

                            string moduleID = member.Object.ModuleID;
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            BrowseResult res = await RestartOnExp(module, m => m.BrowseObjectMemberValues(member, continueID));

                            return Result_OK(res);
                        }

                    case Req_GetMetaInfos: {

                            string moduleID = (string)req["moduleID"];
                            ModuleState module = ModuleFromIdOrThrow(moduleID);
                            try {
                                MetaInfos meta = await module.Instance.GetMetaInfo();
                                return Result_OK(meta);
                            }
                            catch (Exception exp) {
                                return Result_BAD(exp.Message);
                            }
                        }

                    case Req_Logout: {
                            info.LogoutCompleted = true;
                            sessions.Remove(session);
                            if (info.EventSocket != null) {
                                info.EventSocketTCS.TrySetResult(null);
                            }
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

        private async Task<ReqResult> DoWriteVariablesSync(JObject req, SessionInfo info, bool ignoreMissing) {
            JToken tokenValues = req["values"];
            if (tokenValues == null) throw new Exception("Missing values");
            VariableValue[] values = StdJson.ObjectFromJToken<VariableValue[]>(tokenValues);
            VariableError[] ignoredVars = null;
            if (ignoreMissing) {
                VariableValue[] filteredValues = values.Where(VarExists).ToArray();
                if (filteredValues.Length < values.Length) {
                    ignoredVars = values.Where(VarMissing).Select(v => new VariableError(v.Variable, $"Variable {v.Variable.ToString()} does not exist.")).ToArray();
                }
                values = filteredValues;
            }
            string strTimeout = (string)req["timeout"];
            Duration? timeout = null;
            if (strTimeout != null) {
                timeout = Duration.Parse(strTimeout);
            }
            List<string> moduleIDs = values.Select(x => x.Variable.Object.ModuleID).Distinct().ToList();
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
                VariableError[] writableErrors = null;
                if (filtereModuleValues.Length < moduleValues.Length) {
                    VariableValue[] removedModuleValues = moduleValues.Where(vv => !(module.GetVarDescription(vv.Variable)?.Writable ?? false)).ToArray();
                    moduleValues = filtereModuleValues;
                    writableErrors = removedModuleValues.Select(vv => new VariableError(vv.Variable, $"Variable {vv.Variable.ToString()} is not writable.")).ToArray();
                }
                if (moduleValues.Length > 0) {
                    Task<WriteResult> t = RestartOnExp(module, m => m.WriteVariables(info.Origin, moduleValues, timeout));
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

        private async Task<WriteResult> DoWriteVariables(JObject req, SessionInfo info, bool ignoreMissing) {
            JToken tokenValues = req["values"];
            if (tokenValues == null) throw new Exception("Missing values");
            VariableValue[] values = StdJson.ObjectFromJToken<VariableValue[]>(tokenValues);
            VariableError[] ignoredVars = null;
            if (ignoreMissing) {
                VariableValue[] filteredValues = values.Where(VarExists).ToArray();
                if (filteredValues.Length < values.Length) {
                    ignoredVars = values.Where(VarMissing).Select(v => new VariableError(v.Variable, $"Variable {v.Variable.ToString()} does not exist.")).ToArray();
                }
                values = filteredValues;
            }
            string[] moduleIDs = values.Select(x => x.Variable.Object.ModuleID).Distinct().ToArray();

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
                    var ignored = RestartOnExp(module, m => m.WriteVariables(info.Origin, moduleValues, null));
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

        private async Task<List<VariableValue>> DoReadVariablesSync(JObject req, SessionInfo info, bool ignoreMissing) {
            JToken tokenVariables = req["variables"];
            if (tokenVariables == null) throw new Exception("Missing variables");
            VariableRef[] variables = StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);

            if (ignoreMissing) {
                variables = variables.Where(VarExists).ToArray();
            }

            string strTimeout = (string)req["timeout"];
            Duration? timeout = null;
            if (strTimeout != null) {
                timeout = Duration.Parse(strTimeout);
            }
            List<string> moduleIDs = variables.Select(x => x.Object.ModuleID).Distinct().ToList();

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
                if (moduleVars.All(v => module.GetVarDescription(v).SyncReadable)) {
                    return RestartOnExp(module, m => m.ReadVariables(info.Origin, moduleVars, timeout));
                }
                else {
                    VTQ[] vtqRes = new VTQ[moduleVars.Length];
                    var listIdx = new List<int>(moduleVars.Length - 1);
                    var syncVars = new List<VariableRef>(moduleVars.Length - 1);
                    for (int i = 0; i < moduleVars.Length; ++i) {
                        VariableRef v = moduleVars[i];
                        if (module.GetVarDescription(v).SyncReadable) {
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
            var result = new List<VariableValue>(variables.Length);
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

        private VariableValue[] DoReadVariables(JObject req, bool ignoreMissing) {
            JToken tokenVariables = req["variables"];
            if (tokenVariables == null) throw new Exception("Missing variables");
            VariableRef[] variables = StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);
            VariableValue[] res = variables.Where(v => !ignoreMissing || VarExists(v)).Select(variable => {
                string mod = variable.Object.ModuleID;
                ModuleState module = ModuleFromIdOrThrow(mod);
                return VariableValue.Make(variable, module.GetVarValue(variable));
            }).ToArray();
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
                    Exception exp = tt.Exception.GetBaseException() ?? tt.Exception;
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

                if (session.EventSocket != null && session.EventSocket.State == WebSocketState.Open) {

                    var filtered = new List<VariableValue>(values.Count);

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
                            if (sub.Value.SendValueWithEvent) {
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

                VariableValuePrev minVal = null;
                VariableValuePrev maxVal = null;

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
                resValues[i] = new VariableValuePrev(maxVal.Value, minVal.PreviousValue);
            }
            return resValues;
        }

        internal void OnVariableHistoryChanged(IList<HistoryChange> changes) {

            if (terminating) return;

            string modID = null;
            Func<ObjectRef, ObjectRef?> parentMap = null;

            foreach (var session in sessions.Values) {

                if (session.EventSocket != null && session.EventSocket.State == WebSocketState.Open) {

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

                            ok = checkObjectInTree(vr.Object, session.VariablesHistoryChangedEventTrees, parentMap);
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

        internal void OnConfigChanged(IList<ObjectRef> changedObjects, Func<ObjectRef, ObjectRef?> parentMap) {

            if (terminating) return;

            SessionInfo[] relevantSessions = sessions.Values.Where(s => s.EventSocket != null && s.EventSocket.State == WebSocketState.Open && s.ConfigChangeObjects.Count > 0).ToArray();

            if (relevantSessions.Length == 0) return;

            var changedObjectsWithAllParents = new HashSet<ObjectRef>();

            foreach (ObjectRef changed in changedObjects) {
                AddParents(changed, parentMap, changedObjectsWithAllParents);
            }

            foreach (var session in relevantSessions) {

                ObjectRef[] filtered = session.ConfigChangeObjects
                    .Where(obj => changedObjectsWithAllParents.Contains(obj) || IsInTree(obj, changedObjects, parentMap))
                    .ToArray();

                if (filtered.Length > 0) {
                    session.SendEvent_ConfigChanged(filtered);
                }
            }
        }

        internal void OnAlarmOrEvent(AlarmOrEvent ae) {

            if (terminating) return;

            SessionInfo[] relevantSessions = sessions.Values.Where(s => s.AlarmsAndEventsEnabled && s.EventSocket != null && s.EventSocket.State == WebSocketState.Open && (int)s.MinSeverity <= (int)ae.Severity).ToArray();

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

        private bool IsInTree(ObjectRef obj, IList<ObjectRef> treeRoots, Func<ObjectRef, ObjectRef?> parentMap) {
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

        private void GetAllVarRefsOfObjTree(IList<ObjectInfo> all, ObjectRef objID, List<VariableRef> res) {

            foreach (ObjectInfo info in all) {
                if (info.ID == objID && info.Variables != null) {
                    foreach (Variable v in info.Variables) {
                        res.Add(new VariableRef(objID, v.Name));
                    }
                    break;
                }
            }

            foreach (ObjectInfo info in all) {
                if (info.Parent.HasValue && info.Parent.Value.Object == objID) {
                    GetAllVarRefsOfObjTree(all, info.ID, res);
                }
            }
        }

        private ReqResult Result_OK() {
            return new ReqResult(200, new MemoryStream(0));
        }

        private ReqResult Result_OK(object obj) {
            var res = MemoryManager.GetMemoryStream("HandleClientRequests.Result_OK");
            try {
                StdJson.ObjectToStream(obj, res);
                res.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception) {
                res.Dispose();
                throw;
            }
            return new ReqResult(200, res);
        }

        private ReqResult Result_BAD(string errMsg) {
            string js = "{ \"error\": " + StdJson.ValueToString(errMsg) + "}";
            byte[] bytes = Encoding.UTF8.GetBytes(js);
            return new ReqResult(400, new MemoryStream(bytes));
        }

        private ModuleState ModuleFromIdOrThrow(string moduleID) {
            var moduls = core.modules;
            var res = moduls.FirstOrDefault(m => m.ID == moduleID);
            if (res == null) throw new Exception("Unknown module id: " + moduleID);
            return res;
        }

        public class SessionInfo
        {
            public bool terminating = false;

            public Origin Origin { get; set; }
            public string Challenge { get; set; } = "";
            public string Password { get; set; } = "";
            public bool Valid { get; set; } = false;

            public bool LogoutCompleted { get; set; } = false;

            public WebSocket EventSocket { get; set; }
            public TaskCompletionSource<object> EventSocketTCS { get; set; }

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
                    lastTimeVarValues = Timestamp.Now;
                }
                else {
                    var ignored = SendVariables(values);
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
                    lastTimeVarHistory = Timestamp.Now;
                }
                else {
                    var ignored = SendVariableHistory(changes);
                }
            }

            public void SendEvent_ConfigChanged(ObjectRef[] changes) {
                if (forceEventBuffering) {
                    foreach (ObjectRef obj in changes) {
                        bufferConfigChanges.Add(obj);
                    }
                    lastTimeConfigChanged = Timestamp.Now;
                }
                else {
                    var ignored = SendConfigChanged(changes);
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
                        lastTimeEvents = Timestamp.Now;
                    }
                }
                else {
                    var ignored = SendAlarmOrEvents(new AlarmOrEvent[] { ae });
                }
            }

            private async Task SendVariables(IList<VariableValue> values) {

                try {
                    forceEventBuffering = true;
                    await SendWebSocket("{ \"event\": \"OnVariableValueChanged\", \"variables\": ", values);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private async Task SendVariableHistory(IList<HistoryChange> values) {

                try {
                    forceEventBuffering = true;
                    await SendWebSocket("{ \"event\": \"OnVariableHistoryChanged\", \"changes\": ", values);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private async Task SendConfigChanged(ObjectRef[] changes) {

                try {
                    forceEventBuffering = true;
                    await SendWebSocket("{ \"event\": \"OnConfigChanged\", \"changedObjects\": ", changes);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private async Task SendAlarmOrEvents(AlarmOrEvent[] events) {

                try {
                    forceEventBuffering = true;
                    await SendWebSocket("{ \"event\": \"OnAlarmOrEvent\", \"events\": ", events);
                }
                finally {
                    forceEventBuffering = false;
                }
                CheckPendingEvents();
            }

            private void CheckPendingEvents() {

                if (terminating) return;

                int idx = MinIndexNotEmpty(lastTimeVarValues, lastTimeVarHistory, lastTimeConfigChanged, lastTimeEvents);
                if (idx < 0) return;

                switch (idx) {
                    case 0: {
                            var nextValues = bufferVarValues.Values.ToArray();
                            bufferVarValues.Clear();
                            lastTimeVarValues = Timestamp.Empty;
                            Task ignored = SendVariables(nextValues);
                            break;
                        }
                    case 1: {
                            var nextValues = bufferVarHistory.Values.ToArray();
                            bufferVarHistory.Clear();
                            lastTimeVarHistory = Timestamp.Empty;
                            Task ignored = SendVariableHistory(nextValues);
                            break;
                        }
                    case 2: {
                            var nextValues = bufferConfigChanges.ToArray();
                            bufferConfigChanges.Clear();
                            lastTimeConfigChanged = Timestamp.Empty;
                            Task ignored = SendConfigChanged(nextValues);
                            break;
                        }
                    case 3: {
                            var nextValues = bufferEvents.ToArray();
                            bufferEvents.Clear();
                            lastTimeEvents = Timestamp.Empty;
                            Task ignored = SendAlarmOrEvents(nextValues);
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

            private readonly static Encoding UTF8_NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

            private readonly ArraySegment<byte> ackByteBuffer = new ArraySegment<byte>(new byte[32]);
            private readonly MemoryStream streamSend = new MemoryStream(4*1024);

            private async Task SendWebSocket(string msgStart, object content) {

                if (LogoutCompleted) {
                    return;
                }

                WebSocket socket = EventSocket;
                if (socket == null) { return; }

                if (socket.State != WebSocketState.Open) {
                    logger.Info($"SendWebSocket: Will not send event because socket.State = {socket.State}");
                    // TODO: Close session
                    return;
                }

                try {

                    var stream = streamSend;

                    stream.Position = 0;
                    stream.SetLength(0);

                    using (var writer = new StreamWriter(stream, UTF8_NoBOM, 1024, leaveOpen: true)) {
                        writer.Write(msgStart);
                        StdJson.ObjectToWriter(content, writer);
                        writer.Write("}");
                    }

                    ArraySegment<byte> segment;
                    stream.TryGetBuffer(out segment);

                    try {
                        await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception exp) {
                        if (LogoutCompleted) return;
                        Exception e = exp.GetBaseException() ?? exp;
                        var state = socket.State;
                        logger.Warn(e, $"SendWebSocket: EventSocket.SendAsync, State = {state}");
                        // TODO: Close session
                    }
                }
                catch (Exception exp) {
                    if (LogoutCompleted) return;
                    Exception e = exp.GetBaseException() ?? exp;
                    logger.Warn(e, "Exception in SendWebSocket");
                }

                try {
                    await socket.ReceiveAsync(ackByteBuffer, CancellationToken.None);
                }
                catch (Exception exp) {
                    if (LogoutCompleted) return;
                    Exception e = exp.GetBaseException() ?? exp;
                    var state = socket.State;
                    logger.Warn(e, $"SendWebSocket: EventSocket.ReceiveAsync ACK, State = {state}");
                    // TODO: Close session
                }
            }
        }
    }

    public sealed class ReqResult : IDisposable {

        public ReqResult(int statusCode, MemoryStream bytes) {
            if (bytes == null) throw new ArgumentNullException("bytes");
            StatusCode = statusCode;
            Bytes = bytes;
        }

        public MemoryStream Bytes { get; private set; }

        public int StatusCode { get; private set; }

        public string AsString() => Encoding.UTF8.GetString(Bytes.ToArray());

        public void Dispose() {
            Bytes.Dispose();
        }
    }
}
