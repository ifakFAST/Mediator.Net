using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    public class ClosedConnection : Connection
    {
        public override bool IsClosed => true;

        public override string UserRole => "";

        public override Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {
            throw new InvalidOperationException("BrowseObjectMemberValues on closed connection");
        }

        public override Task<DataValue> CallMethod(string moduleID, string methodName, params NamedValue[] parameters) {
            throw new InvalidOperationException("CallMethod on closed connection");
        }

        public override Task<bool[]> CanUpdateConfig(MemberRef[] members) {
            throw new InvalidOperationException("CanUpdateConfig on closed connection");
        }

        public override Task Close() {
            return Task.FromResult(true);
        }

        public override Task DisableAlarmsAndEvents() {
            throw new InvalidOperationException("DisableAlarmsAndEvents on closed connection");
        }

        public override Task DisableChangeEvents(bool disableVarValueChanges = true, bool disableVarHistoryChanges = true, bool disableConfigChanges = true) {
            throw new InvalidOperationException("DisableChangeEvents on closed connection");
        }

        public override void Dispose() {

        }

        public override Task EnableAlarmsAndEvents(Severity minSeverity = Severity.Info) {
            throw new InvalidOperationException("EnableAlarmsAndEvents on closed connection");
        }

        public override Task EnableConfigChangedEvents(params ObjectRef[] objects) {
            throw new InvalidOperationException("EnableConfigChangedEvents on closed connection");
        }

        public override Task EnableVariableHistoryChangedEvents(params ObjectRef[] idsOfEnabledTreeRoots) {
            throw new InvalidOperationException("EnableVariableHistoryChangedEvents on closed connection");
        }

        public override Task EnableVariableHistoryChangedEvents(params VariableRef[] variables) {
            throw new InvalidOperationException("EnableVariableHistoryChangedEvents on closed connection");
        }

        public override Task EnableVariableValueChangedEvents(SubOptions options, params ObjectRef[] idsOfEnabledTreeRoots) {
            throw new InvalidOperationException("EnableVariableValueChangedEvents on closed connection");
        }

        public override Task EnableVariableValueChangedEvents(SubOptions options, params VariableRef[] variables) {
            throw new InvalidOperationException("EnableVariableValueChangedEvents on closed connection");
        }

        public override Task<List<ObjectInfo>> GetAllObjects(string moduleID) {
            throw new InvalidOperationException("GetAllObjects on closed connection");
        }

        public override Task<List<ObjectInfo>> GetAllObjectsOfType(string moduleID, string className) {
            throw new InvalidOperationException("GetAllObjectsOfType on closed connection");
        }

        public override Task<List<ObjectInfo>> GetAllObjectsWithVariablesOfType(string moduleID, params DataType[] types) {
            throw new InvalidOperationException("GetAllObjectsWithVariablesOfType on closed connection");
        }

        public override Task<List<ObjectInfo>> GetChildrenOfObjects(params ObjectRef[] objectIDs) {
            throw new InvalidOperationException("GetChildrenOfObjects on closed connection");
        }

        public override Task<List<LocationInfo>> GetLocations() {
            throw new InvalidOperationException("GetLocations on closed connection");
        }

        public override Task<User> GetLoginUser() {
            throw new InvalidOperationException("GetLoginUser on closed connection");
        }

        public override Task<List<MemberValue>> GetMemberValues(MemberRef[] member) {
            throw new InvalidOperationException("GetMemberValues on closed connection");
        }

        public override Task<MetaInfos> GetMetaInfos(string moduleID) {
            throw new InvalidOperationException("GetMetaInfos on closed connection");
        }

        public override Task<List<ModuleInfo>> GetModules() {
            throw new InvalidOperationException("GetModules on closed connection");
        }

        public override Task<List<ObjectInfo>> GetObjectsByID(params ObjectRef[] objectIDs) {
            throw new InvalidOperationException("GetObjectsByID on closed connection");
        }

        public override Task<List<ObjectValue>> GetObjectValuesByID(params ObjectRef[] objectIDs) {
            throw new InvalidOperationException("GetObjectValuesByID on closed connection");
        }

        public override Task<ObjectValue> GetParentOfObject(ObjectRef objectID) {
            throw new InvalidOperationException("GetParentOfObject on closed connection");
        }

        public override Task<ObjectInfo> GetRootObject(string moduleID) {
            throw new InvalidOperationException("GetRootObject on closed connection");
        }

        public override Task<long> HistorianCount(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter = QualityFilter.ExcludeNone) {
            throw new InvalidOperationException("HistorianCount on closed connection");
        }

        public override Task HistorianDeleteAllVariablesOfObjectTree(ObjectRef objectID) {
            throw new InvalidOperationException("HistorianDeleteAllVariablesOfObjectTree on closed connection");
        }

        public override Task<long> HistorianDeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
            throw new InvalidOperationException("HistorianDeleteAllVariablesOfObjectTree on closed connection");
        }

        public override Task HistorianDeleteVariables(params VariableRef[] variables) {
            throw new InvalidOperationException("HistorianDeleteVariables on closed connection");
        }

        public override Task<VTTQ?> HistorianGetLatestTimestampDB(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
            throw new InvalidOperationException("HistorianGetLatestTimestampDB on closed connection");
        }

        public override Task HistorianModify(VariableRef variable, ModifyMode mode, params VTQ[] data) {
            throw new InvalidOperationException("HistorianModify on closed connection");
        }

        public override Task<List<VTTQ>> HistorianReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter = QualityFilter.ExcludeNone) {
            throw new InvalidOperationException("HistorianReadRaw on closed connection");
        }

        public override Task Ping() {
            throw new InvalidOperationException("Ping on closed connection");
        }

        public override Task<List<VariableValue>> ReadAllVariablesOfObjectTree(ObjectRef objectID) {
            throw new InvalidOperationException("ReadAllVariablesOfObjectTree on closed connection");
        }

        public override Task<List<VTQ>> ReadVariables(List<VariableRef> variables) {
            throw new InvalidOperationException("ReadVariables on closed connection");
        }

        public override Task<List<VariableValue>> ReadVariablesIgnoreMissing(List<VariableRef> variables) {
            throw new InvalidOperationException("ReadVariablesIgnoreMissing on closed connection");
        }

        public override Task<List<VTQ>> ReadVariablesSync(List<VariableRef> variables, Duration? timeout = null) {
            throw new InvalidOperationException("ReadVariablesSync on closed connection");
        }

        public override Task<List<VariableValue>> ReadVariablesSyncIgnoreMissing(List<VariableRef> variables, Duration? timeout = null) {
            throw new InvalidOperationException("ReadVariablesSyncIgnoreMissing on closed connection");
        }

        public override Task UpdateConfig(ObjectValue[]? updateOrDeleteObjects, MemberValue[]? updateOrDeleteMembers, AddArrayElement[]? addArrayElements) {
            throw new InvalidOperationException("UpdateConfig on closed connection");
        }

        public override Task WriteVariables(List<VariableValue> values) {
            throw new InvalidOperationException("WriteVariables on closed connection");
        }

        public override Task<WriteResult> WriteVariablesIgnoreMissing(List<VariableValue> values) {
            throw new InvalidOperationException("WriteVariablesIgnoreMissing on closed connection");
        }

        public override Task<WriteResult> WriteVariablesSync(List<VariableValue> values, Duration? timeout = null) {
            throw new InvalidOperationException("WriteVariablesSync on closed connection");
        }

        public override Task<WriteResult> WriteVariablesSyncIgnoreMissing(List<VariableValue> values, Duration? timeout = null) {
            throw new InvalidOperationException("WriteVariablesSyncIgnoreMissing on closed connection");
        }
    }
}
