// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;
using ModuleInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ModuleInfo>;
using LocationInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.LocationInfo>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;
using ObjectValues = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectValue>;
using MemberValues = System.Collections.Generic.List<Ifak.Fast.Mediator.MemberValue>;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using VariableRefs = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableRef>;
using System.Xml.Linq;
using System.Xml.Schema;
using System.Xml;
using System.Linq;

namespace Ifak.Fast.Mediator
{
    public abstract class Connection : IDisposable
    {
        /// <summary>
        /// Returns the user role of the user that is logged in via this connection.
        /// </summary>
        public abstract string UserRole { get; }

        /// <summary>
        /// Close the connection. Never throws an exception.
        /// </summary>
        public abstract Task Close();

        /// <summary>
        /// Returns true when the connection is closed, false otherwise.
        /// </summary>
        public abstract bool IsClosed { get; }

        /// <summary>
        /// Ping the connection in order to keep the session alive.
        /// </summary>
        public abstract Task Ping();

        /// <summary>
        /// Returns the modules that the running Mediator instance is composed of (can not change while Mediator is running).
        /// </summary>
        public abstract Task<ModuleInfos> GetModules();

        /// <summary>
        /// Returns the global list of locations that describes the hierarchy of locations of a plant or facility
        /// </summary>
        public abstract Task<LocationInfos> GetLocations();

        /// <summary>
        /// Returns the description of the user that is logged in via this connection.
        /// If the connection was created by a module via ConnectWithModuleLogin(), an exception will be thrown.
        /// </summary>
        public abstract Task<User> GetLoginUser();

        /// <summary>
        /// Returns the root object of a specific module.
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        public abstract Task<ObjectInfo> GetRootObject(string moduleID);

        /// <summary>
        /// Returns all objects of a specific module.
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        public abstract Task<ObjectInfos> GetAllObjects(string moduleID);

        /// <summary>
        /// Returns all objects of a module that have a specific type/class.
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        /// <param name="className">The class/type name</param>
        public abstract Task<ObjectInfos> GetAllObjectsOfType(string moduleID, string className);

        /// <summary>
        /// Returns an object by id (ObjectRef)
        /// </summary>
        /// <param name="objectID">The object id</param>
        public virtual async Task<ObjectInfo> GetObjectByID(ObjectRef objectID) {
            ObjectInfos objects = await GetObjectsByID(objectID);
            return objects[0];
        }

        /// <summary>
        /// Returns objects by id.
        /// </summary>
        /// <param name="objectIDs">The object ids</param>
        public virtual Task<ObjectInfos> GetObjectsByID(params ObjectRef[] objectIDs) {
            return GetObjectsByID(objectIDs, ignoreMissing: false);
        }

        /// <summary>
        /// Returns objects by id.
        /// </summary>
        /// <param name="objectIDs">The object ids</param>
        /// <param name="ignoreMissing">If true, missing objects will be ignored, otherwise an exception will be thrown</param>
        public abstract Task<ObjectInfos> GetObjectsByID(ObjectRef[] objectIDs, bool ignoreMissing);

        /// <summary>
        /// Returns all objects that are direct children of a specific object
        /// </summary>
        /// <param name="objectID">The id of the parent object</param>
        public virtual async Task<ObjectInfos> GetChildrenOfObject(ObjectRef objectID) {
            return await GetChildrenOfObjects(objectID);
        }

        /// <summary>
        /// Returns all objects that are direct children of specific objects
        /// </summary>
        /// <param name="objectIDs">The ids of the parent objects</param>
        public abstract Task<ObjectInfos> GetChildrenOfObjects(params ObjectRef[] objectIDs);

        /// <summary>
        /// Returns all objects that are children (at any depth) of specific objects
        /// and that have one of the specified class names, e.g. "DataItem" or "Signal"
        /// </summary>
        public abstract Task<ObjectInfos> GetChildrenOfObjectsRecursive(ObjectRef[] objectIDs, string[] classNames);

        /// <summary>
        /// Returns all objects of a module that have at least one variable of a given type
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        /// <param name="types">The required data type of the variables</param>
        public abstract Task<ObjectInfos> GetAllObjectsWithVariablesOfType(string moduleID, params DataType[] types);

        /// <summary>
        /// Returns an entire object by id.
        /// </summary>
        /// <param name="objectID">The id of the object to return</param>
        public virtual async Task<ObjectValue> GetObjectValueByID(ObjectRef objectID) {
            ObjectValues res = await GetObjectValuesByID(objectID);
            return res[0];
        }

        /// <summary>
        /// Returns entire objects by id.
        /// </summary>
        /// <param name="objectIDs">The ids of the objects to return</param>
        public virtual Task<ObjectValues> GetObjectValuesByID(params ObjectRef[] objectIDs) {
            return GetObjectValuesByID(objectIDs, ignoreMissing: false);
        }

        /// <summary>
        /// Returns entire objects by id.
        /// </summary>
        /// <param name="objectIDs">The ids of the objects to return</param>
        /// <param name="ignoreMissing">If true, missing objects will be ignored, otherwise an exception will be thrown</param>
        public abstract Task<ObjectValues> GetObjectValuesByID(ObjectRef[] objectIDs, bool ignoreMissing);

        /// <summary>
        /// Returns the value of an object member.
        /// </summary>
        /// <param name="objectID">The id of the object</param>
        /// <param name="memberName">The name of the member to return</param>
        public virtual async Task<MemberValue> GetMemberValue(ObjectRef objectID, string memberName) {
            MemberValues values = await GetMemberValues(new MemberRef[] { new MemberRef(objectID, memberName) });
            return values[0];
        }

        /// <summary>
        /// Returns the value of an object member.
        /// </summary>
        /// <param name="member">The reference to the member to return</param>
        /// <returns></returns>
        public virtual async Task<MemberValue> GetMemberValue(MemberRef member) {
            MemberValues values = await GetMemberValues(new MemberRef[] { member });
            return values[0];
        }

        /// <summary>
        /// Returns the values of object members.
        /// </summary>
        /// <param name="member">The references to members to return</param>
        /// <param name="ignoreMissing">If true, missing members will be ignored, otherwise an exception will be thrown for non-existing objects</param>
        public abstract Task<MemberValues> GetMemberValues(MemberRef[] member, bool ignoreMissing = false);

        /// <summary>
        /// Returns the entire object value of the parent object for a given object id.
        /// </summary>
        /// <param name="objectID">The id of the object</param>
        public abstract Task<ObjectValue> GetParentOfObject(ObjectRef objectID);

        /// <summary>
        /// Returns meta information about all objects and types (struct and enums) of
        /// the configuration model of a module.
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        public abstract Task<MetaInfos> GetMetaInfos(string moduleID);

        #region Eventing

        /// <summary>
        /// Enables receiving alarms and events that are send by modules or Mediator core.
        /// The alarms and events can be received by <see cref="EventListener.OnAlarmOrEvents"/>.
        /// </summary>
        /// <param name="minSeverity">The minimum severity of the alarms or events to receive</param>
        public abstract Task EnableAlarmsAndEvents(Severity minSeverity = Severity.Info);

        /// <summary>
        /// Disables any further reception of alarms and events.
        /// </summary>
        public abstract Task DisableAlarmsAndEvents();

        /// <summary>
        /// Enables receiving notifications about changes of specific objects.
        /// The notifications are received by <see cref="EventListener.OnConfigChanged"/>.
        /// </summary>
        /// <remarks>
        /// The <see cref="EventListener.OnConfigChanged"/> method will only receive object references
        /// that have previously been registered. An object is considered changed when any of its parents
        /// or any of its members (including child objects) have been changed.
        /// </remarks>
        /// <param name="objects">The object references to register for changes</param>
        public abstract Task EnableConfigChangedEvents(params ObjectRef[] objects);

        /// <summary>
        /// Enables receiving notifications about value changes of all variables contained in the set
        /// of objects as specified by the given object tree roots. The notifications are received by
        /// <see cref="EventListener.OnVariableValueChanged"/>.
        /// </summary>
        /// <param name="options">Specifies what kind of changes to listen for and whether the new value
        /// will be sent in the notification</param>
        /// <param name="idsOfEnabledTreeRoots">The roots of object trees to register</param>
        public abstract Task EnableVariableValueChangedEvents(SubOptions options, params ObjectRef[] idsOfEnabledTreeRoots);

        /// <summary>
        /// Enables receiving notifications about value changes of specific variables.
        /// The notifications are received by <see cref="EventListener.OnVariableValueChanged"/>.
        /// </summary>
        /// <param name="options">Specifies what kind of changes to listen for and whether the new value will be sent in the notification</param>
        /// <param name="variables">The variable references to register for changes</param>
        public abstract Task EnableVariableValueChangedEvents(SubOptions options, params VariableRef[] variables);

        /// <summary>
        /// Enables receiving notifications about changes to the history of all variables contained in the set
        /// of objects as specified by the given object tree roots. The notifications are received by
        /// <see cref="EventListener.OnVariableHistoryChanged"/>.
        /// </summary>
        /// <param name="idsOfEnabledTreeRoots">The roots of object trees to register</param>
        public abstract Task EnableVariableHistoryChangedEvents(params ObjectRef[] idsOfEnabledTreeRoots);

        /// <summary>
        /// Enables receiving notifications about changes to the history of specific variables.
        /// The notifications are received by <see cref="EventListener.OnVariableHistoryChanged"/>.
        /// </summary>
        /// <param name="variables">The variable references to register for changes</param>
        public abstract Task EnableVariableHistoryChangedEvents(params VariableRef[] variables);

        /// <summary>
        /// Disables any further reception of notifications regarding variable value changes, variable history changes or object changes.
        /// </summary>
        /// <param name="disableVarValueChanges">Specifies whether to disable variable value changes</param>
        /// <param name="disableVarHistoryChanges">Specifies whether to disable variable history changes</param>
        /// <param name="disableConfigChanges">Specifies whether to disable object changes</param>
        public abstract Task DisableChangeEvents(bool disableVarValueChanges = true, bool disableVarHistoryChanges = true, bool disableConfigChanges = true);

        #endregion

        #region UpdateConfig

        /// <summary>
        /// Returns which MemberRefs can be updated by the current user/role. For non-existing members, false is returned.
        /// </summary>
        public abstract Task<bool[]> CanUpdateConfig(MemberRef[] members);

        /// <summary>
        /// Returns whether the given MemberRef can be updated by the current user/role.
        /// </summary>
        public virtual async Task<bool> CanUpdateConfig(MemberRef member) {
            return (await CanUpdateConfig(new MemberRef[] { member }))[0];
        }

        /// <summary>
        /// Updates the configuration (objects) of one or several modules by updating or deleting specific objects.
        /// </summary>
        /// <remarks>
        /// For deleting an object use an empty DataValue, e.g. <c>UpdateConfig(ObjectValue.Make("ModuleID", "ObjID", DataValue.Empty))</c>
        /// </remarks>
        /// <param name="updateOrDeleteObjects">The object values for update or delete</param>
        public virtual Task UpdateConfig(params ObjectValue[] updateOrDeleteObjects) {
            return UpdateConfig(updateOrDeleteObjects, null, null);
        }

        /// <summary>
        /// Updates the configuration (objects) of one or several modules by updating or deleting specific object members.
        /// </summary>
        /// <remarks>
        /// For deleting an (optional) member use an empty DataValue, e.g. <c>UpdateConfig(MemberValue.Make("ModuleID", "ObjID", "Member", DataValue.Empty))</c>
        /// </remarks>
        /// <param name="updateOrDeleteMembers">The member values for update or delete</param>
        public virtual Task UpdateConfig(params MemberValue[] updateOrDeleteMembers) {
            return UpdateConfig(null, updateOrDeleteMembers, null);
        }

        /// <summary>
        /// Updates the configuration (objects) of one or several modules by adding a value to array members.
        /// </summary>
        /// <param name="addArrayElements">The values to add to an array member</param>
        public virtual Task UpdateConfig(params AddArrayElement[] addArrayElements) {
            return UpdateConfig(null, null, addArrayElements);
        }

        /// <summary>
        /// Updates the configuration (objects) of one or several modules by updating or deleting specific objects,
        /// updating or deleting specific object members, and/or adding a value to array members.
        /// </summary>
        /// <param name="updateOrDeleteObjects">The object values for update or delete</param>
        /// <param name="updateOrDeleteMembers">The member values for update or delete</param>
        /// <param name="addArrayElements">The values to add to an array member</param>
        public abstract Task UpdateConfig(ObjectValue[]? updateOrDeleteObjects, MemberValue[]? updateOrDeleteMembers, AddArrayElement[]? addArrayElements);

        #endregion

        #region Variables

        /// <summary>
        /// Reads the current value of a variable from the Mediator cache.
        /// Throws an exception if the variable does not exist.
        /// </summary>
        /// <param name="objectID">The object containing the variable</param>
        /// <param name="variableName">The name of the variable to read</param>
        /// <returns></returns>
        public virtual async Task<VTQ> ReadVariable(ObjectRef objectID, string variableName) {
            VTQs tmp = await ReadVariables(new VariableRefs { VariableRef.Make(objectID, variableName) });
            return tmp[0];
        }

        /// <summary>
        /// Reads the current value of a variable from the Mediator cache.
        /// Throws an exception if the variable does not exist.
        /// </summary>
        /// <param name="variable">The variable to read</param>
        public virtual async Task<VTQ> ReadVariable(VariableRef variable) {
            VTQs tmp = await ReadVariables(new VariableRefs { variable });
            return tmp[0];
        }

        /// <summary>
        /// Reads the current value of variables from the Mediator cache.
        /// Throws an exception if any of the variables does not exist.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        public abstract Task<VTQs> ReadVariables(VariableRefs variables);

        /// <summary>
        /// Reads the current value of variables from the Mediator cache.
        /// If any of the variables does not exist, it will be excluded from the result.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        public abstract Task<VariableValues> ReadVariablesIgnoreMissing(VariableRefs variables);

        /// <summary>
        /// Reads the current value of a variable directly from the containing module.
        /// Throws an exception if the variable does not exist or the timeout expires.
        /// </summary>
        /// <param name="objectID">The object containing the variable</param>
        /// <param name="variableName">The name of the variable to read</param>
        /// <param name="timeout">Optional timeout</param>
        public virtual Task<VTQ> ReadVariableSync(ObjectRef objectID, string variableName, Duration? timeout = null) {
            return ReadVariableSync(VariableRef.Make(objectID, variableName), timeout);
        }

        /// <summary>
        /// Reads the current value of a variable directly from the containing module.
        /// Throws an exception if the variable does not exist or the timeout expires.
        /// </summary>
        /// <param name="variable">The variable to read</param>
        /// <param name="timeout">Optional timeout</param>
        public virtual async Task<VTQ> ReadVariableSync(VariableRef variable, Duration? timeout = null) {
            VTQs tmp = await ReadVariablesSync(new VariableRefs { variable }, timeout);
            return tmp[0];
        }

        /// <summary>
        /// Reads the current value of variables directly from the containing module.
        /// Throws an exception if one of the variables does not exist or the timeout expires.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        /// <param name="timeout">Optional timeout</param>
        /// <returns></returns>
        public abstract Task<VTQs> ReadVariablesSync(VariableRefs variables, Duration? timeout = null);

        /// <summary>
        /// Reads the current value of variables directly from the containing module.
        /// Throws an exception if the timeout expires.
        /// Variables, that do not exist, are excluded from the result.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        /// <param name="timeout">Optional timeout</param>
        /// <returns></returns>
        public abstract Task<VariableValues> ReadVariablesSyncIgnoreMissing(VariableRefs variables, Duration? timeout = null);

        /// <summary>
        /// Reads the current value of all variables of all objects in the tree of objects defined by the given object reference.
        /// </summary>
        /// <param name="objectID">The object defining the root of the object tree</param>
        public abstract Task<VariableValues> ReadAllVariablesOfObjectTree(ObjectRef objectID);

        /// <summary>
        /// Reads the current value of all variables of all objects in the tree of objects defined by the given object references.
        /// </summary>
        /// <param name="objects">The objects defining the roots of the object trees</param>
        public async Task<VariableValues> ReadAllVariablesOfObjectTrees(IEnumerable<ObjectRef> objects) {
            var result = new VariableValues();
            foreach (var obj in objects) {
                var vals = await ReadAllVariablesOfObjectTree(obj);
                result.AddRange(vals);
            }
            return result;
        }

        /// <summary>
        /// Writes a new value to a variable without waiting for the receiving module to complete the write request.
        /// An exception is thrown when the variable does not exist.
        /// </summary>
        /// <param name="objectID">The object containing the variable</param>
        /// <param name="variableName">The name of the variable to write</param>
        /// <param name="value">The new value</param>
        public virtual async Task WriteVariable(ObjectRef objectID, string variableName, VTQ value) {
            await WriteVariables(new VariableValues { VariableValue.Make(objectID, variableName, value) });
        }

        /// <summary>
        /// Writes a new value to a variable without waiting for the receiving module to complete the write request.
        /// An exception is thrown when the variable does not exist.
        /// </summary>
        /// <param name="variable">The reference to the variable to write</param>
        /// <param name="value">The new value</param>
        public virtual async Task WriteVariable(VariableRef variable, VTQ value) {
            await WriteVariables(new VariableValues { VariableValue.Make(variable, value) });
        }

        /// <summary>
        /// Writes new values to variables without waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, an exception is thrown and no value is written.
        /// </summary>
        /// <param name="values">The new variable values</param>
        public abstract Task WriteVariables(VariableValues values);

        /// <summary>
        /// Writes new values to variables without waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, it is ignored and a corresponding VariableError is returned
        /// for each missing variable.
        /// </summary>
        /// <param name="values">The new variable values</param>
        public abstract Task<WriteResult> WriteVariablesIgnoreMissing(VariableValues values);

        /// <summary>
        /// Writes a new value to a variable waiting for the receiving module to complete the write request.
        /// An exception is thrown when the variable does not exist. Check the return value to verify whether
        /// the receiving module successfully processed the write request.
        /// </summary>
        /// <param name="objectID">The object containing the variable</param>
        /// <param name="variableName">The name of the variable to write</param>
        /// <param name="value">The new value</param>
        /// <param name="timeout">Optional timeout</param>
        public virtual async Task<Result> WriteVariableSync(ObjectRef objectID, string variableName, VTQ value, Duration? timeout = null) {
            WriteResult res = await WriteVariablesSync(new VariableValues { VariableValue.Make(objectID, variableName, value) }, timeout);
            if (res.Failed()) return Result.Failure(res.FailedVariables![0].Error);
            return Result.OK;
        }

        /// <summary>
        /// Writes new values to variables waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, an exception is thrown and no value is written.
        /// Check the return value to verify whether the receiving module successfully processed the write requests.
        /// </summary>
        /// <param name="values">The new variable values</param>
        /// <param name="timeout">Optional timeout</param>
        public abstract Task<WriteResult> WriteVariablesSync(VariableValues values, Duration? timeout = null);

        /// <summary>
        /// Writes new values to variables waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, it is ignored and a corresponding VariableError is returned
        /// for each missing variable.
        /// Check the return value to verify whether the receiving module successfully processed the write requests.
        /// </summary>
        /// <param name="values">The new variable values</param>
        /// <param name="timeout">Optional timeout</param>
        public abstract Task<WriteResult> WriteVariablesSyncIgnoreMissing(VariableValues values, Duration? timeout = null);

        #endregion

        #region History

        /// <summary>
        /// Reads the history of a variable in a certain time interval.
        /// </summary>
        /// <param name="variable">The variable to read</param>
        /// <param name="startInclusive">The start of the time interval (inclusive)</param>
        /// <param name="endInclusive">The end of the time interval (inclusive)</param>
        /// <param name="maxValues">The maximum number of data points to return</param>
        /// <param name="bounding">Defines which data points to return when there are more data points in the time interval than maxValues</param>
        /// <param name="filter">Allows to filter the result based on the Quality property of the VTTQs</param>
        public abstract Task<VTTQs> HistorianReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter = QualityFilter.ExcludeNone);

        /// <summary>
        /// Reads aggregated historical data for the specified variable over defined intervals.
        /// </summary>
        /// <remarks>
        /// This method retrieves historical data and aggregates it within specified intervals.
        /// The <paramref name="intervalBounds"/> array must contain N sorted timestamps that define N-1 intervals.
        /// Each interval is [bounds[i], bounds[i+1]), left-inclusive and right-exclusive.
        ///
        /// For intervals with no data points:
        /// - Count returns 0
        /// - All other aggregations return DataValue.Empty
        ///
        /// The quality of all returned VTQs is always Good. The <paramref name="rawFilter"/> only controls
        /// which raw data points are included in the aggregation calculation.
        /// 
        /// Note that non-numerical values are ignored/filtered out before applying the aggregation.
        /// 
        /// </remarks>
        /// <param name="variable">The variable reference for which to retrieve aggregated data.</param>
        /// <param name="intervalBounds">Array of timestamps defining interval boundaries. Must be sorted in ascending order.
        /// Creates intervalBounds.Length - 1 intervals. Overall time range is [intervalBounds[0], intervalBounds[Last]].</param>
        /// <param name="aggregation">The aggregation method to apply within each interval (Average, Min, Max, Count, Sum, First, Last).</param>
        /// <param name="rawFilter">Quality filter applied to raw data points before aggregation. Default: ExcludeNone.</param>
        /// <returns>List of aggregated VTQs with length = intervalBounds.Length - 1. Each VTQ has Quality=Good
        /// and timestamp equal to the interval start time. Value is DataValue.Empty for empty intervals (except Count/Sum which return 0).</returns>
        public abstract Task<VTQs> HistorianReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter rawFilter = QualityFilter.ExcludeNone);

        /// <summary>
        /// Counts the number of data points in the history of a variable within a certain time interval.
        /// </summary>
        /// <param name="variable">The variable</param>
        /// <param name="startInclusive">The start of the time interval (inclusive)</param>
        /// <param name="endInclusive">The end of the time interval (inclusive)</param>
        /// <param name="filter">Allows to filter the result based on the Quality of the data points</param>
        /// <returns>The number of data points</returns>
        public abstract Task<long> HistorianCount(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter = QualityFilter.ExcludeNone);

        /// <summary>
        /// Deletes all data points in the history of a variable within a certain time interval and returns
        /// the number of data points that have been deleted.
        /// </summary>
        /// <param name="variable">The variable</param>
        /// <param name="startInclusive">The start of the time interval (inclusive)</param>
        /// <param name="endInclusive">The end of the time interval (inclusive)</param>
        /// <returns>The number of data points that have been deleted</returns>
        public abstract Task<long> HistorianDeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive);

        /// <summary>
        /// Modifies (delete, update, insert) the history of a variable using a set of data points.
        /// </summary>
        /// <param name="variable">The variable</param>
        /// <param name="mode">The modification mode, e.g. Insert, Update, Delete</param>
        /// <param name="data">The data points used for modification</param>
        public abstract Task HistorianModify(VariableRef variable, ModifyMode mode, params VTQ[] data);

        /// <summary>
        /// Deletes the entire history of one or more variables.
        /// </summary>
        /// <param name="variables">The variables</param>
        public abstract Task HistorianDeleteVariables(params VariableRef[] variables);

        /// <summary>
        /// Deletes the history of all variables of all objects contained in the set
        /// of objects as specified by the given object tree root.
        /// </summary>
        /// <param name="objectID">The object that defines the root of the object tree</param>
        public abstract Task HistorianDeleteAllVariablesOfObjectTree(ObjectRef objectID);

        /// <summary>
        /// Returns the data point in the history of a variable in a certain time interval that
        /// has the most recent value for the T_DB value, i.e. that has most recently been
        /// inserted or updated.
        /// </summary>
        /// <param name="variable">The variable</param>
        /// <param name="startInclusive">The start of the time interval (inclusive)</param>
        /// <param name="endInclusive">The end of the time interval (inclusive)</param>
        /// <returns>The most recently updated data point or null if the time interval is empty</returns>
        public abstract Task<VTTQ?> HistorianGetLatestTimestampDB(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive);

        #endregion

        /// <summary>
        /// Deletes/truncates the history of all variables of all objects contained in the set
        /// of objects as specified by the given object tree root and sets the value of all these variables
        /// to their default value with Timestamp.Empty and Quality.Bad.
        /// </summary>
        /// <param name="objectID">The object that defines the root of the object tree</param>
        public abstract Task ResetAllVariablesOfObjectTree(ObjectRef objectID);

        /// <summary>
        /// Invokes a module specific method/function. This will result in a call to
        /// <see cref="ModuleBase.OnMethodCall"/> in the receiving module.
        /// If <see cref="ModuleBase.OnMethodCall"/> returns a failure Result,
        /// then <see cref="CallMethod"/> will throw an exception.
        /// </summary>
        /// <param name="moduleID">The id of the receiving module</param>
        /// <param name="methodName">The name of the method to call</param>
        /// <param name="parameters">The set of parameters to pass to the method</param>
        /// <returns>The return value of the method call</returns>
        public abstract Task<DataValue> CallMethod(string moduleID, string methodName, params NamedValue[] parameters);

        /// <summary>
        /// Used to browse possible values of a specific member of an object.
        /// </summary>
        /// <param name="member">Identifies the member to browse</param>
        /// <param name="continueID">Used to continue a prior incomplete browsing</param>
        /// <returns></returns>
        public abstract Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null);

        public abstract void Dispose();
    }

    public enum Aggregation {
        Average,
        Min,
        Max,
        Count,
        Sum,
        First,
        Last
    }

    public class ConnectivityException : Exception
    {
        public ConnectivityException(string message) : base(message) { }
    }

    public class RequestException : Exception
    {
        public RequestException(string message) : base(message) { }
    }

    public interface EventListener
    {
        Task OnConfigChanged(List<ObjectRef> changedObjects);

        Task OnVariableValueChanged(List<VariableValue> variables);

        Task OnVariableHistoryChanged(List<HistoryChange> changes);

        Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents);

        Task OnConnectionClosed();
    }

    public class AlarmOrEvent // several instances of the same (ModuleID, IsSystem, Severity, Type, AffectedObjects) combination should be grouped together
    {
        public string ModuleID { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public Timestamp Time { get; set; }
        public bool IsSystem { get; set; } = false; // if true, then this notification originates from the Meditor core instead of a module
        public bool ReturnToNormal { get; set; } = false; // if true, indicates that a previous alarm of this type returned to normal (is not active anymore)
        public Severity Severity { get; set; } = Severity.Info;
        public string Type { get; set; } = ""; // module specific category e.g. "SensorFailure", "ModuleRestart", "CommunicationLoss"
        public string Message { get; set; } = ""; // one line of text
        public string Details { get; set; } = ""; // optional, potentially multiple lines of text
        public ObjectRef[] AffectedObjects { get; set; } = new ObjectRef[0]; // optional, specifies which objects are affected
        public Origin? Initiator { get; set; } = null; // the user or module that triggered the event
        public override string ToString() => Severity + ": " + Message;

        public bool IsWarningOrAlarm() => Severity == Severity.Warning || Severity == Severity.Alarm;
    }

    public struct HistoryChange
    {
        public HistoryChange(VariableRef variable, Timestamp start, Timestamp end, HistoryChangeType changeType) {
            Variable = variable;
            ChangeStart = start;
            ChangeEnd = end;
            ChangeType = changeType;
        }

        public VariableRef Variable { get; set; }
        public Timestamp   ChangeStart { get; set; }
        public Timestamp   ChangeEnd   { get; set; }
        public HistoryChangeType ChangeType { get; set; }

        public override readonly string ToString() => $"HistoryChange {ChangeType} {Variable}  [{ChangeStart} - {ChangeEnd}]";
    }

    public enum HistoryChangeType
    {
        Append,
        Delete,
        Insert,
        Update,
        Upsert,
        Mixed,
    }

    public enum ModifyMode
    {
        Insert,
        Update,
        Upsert,
        Delete,
        ReplaceAll
    }

    public enum BoundingMethod
    {
        TakeFirstN,
        TakeLastN,
        CompressToN
    }

    public enum QualityFilter
    {
        ExcludeNone,
        ExcludeBad,
        ExcludeNonGood,
    }

    public struct SubOptions
    {
        public static SubOptions AllUpdates(bool sendValueWithEvent) => new SubOptions(ListenMode.AllUpdates, sendValueWithEvent);
        public static SubOptions OnlyValueAndQualityChanges(bool sendValueWithEvent) => new SubOptions(ListenMode.OnlyValueAndQualityChanges, sendValueWithEvent);

        public SubOptions(ListenMode mode, bool sendValueWithEvent) {
            Mode = mode;
            SendValueWithEvent = sendValueWithEvent;
        }

        public ListenMode Mode { get; set; }

        /// <summary>
        /// if false: VariableValue.Value.V will be empty (only T and Q send to event handler)
        /// </summary>
        public bool SendValueWithEvent { get; set; }

        // Deadband?
    }

    public enum ListenMode
    {
        OnlyValueAndQualityChanges,
        AllUpdates
    }

    public class ModuleInfo
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Enabled { get; set; }
        public DataType[] VariableDataTypes { get; set; } = [];
        /// <summary>
        /// Return true if the module contains any object with at least one variable that is numeric or boolean
        /// </summary>
        public bool HasNumericVariables => VariableDataTypes.Any(t => t.IsNumeric() || t == DataType.Bool );
        public override string ToString() => Name;
    }

    public class LocationInfo
    {
        public string ID { get; set; } = "";

        public string Name { get; set; } = "";

        public string LongName { get; set; } = "";

        public string Parent { get; set; } = "";

        public List<NamedValue> Config { get; set; } = new List<NamedValue>();

        public override string ToString() => Name ?? "";
    }

    public class User
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = "";

        [XmlAttribute("login")]
        public string Login { get; set; } = "";

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        [XmlAttribute("encryptedPassword")]
        public string EncryptedPassword { get; set; } = "";

        [XmlAttribute("inactive")]
        public bool Inactive { get; set; } = false;

        public string[] Roles { get; set; } = new string[0];

        public List<NamedValue> Attributes { get; set; } = new List<NamedValue>();

        public override string ToString() => Name;
    }

    public class Role
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = "";
        
        public bool RestrictConfigChanges { get; set; } = false;

        public List<ConfigRule> ConfigRules { get; set; } = new List<ConfigRule>();

        public override string ToString() => Name;
    }

    public class ConfigRule : IXmlSerializable 
    {
        public Mode Mode { get; set; } = Mode.Allow;
        public ObjectRef RootObject { get; set; }
        public string ObjectTypes { get; set; } = "*"; // *: all types, comma separated list of types
        public string WithID { get; set; } = "*"; // *: all IDs
        public string Members { get; set; } = "*"; // *: all members, comma separated list of members

        public XmlSchema? GetSchema() => null;

        public void ReadXml(XmlReader reader) {
            string mode = reader["mode"];
            if (mode == "Allow") { Mode = Mode.Allow; }
            else if (mode == "Deny") { Mode = Mode.Deny; }
            else { throw new Exception("Invalid mode: " + mode); }
            RootObject = ObjectRef.FromEncodedString(reader["root"]);
            ObjectTypes = reader["types"];
            WithID = reader["id"];
            Members = reader["members"];
            reader.Read();
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteAttributeString("mode", Mode.ToString());
            writer.WriteAttributeString("root", RootObject.ToEncodedString());
            writer.WriteAttributeString("types", ObjectTypes);
            writer.WriteAttributeString("id", WithID);
            writer.WriteAttributeString("members", Members);
        }
    }

    public enum Mode {
        Allow,
        Deny
    }
}