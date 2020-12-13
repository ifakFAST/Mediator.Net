// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator
{
    public abstract class Connection : IDisposable
    {
        /// <summary>
        /// Close the connection. Never throws an exception.
        /// </summary>
        public abstract Task Close();

        /// <summary>
        /// Returns true when the connection is closed, false otherwise.
        /// </summary>
        public abstract bool IsClosed { get; }

        /// <summary>
        /// Returns the modules that the running Mediator instance is composed of (can not change while Mediator is running).
        /// </summary>
        public abstract Task<ModuleInfo[]> GetModules();

        /// <summary>
        /// Returns the global list of locations that describes the hierarchy of locations of a plant or facility
        /// </summary>
        public abstract Task<LocationInfo[]> GetLocations();

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
        public abstract Task<ObjectInfo[]> GetAllObjects(string moduleID);

        /// <summary>
        /// Returns all objects of a module that have a specific type/class.
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        /// <param name="className">The class/type name</param>
        public abstract Task<ObjectInfo[]> GetAllObjectsOfType(string moduleID, string className);

        /// <summary>
        /// Returns an object by id (ObjectRef)
        /// </summary>
        /// <param name="objectID">The object id</param>
        public virtual async Task<ObjectInfo> GetObjectByID(ObjectRef objectID) {
            ObjectInfo[] objects = await GetObjectsByID(objectID);
            return objects[0];
        }

        /// <summary>
        /// Returns objects by id.
        /// </summary>
        /// <param name="objectIDs">The object ids</param>
        public abstract Task<ObjectInfo[]> GetObjectsByID(params ObjectRef[] objectIDs);

        /// <summary>
        /// Returns all objects that are direct children of a specific object
        /// </summary>
        /// <param name="objectID">The id of the parent object</param>
        public virtual async Task<ObjectInfo[]> GetChildrenOfObject(ObjectRef objectID) {
            return await GetChildrenOfObjects(objectID);
        }

        /// <summary>
        /// Returns all objects that are direct children of specific objects
        /// </summary>
        /// <param name="objectIDs">The ids of the parent objects</param>
        public abstract Task<ObjectInfo[]> GetChildrenOfObjects(params ObjectRef[] objectIDs);

        /// <summary>
        /// Returns all objects of a module that have at least one variable of a given type
        /// </summary>
        /// <param name="moduleID">The id of the module</param>
        /// <param name="types">The required data type of the variables</param>
        public abstract Task<ObjectInfo[]> GetAllObjectsWithVariablesOfType(string moduleID, params DataType[] types);

        /// <summary>
        /// Returns an entire object by id.
        /// </summary>
        /// <param name="objectID">The id of the object to return</param>
        public virtual async Task<ObjectValue> GetObjectValueByID(ObjectRef objectID) {
            ObjectValue[] res = await GetObjectValuesByID(objectID);
            return res[0];
        }

        /// <summary>
        /// Returns entire objects by id.
        /// </summary>
        /// <param name="objectIDs">The ids of the objects to return</param>
        public abstract Task<ObjectValue[]> GetObjectValuesByID(params ObjectRef[] objectIDs);

        /// <summary>
        /// Returns the value of an object member.
        /// </summary>
        /// <param name="objectID">The id of the object</param>
        /// <param name="memberName">The name of the member to return</param>
        public virtual async Task<MemberValue> GetMemberValue(ObjectRef objectID, string memberName) {
            MemberValue[] values = await GetMemberValues(new MemberRef[] { new MemberRef(objectID, memberName) });
            return values[0];
        }

        /// <summary>
        /// Returns the value of an object member.
        /// </summary>
        /// <param name="member">The reference to the member to return</param>
        /// <returns></returns>
        public virtual async Task<MemberValue> GetMemberValue(MemberRef member) {
            MemberValue[] values = await GetMemberValues(new MemberRef[] { member });
            return values[0];
        }

        /// <summary>
        /// Returns the values of object members.
        /// </summary>
        /// <param name="member">The references to members to return</param>
        public abstract Task<MemberValue[]> GetMemberValues(MemberRef[] member);

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
        public abstract Task UpdateConfig(ObjectValue[] updateOrDeleteObjects, MemberValue[] updateOrDeleteMembers, AddArrayElement[] addArrayElements);

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
            VTQ[] tmp = await ReadVariables(new VariableRef[] { VariableRef.Make(objectID, variableName) });
            return tmp[0];
        }

        /// <summary>
        /// Reads the current value of a variable from the Mediator cache.
        /// Throws an exception if the variable does not exist.
        /// </summary>
        /// <param name="variable">The variable to read</param>
        public virtual async Task<VTQ> ReadVariable(VariableRef variable) {
            VTQ[] tmp = await ReadVariables(new VariableRef[] { variable });
            return tmp[0];
        }

        /// <summary>
        /// Reads the current value of variables from the Mediator cache.
        /// Throws an exception if any of the variables does not exist.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        public abstract Task<VTQ[]> ReadVariables(VariableRef[] variables);

        /// <summary>
        /// Reads the current value of variables from the Mediator cache.
        /// If any of the variables does not exist, it will be excluded from the result.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        public abstract Task<VariableValue[]> ReadVariablesIgnoreMissing(VariableRef[] variables);

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
            VTQ[] tmp = await ReadVariablesSync(new VariableRef[] { variable }, timeout);
            return tmp[0];
        }

        /// <summary>
        /// Reads the current value of variables directly from the containing module.
        /// Throws an exception if one of the variables does not exist or the timeout expires.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        /// <param name="timeout">Optional timeout</param>
        /// <returns></returns>
        public abstract Task<VTQ[]> ReadVariablesSync(VariableRef[] variables, Duration? timeout = null);

        /// <summary>
        /// Reads the current value of variables directly from the containing module.
        /// Throws an exception if the timeout expires.
        /// Variables, that do not exist, are excluded from the result.
        /// </summary>
        /// <param name="variables">The variables to read</param>
        /// <param name="timeout">Optional timeout</param>
        /// <returns></returns>
        public abstract Task<VariableValue[]> ReadVariablesSyncIgnoreMissing(VariableRef[] variables, Duration? timeout = null);

        /// <summary>
        /// Reads the current value of all variables of all objects in the tree of objects defined by the given object reference.
        /// </summary>
        /// <param name="objectID">The object defining the root of the object tree</param>
        public abstract Task<VariableValue[]> ReadAllVariablesOfObjectTree(ObjectRef objectID);

        /// <summary>
        /// Writes a new value to a variable without waiting for the receiving module to complete the write request.
        /// An exception is thrown when the variable does not exist.
        /// </summary>
        /// <param name="objectID">The object containing the variable</param>
        /// <param name="variableName">The name of the variable to write</param>
        /// <param name="value">The new value</param>
        public virtual async Task WriteVariable(ObjectRef objectID, string variableName, VTQ value) {
            await WriteVariables(new VariableValue[] { VariableValue.Make(objectID, variableName, value) });
        }

        /// <summary>
        /// Writes a new value to a variable without waiting for the receiving module to complete the write request.
        /// An exception is thrown when the variable does not exist.
        /// </summary>
        /// <param name="variable">The reference to the variable to write</param>
        /// <param name="value">The new value</param>
        public virtual async Task WriteVariable(VariableRef variable, VTQ value) {
            await WriteVariables(new VariableValue[] { VariableValue.Make(variable, value) });
        }

        /// <summary>
        /// Writes new values to variables without waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, an exception is thrown and no value is written.
        /// </summary>
        /// <param name="values">The new variable values</param>
        public abstract Task WriteVariables(params VariableValue[] values);

        /// <summary>
        /// Writes new values to variables without waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, it is ignored and a corresponding VariableError is returned
        /// for each missing variable.
        /// </summary>
        /// <param name="values">The new variable values</param>
        public abstract Task<WriteResult> WriteVariablesIgnoreMissing(params VariableValue[] values);

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
            WriteResult res = await WriteVariablesSync(new VariableValue[] { VariableValue.Make(objectID, variableName, value) }, timeout);
            if (res.Failed()) return Result.Failure(res.FailedVariables[0].Error);
            return Result.OK;
        }

        /// <summary>
        /// Writes new values to variables waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, an exception is thrown and no value is written.
        /// Check the return value to verify whether the receiving module successfully processed the write requests.
        /// </summary>
        /// <param name="values">The new variable values</param>
        /// <param name="timeout">Optional timeout</param>
        public abstract Task<WriteResult> WriteVariablesSync(VariableValue[] values, Duration? timeout = null);

        /// <summary>
        /// Writes new values to variables waiting for the receiving module to complete the write request.
        /// When any of the variables does not exist, it is ignored and a corresponding VariableError is returned
        /// for each missing variable.
        /// Check the return value to verify whether the receiving module successfully processed the write requests.
        /// </summary>
        /// <param name="values">The new variable values</param>
        /// <param name="timeout">Optional timeout</param>
        public abstract Task<WriteResult> WriteVariablesSyncIgnoreMissing(VariableValue[] values, Duration? timeout = null);

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
        public abstract Task<VTTQ[]> HistorianReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding);

        /// <summary>
        /// Counts the number of data points in the history of a variable within a certain time interval.
        /// </summary>
        /// <param name="variable">The variable</param>
        /// <param name="startInclusive">The start of the time interval (inclusive)</param>
        /// <param name="endInclusive">The end of the time interval (inclusive)</param>
        /// <returns>The number of data points</returns>
        public abstract Task<long> HistorianCount(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive);

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
        Task OnConfigChanged(ObjectRef[] changedObjects);

        Task OnVariableValueChanged(VariableValue[] variables);

        Task OnVariableHistoryChanged(HistoryChange[] changes);

        Task OnAlarmOrEvents(AlarmOrEvent[] alarmOrEvents);

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
        public string ID { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        /// <summary>
        /// Return true if the module contains any object with at least one variable that is numeric or boolean
        /// </summary>
        public bool HasNumericVariables { get; set; }
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
    }

    public class Role
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = "";
    }
}