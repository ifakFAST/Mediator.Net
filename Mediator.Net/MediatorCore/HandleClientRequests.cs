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

        public bool terminating = false;

        const string Req_Login = "/Mediator/Login";
        const string Req_Auth = "/Mediator/Authenticate";
        const string Req_GetModules = "/Mediator/GetModules";
        const string Req_GetLocations = "/Mediator/GetLocations";
        const string Req_GetLoginUser = "/Mediator/GetLoginUser";
        const string Req_GetRootObject = "/Mediator/GetRootObject";
        const string Req_GetAllObjects = "/Mediator/GetAllObjects";
        const string Req_GetAllObjectsOfType = "/Mediator/GetAllObjectsOfType";
        const string Req_GetObjectsByID = "/Mediator/GetObjectsByID";
        const string Req_GetChildrenOfObjects = "/Mediator/GetChildrenOfObjects";
        const string Req_GetAllObjectsWithVariablesOfType = "/Mediator/GetAllObjectsWithVariablesOfType";
        const string Req_GetObjectValuesByID = "/Mediator/GetObjectValuesByID";
        const string Req_GetMemberValues = "/Mediator/GetMemberValues";
        const string Req_GetMetaInfos = "/Mediator/GetMetaInfos";
        const string Req_GetParentOfObject = "/Mediator/GetParentOfObject";
        const string Req_ReadVariables = "/Mediator/ReadVariables";
        const string Req_ReadVariablesSync = "/Mediator/ReadVariablesSync";
        const string Req_WriteVariables = "/Mediator/WriteVariables";
        const string Req_WriteVariablesSync = "/Mediator/WriteVariablesSync";
        const string Req_ReadAllVariablesOfObjectTree = "/Mediator/ReadAllVariablesOfObjectTree";
        const string Req_UpdateConfig = "/Mediator/UpdateConfig";
        const string Req_CallMethod = "/Mediator/CallMethod";
        const string Req_Browse = "/Mediator/BrowseObjectMemberValues";
        const string Req_Logout = "/Mediator/Logout";
        const string Req_EnableVariableValueChangedEvents = "/Mediator/EnableVariableValueChangedEvents";
        const string Req_EnableVariableHistoryChangedEvents = "/Mediator/EnableVariableHistoryChangedEvents";
        const string Req_EnableConfigChangedEvents = "/Mediator/EnableConfigChangedEvents";
        const string Req_DisableChangeEvents = "/Mediator/DisableChangeEvents";
        const string Req_EnableAlarmsAndEvents = "/Mediator/EnableAlarmsAndEvents";
        const string Req_DisableAlarmsAndEvents = "/Mediator/DisableAlarmsAndEvents";
        const string Req_HistorianReadRaw = "/Mediator/HistorianReadRaw";
        const string Req_HistorianCount = "/Mediator/HistorianCount";
        const string Req_HistorianDeleteInterval = "/Mediator/HistorianDeleteInterval";
        const string Req_HistorianModify = "/Mediator/HistorianModify";
        const string Req_HistorianDeleteAllVariablesOfObjectTree = "/Mediator/HistorianDeleteAllVariablesOfObjectTree";
        const string Req_HistorianDeleteVariables = "/Mediator/HistorianDeleteVariables";
        const string Req_HistorianGetLatestTimestampDB = "/Mediator/HistorianGetLatestTimestampDB";

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
            Req_ReadVariablesSync,
            Req_WriteVariables,
            Req_WriteVariablesSync,
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
                            ModuleInfo[] res = core.modules.Select(m => new ModuleInfo() {
                                ID = m.ID,
                                Name = m.Name,
                                Enabled = m.Enabled
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

                            JToken tokenVariables = req["variables"];
                            if (tokenVariables == null) throw new Exception("Missing variables");
                            VariableRef[] variables = StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);
                            VTQ[] res = variables.Select(variable => {
                                string mod = variable.Object.ModuleID;
                                ModuleState module = ModuleFromIdOrThrow(mod);
                                return module.GetVarValue(variable);
                            }).ToArray();
                            return Result_OK(res);
                        }

                    case Req_ReadVariablesSync: {

                            JToken tokenVariables = req["variables"];
                            if (tokenVariables == null) throw new Exception("Missing variables");
                            VariableRef[] variables = StdJson.ObjectFromJToken<VariableRef[]>(tokenVariables);
                            string strTimeout = (string)req["timeout"];
                            Duration? timeout = null;
                            if (strTimeout != null) {
                                timeout = Duration.Parse(strTimeout);
                            }
                            List<string> moduleIDs = variables.Select(x => x.Object.ModuleID).Distinct().ToList();
                            foreach (string moduleID in moduleIDs) {
                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                VariableRef[] moduleVars = variables.Where(v => v.Object.ModuleID == moduleID).ToArray();
                                module.ValidateVariableRefsOrThrow(moduleVars);
                            }

                            Task<VTQ[]>[] tasks = moduleIDs.Select(moduleID => {
                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                VariableRef[] moduleVars = variables.Where(v => v.Object.ModuleID == moduleID).ToArray();
                                return RestartOnExp(module, m => m.ReadVariables(info.Origin, moduleVars, timeout));
                            }).ToArray();


                            VTQ[][] res = await Task.WhenAll(tasks);
                            int[] ii = new int[moduleIDs.Count];
                            var result = new List<VTQ>(variables.Length);
                            foreach (VariableRef vref in variables) {
                                string mid = vref.Object.ModuleID;
                                int mIdx = moduleIDs.IndexOf(mid);
                                VTQ[] vtqs = res[mIdx];
                                int i = ii[mIdx];
                                result.Add(vtqs[i]);
                                ii[mIdx] = i + 1;
                            }

                            return Result_OK(result);
                        }

                    case Req_WriteVariables: {

                            JToken tokenValues = req["values"];
                            if (tokenValues == null) throw new Exception("Missing values");
                            VariableValue[] values = StdJson.ObjectFromJToken<VariableValue[]>(tokenValues);
                            string[] moduleIDs = values.Select(x => x.Variable.Object.ModuleID).Distinct().ToArray();

                            foreach (string moduleID in moduleIDs) {
                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                                module.ValidateVariableValuesOrThrow(moduleValues);
                            }

                            int maxBufferCount = 0;
                            foreach (string moduleID in moduleIDs) {
                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                                int count = module.UpdateVariableValues(moduleValues);
                                maxBufferCount = Math.Max(maxBufferCount, count);
                                var ignored = RestartOnExp(module, m => m.WriteVariables(info.Origin, moduleValues, null));
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

                            return Result_OK();
                        }

                    case Req_WriteVariablesSync: {

                            JToken tokenValues = req["values"];
                            if (tokenValues == null) throw new Exception("Missing values");
                            VariableValue[] values = StdJson.ObjectFromJToken<VariableValue[]>(tokenValues);
                            string strTimeout = (string)req["timeout"];
                            Duration? timeout = null;
                            if (strTimeout != null) {
                                timeout = Duration.Parse(strTimeout);
                            }
                            List<string> moduleIDs = values.Select(x => x.Variable.Object.ModuleID).Distinct().ToList();
                            foreach (string moduleID in moduleIDs) {
                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                                module.ValidateVariableValuesOrThrow(moduleValues);
                            }

                            int maxBufferCount = 0;
                            Task<WriteResult>[] tasks = moduleIDs.Select(moduleID => {
                                ModuleState module = ModuleFromIdOrThrow(moduleID);
                                VariableValue[] moduleValues = values.Where(v => v.Variable.Object.ModuleID == moduleID).ToArray();
                                int count = module.UpdateVariableValues(moduleValues);
                                maxBufferCount = Math.Max(maxBufferCount, count);
                                return RestartOnExp(module, m => m.WriteVariables(info.Origin, moduleValues, timeout));
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

                            return Result_OK(WriteResult.FromResults(res));
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

                            IList<VTTQ> vttqs = await core.history.HistorianReadRaw(variable, tStart, tEnd, maxValues, bounding);
                            return Result_OK(vttqs);
                        }

                    case Req_HistorianCount: {

                            VariableRef variable = StdJson.ObjectFromJToken<VariableRef>(req["variable"]);
                            Timestamp tStart = Timestamp.FromISO8601((string)req["startInclusive"]);
                            Timestamp tEnd = Timestamp.FromISO8601((string)req["endInclusive"]);

                            long count = await core.history.HistorianCount(variable, tStart, tEnd);
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

        private Timestamp timeLastWriteVariablesThrottle = Timestamp.Empty;
        private Timestamp timeLastWriteVariablesSyncThrottle = Timestamp.Empty;

        internal void OnVariableValuesChanged(IList<VaribleValuePrev> origValues, Func<ObjectRef, ObjectRef?> parentMap) {

            if (terminating) return;
            IList<VaribleValuePrev> values = Compact(origValues);

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
                        var ignored = SendWebSocket(session.EventSocket, "{ \"event\": \"OnVariableValueChanged\", \"variables\": ", filtered);
                    }
                }
            }
        }

        private static IList<VaribleValuePrev> Compact(IList<VaribleValuePrev> values) {

            var groups = values.GroupBy(x => x.Value.Variable).ToArray();
            if (groups.Length == values.Count) {
                return values;
            }

            VaribleValuePrev[] resValues = new VaribleValuePrev[groups.Length];

            for (int i = 0; i < groups.Length; ++i) {

                IGrouping<VariableRef, VaribleValuePrev> group = groups[i];

                Timestamp minT = Timestamp.Max;
                Timestamp maxT = Timestamp.Empty;

                VaribleValuePrev minVal = null;
                VaribleValuePrev maxVal = null;

                foreach (VaribleValuePrev v in group) {
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
                resValues[i] = new VaribleValuePrev(maxVal.Value, minVal.PreviousValue);
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
                        var ignored = SendWebSocket(session.EventSocket, "{ \"event\": \"OnVariableHistoryChanged\", \"changes\": ", filtered);
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
                    var ignored = SendWebSocket(session.EventSocket, "{ \"event\": \"OnConfigChanged\", \"changedObjects\": ", filtered);
                }
            }
        }

        internal void OnAlarmOrEvent(AlarmOrEvent ae) {

            if (terminating) return;

            SessionInfo[] relevantSessions = sessions.Values.Where(s => s.AlarmsAndEventsEnabled && s.EventSocket != null && s.EventSocket.State == WebSocketState.Open && (int)s.MinSeverity <= (int)ae.Severity).ToArray();

            foreach (var session in relevantSessions) {
                var ignored = SendWebSocket(session.EventSocket, "{ \"event\": \"OnAlarmOrEvent\", \"alarmOrEvent\": ", ae);
            }
        }

        private readonly static Encoding UTF8_NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private async Task SendWebSocket(WebSocket socket, string msgStart, object content) {

            try {

                using (var stream = MemoryManager.GetMemoryStream("HandleClientRequests.SendWebSocket")) {
                    using (var writer = new StreamWriter(stream, UTF8_NoBOM, 1024, leaveOpen: true)) {
                        writer.Write(msgStart);
                        StdJson.ObjectToWriter(content, writer);
                        writer.Write("}");
                    }
                    byte[] bytes = stream.GetBuffer();
                    int count = (int)stream.Length;
                    var segment = new ArraySegment<byte>(bytes, 0, count);
                    try {
                        await socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    catch (Exception) { }
                }
            }
            catch (Exception exp) {
                logger.Warn("Exception in SendWebSocket: " + exp.Message);
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
            public Origin Origin { get; set; }
            public string Challenge { get; set; } = "";
            public string Password { get; set; } = "";
            public bool Valid { get; set; } = false;

            public WebSocket EventSocket { get; set; }
            public TaskCompletionSource<object> EventSocketTCS { get; set; }

            public Dictionary<ObjectRef, SubOptions> VariablesChangedEventTrees { get; private set; } = new Dictionary<ObjectRef, SubOptions>();
            public Dictionary<VariableRef, SubOptions> VariableRefs { get; private set; } = new Dictionary<VariableRef, SubOptions>();
            public HashSet<ObjectRef> VariablesHistoryChangedEventTrees { get; private set; } = new HashSet<ObjectRef>();
            public HashSet<VariableRef> VariableHistoryRefs { get; private set; } = new HashSet<VariableRef>();
            public HashSet<ObjectRef> ConfigChangeObjects { get; private set; } = new HashSet<ObjectRef>();
            public bool AlarmsAndEventsEnabled { get; set; } = false;
            public Severity MinSeverity { get; set; } = Severity.Info;
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
