using System;
using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator
{
    public class ReqDef
    {
        public int NumericID { get; private set; }
        public string HttpPath { get; private set; }
        public Type ReqType { get; private set; }

        private ReqDef() { }

        public static ReqDef Make<T>() where T : RequestBase, new() {
            T t = new T();
            int id = t.GetID();
            string path = t.GetPath();
            return new ReqDef() {
                NumericID = id,
                HttpPath = "/Mediator/" + path,
                ReqType = typeof(T),
            };
        }
    }

    public static class RequestDefinitions
    {
        public static readonly ReqDef Login = ReqDef.Make<LoginReq>();
        public static readonly ReqDef Auth = ReqDef.Make<AuthenticateReq>();
        public static readonly ReqDef ReadVariables = ReqDef.Make<ReadVariablesReq>();
        public static readonly ReqDef ReadVariablesIgnoreMissing = ReqDef.Make<ReadVariablesIgnoreMissingReq>();
        public static readonly ReqDef ReadVariablesSync = ReqDef.Make<ReadVariablesSyncReq>();
        public static readonly ReqDef ReadVariablesSyncIgnoreMissing = ReqDef.Make<ReadVariablesSyncIgnoreMissingReq>();
        public static readonly ReqDef WriteVariables = ReqDef.Make<WriteVariablesReq>();
        public static readonly ReqDef WriteVariablesIgnorMissing = ReqDef.Make<WriteVariablesIgnoreMissingReq>();
        public static readonly ReqDef WriteVariablesSync = ReqDef.Make<WriteVariablesSyncReq>();
        public static readonly ReqDef WriteVariablesSyncIgnoreMissing = ReqDef.Make<WriteVariablesSyncIgnoreMissingReq>();
        public static readonly ReqDef ReadAllVariablesOfObjectTree = ReqDef.Make<ReadAllVariablesOfObjectTreeReq>();
        public static readonly ReqDef GetModules = ReqDef.Make<GetModulesReq>();
        public static readonly ReqDef GetLocations = ReqDef.Make<GetLocationsReq>();
        public static readonly ReqDef GetLoginUser = ReqDef.Make<GetLoginUserReq>();
        public static readonly ReqDef GetRootObject = ReqDef.Make<GetRootObjectReq>();
        public static readonly ReqDef GetAllObjects = ReqDef.Make<GetAllObjectsReq>();
        public static readonly ReqDef GetAllObjectsOfType = ReqDef.Make<GetAllObjectsOfTypeReq>();
        public static readonly ReqDef GetObjectsByID = ReqDef.Make<GetObjectsByIDReq>();
        public static readonly ReqDef GetChildrenOfObjects = ReqDef.Make<GetChildrenOfObjectsReq>();
        public static readonly ReqDef GetAllObjectsWithVariablesOfType = ReqDef.Make<GetAllObjectsWithVariablesOfTypeReq>();
        public static readonly ReqDef GetObjectValuesByID = ReqDef.Make<GetObjectValuesByIDReq>();
        public static readonly ReqDef GetMemberValues = ReqDef.Make<GetMemberValuesReq>();
        public static readonly ReqDef GetParentOfObject = ReqDef.Make<GetParentOfObjectReq>();
        public static readonly ReqDef UpdateConfig = ReqDef.Make<UpdateConfigReq>();
        public static readonly ReqDef EnableVariableValueChangedEvents = ReqDef.Make<EnableVariableValueChangedEventsReq>();
        public static readonly ReqDef EnableVariableHistoryChangedEvents = ReqDef.Make<EnableVariableHistoryChangedEventsReq>();
        public static readonly ReqDef EnableConfigChangedEvents = ReqDef.Make<EnableConfigChangedEventsReq>();
        public static readonly ReqDef DisableChangeEvents = ReqDef.Make<DisableChangeEventsReq>();
        public static readonly ReqDef EnableAlarmsAndEvents = ReqDef.Make<EnableAlarmsAndEventsReq>();
        public static readonly ReqDef DisableAlarmsAndEvents = ReqDef.Make<DisableAlarmsAndEventsReq>();
        public static readonly ReqDef HistorianReadRaw = ReqDef.Make<HistorianReadRawReq>();
        public static readonly ReqDef HistorianCount = ReqDef.Make<HistorianCountReq>();
        public static readonly ReqDef HistorianDeleteInterval = ReqDef.Make<HistorianDeleteIntervalReq>();
        public static readonly ReqDef HistorianModify = ReqDef.Make<HistorianModifyReq>();
        public static readonly ReqDef HistorianDeleteAllVariablesOfObjectTree = ReqDef.Make<HistorianDeleteAllVariablesOfObjectTreeReq>();
        public static readonly ReqDef HistorianDeleteVariables = ReqDef.Make<HistorianDeleteVariablesReq>();
        public static readonly ReqDef HistorianGetLatestTimestampDB = ReqDef.Make<HistorianGetLatestTimestampDBReq>();
        public static readonly ReqDef CallMethod = ReqDef.Make<CallMethodReq>();
        public static readonly ReqDef BrowseObjectMemberValues = ReqDef.Make<BrowseObjectMemberValuesReq>();
        public static readonly ReqDef GetMetaInfos = ReqDef.Make<GetMetaInfosReq>();
        public static readonly ReqDef Ping = ReqDef.Make<PingReq>();
        public static readonly ReqDef EnableEventPing = ReqDef.Make<EnableEventPingReq>();
        public static readonly ReqDef Logout = ReqDef.Make<LogoutReq>();

        public static readonly ReadOnlyList<ReqDef> Definitions = new ReadOnlyList<ReqDef>(
            Login, Auth,
            ReadVariables, ReadVariablesIgnoreMissing, ReadVariablesSync, ReadVariablesSyncIgnoreMissing,
            WriteVariables, WriteVariablesIgnorMissing, WriteVariablesSync, WriteVariablesSyncIgnoreMissing,
            ReadAllVariablesOfObjectTree, GetModules, GetLocations, GetLoginUser, GetRootObject, GetAllObjects,
            GetAllObjectsOfType, GetObjectsByID, GetChildrenOfObjects, GetAllObjectsWithVariablesOfType,
            GetObjectValuesByID, GetMemberValues, GetParentOfObject, UpdateConfig,
            EnableVariableValueChangedEvents, EnableVariableHistoryChangedEvents, EnableConfigChangedEvents,
            DisableChangeEvents, EnableAlarmsAndEvents, DisableAlarmsAndEvents,
            HistorianReadRaw, HistorianCount, HistorianDeleteInterval, HistorianModify, HistorianDeleteAllVariablesOfObjectTree,
            HistorianDeleteVariables, HistorianGetLatestTimestampDB,
            CallMethod, BrowseObjectMemberValues, GetMetaInfos, Ping, EnableEventPing, Logout
        );
    }

    public abstract class RequestBase
    {
        [JsonProperty("session")]
        public string Session { get; set; }

        public abstract int GetID();
        public abstract string GetPath();
    }

    public class LoginReq : RequestBase
    {
        public const int ID = 1;

        public override int GetID() => ID;
        public override string GetPath() => "Login";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }

        [JsonProperty("login")]
        public string Login { get; set; }

        [JsonProperty("roles")]
        public string[] Roles { get; set; }
    }

    public class LoginResponse
    {
        [JsonProperty("session")]
        public string Session { get; set; }

        [JsonProperty("challenge")]
        public string Challenge { get; set; }
    }

    public class AuthenticateReq : RequestBase
    {
        public const int ID = 2;

        public override int GetID() => ID;
        public override string GetPath() => "Authenticate";

        [JsonProperty("hash")]
        public long Hash { get; set; }
    }

    public class AuthenticateResponse
    {
        [JsonProperty("session")]
        public string Session { get; set; }
    }

    public class ReadVariablesReq : RequestBase
    {
        public const int ID = 3;

        public override int GetID() => ID;
        public override string GetPath() => "ReadVariables";

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }

        [JsonProperty("bin")]
        public int BinaryMode { get; set; } = 0; // 0 = JSON, 1 = CompactBinary format
    }

    public class ReadVariablesIgnoreMissingReq : RequestBase
    {
        public const int ID = 4;

        public override int GetID() => ID;
        public override string GetPath() => "ReadVariablesIgnoreMissing";

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }
    }

    public class ReadVariablesSyncReq : RequestBase
    {
        public const int ID = 5;

        public override int GetID() => ID;
        public override string GetPath() => "ReadVariablesSync";

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }

        [JsonProperty("timeout")]
        public Duration? Timeout { get; set; }

        [JsonProperty("bin")]
        public int BinaryMode { get; set; } = 0; // 0 = JSON, 1 = CompactBinary format

        public bool ShouldSerializeTimeout() => Timeout.HasValue;
    }

    public class ReadVariablesSyncIgnoreMissingReq : RequestBase
    {
        public const int ID = 6;

        public override int GetID() => ID;
        public override string GetPath() => "ReadVariablesSyncIgnoreMissing";

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }

        [JsonProperty("timeout")]
        public Duration? Timeout { get; set; }

        public bool ShouldSerializeTimeout() => Timeout.HasValue;
    }

    public class WriteVariablesReq : RequestBase
    {
        public const int ID = 7;

        public override int GetID() => ID;
        public override string GetPath() => "WriteVariables";

        [JsonProperty("values")]
        public VariableValue[] Values { get; set; }
    }

    public class WriteVariablesIgnoreMissingReq : RequestBase
    {
        public const int ID = 8;

        public override int GetID() => ID;
        public override string GetPath() => "WriteVariablesIgnoreMissing";

        [JsonProperty("values")]
        public VariableValue[] Values { get; set; }
    }

    public class WriteVariablesSyncReq : RequestBase
    {
        public const int ID = 9;

        public override int GetID() => ID;
        public override string GetPath() => "WriteVariablesSync";

        [JsonProperty("values")]
        public VariableValue[] Values { get; set; }

        [JsonProperty("timeout")]
        public Duration? Timeout { get; set; }

        public bool ShouldSerializeTimeout() => Timeout.HasValue;
    }

    public class WriteVariablesSyncIgnoreMissingReq : RequestBase
    {
        public const int ID = 10;

        public override int GetID() => ID;
        public override string GetPath() => "WriteVariablesSyncIgnoreMissing";

        [JsonProperty("values")]
        public VariableValue[] Values { get; set; }

        [JsonProperty("timeout")]
        public Duration? Timeout { get; set; }

        public bool ShouldSerializeTimeout() => Timeout.HasValue;
    }

    public class ReadAllVariablesOfObjectTreeReq : RequestBase
    {
        public const int ID = 11;

        public override int GetID() => ID;
        public override string GetPath() => "ReadAllVariablesOfObjectTree";

        [JsonProperty("objectID")]
        public ObjectRef ObjectID { get; set; }
    }

    public class GetModulesReq : RequestBase
    {
        public const int ID = 12;

        public override int GetID() => ID;
        public override string GetPath() => "GetModules";
    }

    public class GetLocationsReq : RequestBase
    {
        public const int ID = 13;

        public override int GetID() => ID;
        public override string GetPath() => "GetLocations";
    }

    public class GetLoginUserReq : RequestBase
    {
        public const int ID = 14;

        public override int GetID() => ID;
        public override string GetPath() => "GetLoginUser";
    }

    public class GetRootObjectReq : RequestBase
    {
        public const int ID = 15;

        public override int GetID() => ID;
        public override string GetPath() => "GetRootObject";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }
    }

    public class GetAllObjectsReq : RequestBase
    {
        public const int ID = 16;

        public override int GetID() => ID;
        public override string GetPath() => "GetAllObjects";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }
    }

    public class GetAllObjectsOfTypeReq : RequestBase
    {
        public const int ID = 17;

        public override int GetID() => ID;
        public override string GetPath() => "GetAllObjectsOfType";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }

        [JsonProperty("className")]
        public string ClassName { get; set; }
    }

    public class GetObjectsByIDReq : RequestBase
    {
        public const int ID = 18;

        public override int GetID() => ID;
        public override string GetPath() => "GetObjectsByID";

        [JsonProperty("objectIDs")]
        public ObjectRef[] ObjectIDs { get; set; }
    }

    public class GetChildrenOfObjectsReq : RequestBase
    {
        public const int ID = 19;

        public override int GetID() => ID;
        public override string GetPath() => "GetChildrenOfObjects";

        [JsonProperty("objectIDs")]
        public ObjectRef[] ObjectIDs { get; set; }
    }

    public class GetAllObjectsWithVariablesOfTypeReq : RequestBase
    {
        public const int ID = 20;

        public override int GetID() => ID;
        public override string GetPath() => "GetAllObjectsWithVariablesOfType";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }

        [JsonProperty("types")]
        public DataType[] Types { get; set; }
    }

    public class GetObjectValuesByIDReq : RequestBase
    {
        public const int ID = 21;

        public override int GetID() => ID;
        public override string GetPath() => "GetObjectValuesByID";

        [JsonProperty("objectIDs")]
        public ObjectRef[] ObjectIDs { get; set; }
    }

    public class GetMemberValuesReq : RequestBase
    {
        public const int ID = 22;

        public override int GetID() => ID;
        public override string GetPath() => "GetMemberValues";

        [JsonProperty("member")]
        public MemberRef[] Member { get; set; }
    }

    public class GetParentOfObjectReq : RequestBase
    {
        public const int ID = 23;

        public override int GetID() => ID;
        public override string GetPath() => "GetParentOfObject";

        [JsonProperty("objectID")]
        public ObjectRef ObjectID { get; set; }
    }

    public class UpdateConfigReq : RequestBase
    {
        public const int ID = 24;

        public override int GetID() => ID;
        public override string GetPath() => "UpdateConfig";

        [JsonProperty("updateOrDeleteObjects")]
        public ObjectValue[] UpdateOrDeleteObjects { get; set; }

        [JsonProperty("updateOrDeleteMembers")]
        public MemberValue[] UpdateOrDeleteMembers { get; set; }

        [JsonProperty("addArrayElements")]
        public AddArrayElement[] AddArrayElements { get; set; }
    }

    public class EnableVariableValueChangedEventsReq : RequestBase
    {
        public const int ID = 25;

        public override int GetID() => ID;
        public override string GetPath() => "EnableVariableValueChangedEvents";

        [JsonProperty("options")]
        public SubOptions Options { get; set; }

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }

        [JsonProperty("idsOfEnabledTreeRoots")]
        public ObjectRef[] IdsOfEnabledTreeRoots { get; set; }
    }

    public class EnableVariableHistoryChangedEventsReq : RequestBase
    {
        public const int ID = 26;

        public override int GetID() => ID;
        public override string GetPath() => "EnableVariableHistoryChangedEvents";

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }

        [JsonProperty("idsOfEnabledTreeRoots")]
        public ObjectRef[] IdsOfEnabledTreeRoots { get; set; }
    }

    public class EnableConfigChangedEventsReq : RequestBase
    {
        public const int ID = 27;

        public override int GetID() => ID;
        public override string GetPath() => "EnableConfigChangedEvents";

        [JsonProperty("objects")]
        public ObjectRef[] Objects { get; set; }
    }

    public class DisableChangeEventsReq : RequestBase
    {
        public const int ID = 28;

        public override int GetID() => ID;
        public override string GetPath() => "DisableChangeEvents";

        [JsonProperty("disableVarValueChanges")]
        public bool DisableVarValueChanges { get; set; }

        [JsonProperty("disableVarHistoryChanges")]
        public bool DisableVarHistoryChanges { get; set; }

        [JsonProperty("disableConfigChanges")]
        public bool DisableConfigChanges { get; set; }
    }

    public class EnableAlarmsAndEventsReq : RequestBase
    {
        public const int ID = 29;

        public override int GetID() => ID;
        public override string GetPath() => "EnableAlarmsAndEvents";

        [JsonProperty("minSeverity")]
        public Severity MinSeverity { get; set; }
    }

    public class DisableAlarmsAndEventsReq : RequestBase
    {
        public const int ID = 30;

        public override int GetID() => ID;
        public override string GetPath() => "DisableAlarmsAndEvents";
    }

    public class HistorianReadRawReq : RequestBase
    {
        public const int ID = 31;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianReadRaw";

        [JsonProperty("variable")]
        public VariableRef Variable { get; set; }

        [JsonProperty("startInclusive")]
        public Timestamp StartInclusive { get; set; }

        [JsonProperty("endInclusive")]
        public Timestamp EndInclusive { get; set; }

        [JsonProperty("maxValues")]
        public int MaxValues { get; set; }

        [JsonProperty("bounding")]
        public BoundingMethod Bounding { get; set; }

        [JsonProperty("filter")]
        public QualityFilter Filter { get; set; } = QualityFilter.ExcludeNone;

        [JsonProperty("bin")]
        public int BinaryMode { get; set; } = 0; // 0 = JSON, 1 = CompactBinary format
    }

    public class HistorianCountReq : RequestBase
    {
        public const int ID = 32;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianCount";

        [JsonProperty("variable")]
        public VariableRef Variable { get; set; }

        [JsonProperty("startInclusive")]
        public Timestamp StartInclusive { get; set; }

        [JsonProperty("endInclusive")]
        public Timestamp EndInclusive { get; set; }

        [JsonProperty("filter")]
        public QualityFilter Filter { get; set; } = QualityFilter.ExcludeNone;
    }

    public class HistorianDeleteIntervalReq : RequestBase
    {
        public const int ID = 33;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianDeleteInterval";

        [JsonProperty("variable")]
        public VariableRef Variable { get; set; }

        [JsonProperty("startInclusive")]
        public Timestamp StartInclusive { get; set; }

        [JsonProperty("endInclusive")]
        public Timestamp EndInclusive { get; set; }
    }

    public class HistorianModifyReq : RequestBase
    {
        public const int ID = 34;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianModify";

        [JsonProperty("variable")]
        public VariableRef Variable { get; set; }

        [JsonProperty("data")]
        public VTQ[] Data { get; set; }

        [JsonProperty("mode")]
        public ModifyMode Mode { get; set; }
    }

    public class HistorianDeleteAllVariablesOfObjectTreeReq : RequestBase
    {
        public const int ID = 35;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianDeleteAllVariablesOfObjectTree";

        [JsonProperty("objectID")]
        public ObjectRef ObjectID { get; set; }
    }

    public class HistorianDeleteVariablesReq : RequestBase
    {
        public const int ID = 36;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianDeleteVariables";

        [JsonProperty("variables")]
        public VariableRef[] Variables { get; set; }
    }

    public class HistorianGetLatestTimestampDBReq : RequestBase
    {
        public const int ID = 37;

        public override int GetID() => ID;
        public override string GetPath() => "HistorianGetLatestTimestampDB";

        [JsonProperty("variable")]
        public VariableRef Variable { get; set; }

        [JsonProperty("startInclusive")]
        public Timestamp StartInclusive { get; set; }

        [JsonProperty("endInclusive")]
        public Timestamp EndInclusive { get; set; }
    }

    public class CallMethodReq : RequestBase
    {
        public const int ID = 38;

        public override int GetID() => ID;
        public override string GetPath() => "CallMethod";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }

        [JsonProperty("methodName")]
        public string MethodName { get; set; }

        [JsonProperty("parameters")]
        public NamedValue[] Parameters { get; set; }
    }

    public class BrowseObjectMemberValuesReq : RequestBase
    {
        public const int ID = 39;

        public override int GetID() => ID;
        public override string GetPath() => "BrowseObjectMemberValues";

        [JsonProperty("member")]
        public MemberRef Member { get; set; }

        [JsonProperty("continueID")]
        public int? ContinueID { get; set; }
    }

    public class GetMetaInfosReq : RequestBase
    {
        public const int ID = 40;

        public override int GetID() => ID;
        public override string GetPath() => "GetMetaInfos";

        [JsonProperty("moduleID")]
        public string ModuleID { get; set; }

        [JsonProperty("continueID")]
        public int? ContinueID { get; set; }
    }

    public class PingReq : RequestBase
    {
        public const int ID = 41;

        public override int GetID() => ID;
        public override string GetPath() => "Ping";
    }

    public class EnableEventPingReq : RequestBase
    {
        public const int ID = 42;

        public override int GetID() => ID;
        public override string GetPath() => "EnableEventPing";
    }

    public class LogoutReq : RequestBase
    {
        public const int ID = 43;

        public override int GetID() => ID;
        public override string GetPath() => "Logout";
    }


    //////////////////////////////////////////////////////////////

    public class ErrorResult
    {
        [JsonProperty("error")]
        public string Error { get; set; }
    }

    public class EventContent
    {
        [JsonProperty("event")]
        public string Event { get; set; }

        [JsonProperty("variables")]
        public List<VariableValue> Variables { get; set; }

        [JsonProperty("changes")]
        public List<HistoryChange> Changes { get; set; }

        [JsonProperty("changedObjects")]
        public List<ObjectRef> ChangedObjects { get; set; }

        [JsonProperty("events")]
        public List<AlarmOrEvent> Events { get; set; }

        public bool ShouldSerializeVariables() => Variables != null;
        public bool ShouldSerializeChanges() => Changes != null;
        public bool ShouldSerializeChangedObjects() => ChangedObjects != null;
        public bool ShouldSerializeEvents() => Events != null;
    }
}
