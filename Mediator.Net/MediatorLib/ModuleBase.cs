// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    /// <summary>
    /// Each module is run on a dedicated thread so that async methods may be implemented synchronously.
    /// </summary>
    public abstract class ModuleBase {

        /// <summary>
        /// Called as the first method after module start for initialization.
        /// </summary>
        /// <param name="info">Provides information like module id, login information and configuration</param>
        /// <param name="restoreVariableValues">Contains the last value for all module variables with <see cref="Variable.Remember"/> == true</param>
        /// <param name="notifier">Used to notify the Mediator core about events, e.g. variable value changes and alarms</param>
        /// <param name="moduleThread">Can be used to post methods to the module's main thread</param>
        public abstract Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread);

        /// <summary>
        /// Called after Init (instead of Run) in case the Mediator initialization could not complete successfully.
        /// May be used to perform clean-up, if necessary. The default implementation is a no-op.
        /// </summary>
        /// <returns></returns>
        public virtual Task InitAbort() {
            return Task.FromResult(true);
        }

        /// <summary>
        /// Called after successful Init to start the execution of the module. This method must only return
        /// when the module is requested to shutdown, i.e. shutdown() returns true.
        /// Any exception or early return is considered an error and leads to module restart.
        /// </summary>
        /// <param name="shutdown">Used to indicate shutdown request to the module</param>
        public abstract Task Run(Func<bool> shutdown);

        /// <summary>
        /// Called by Mediator core directly after Init (and after <see cref="Notifier.Notify_ConfigChanged"/>) in order
        /// to get the object structure (including variables) of the module configuration.
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        public abstract Task<ObjectInfo[]> GetAllObjects();

        /// <summary>
        /// Used by Mediator core to get ObjectInfo for specific object references. Invalid object references should be ignored.
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="ids">The objects to get ObjectInfo for</param>
        public virtual Task<ObjectInfo[]> GetObjectsByID(ObjectRef[] ids) {
            return Task.FromResult(new ObjectInfo[0]);
        }

        /// <summary>
        /// Used to get meta data about the configuartion object model, e.g. type descriptions.
        /// </summary>
        public virtual Task<MetaInfos> GetMetaInfo() {
            return Task.FromResult(new MetaInfos());
        }

        /// <summary>
        /// Used to get the value of specific objects, i.e. a DataValue representation/serialization of the objects using <see cref="DataValue.FromObject"/>.
        /// Invalid object references should be ignored (excluded from the result).
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="objectIDs">The requested object references</param>
        public virtual Task<ObjectValue[]> GetObjectValuesByID(ObjectRef[] objectIDs) {
            return Task.FromResult(new ObjectValue[0]);
        }

        /// <summary>
        /// Used to get the value of specific object members, i.e. a DataValue representation/serialization of the member values.
        /// Invalid object/member references should be ignored (excluded from the result).
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="member">The desired members</param>
        public virtual Task<MemberValue[]> GetMemberValues(MemberRef[] member) {
            return Task.FromResult(new MemberValue[0]);
        }

        /// <summary>
        /// Called by other modules or clients to change the module configuration.
        /// For deleting an object or (optional) member, Value.IsEmpty will be true.
        /// To indicate an attempt for an invalid configuration change, use the Result return value.
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="origin">Information about the originator/initiator of this update</param>
        /// <param name="updateOrDeleteObjects">The objects to change or delete</param>
        /// <param name="updateOrDeleteMembers">The members to change or delete</param>
        /// <param name="addArrayElements">The new elements to add to an array member</param>
        public virtual Task<Result> UpdateConfig(Origin origin, ObjectValue[]? updateOrDeleteObjects, MemberValue[]? updateOrDeleteMembers, AddArrayElement[]? addArrayElements) {
            return Task.FromResult(Result.Failure("UpdateConfig not implemented by module"));
        }

        /// <summary>
        /// Called by <see cref="Connection.ReadVariablesSync"/> in order to get the latest value of specific variables,
        /// potentially triggering time consuming downstream read requests.
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="origin">Information about the originator/initiator of this read request</param>
        /// <param name="variables">The variables to read</param>
        /// <param name="timeout">Optional timeout</param>
        public virtual Task<VTQ[]> ReadVariables(Origin origin, VariableRef[] variables, Duration? timeout) {
            var now = Timestamp.Now;
            var vtqs = variables.Select(v => VTQ.Make(DataValue.Empty, now, Quality.Bad)).ToArray();
            return Task.FromResult(vtqs);
        }

        /// <summary>
        /// Called by <see cref="Connection.WriteVariables"/> and <see cref="Connection.WriteVariablesSync"/> to update the
        /// value of specific variables. If variables can not be written successfully, use the WriteResult return value
        /// to indicate which variable writes failed.
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="origin">Information about the originator/initiator of this write request</param>
        /// <param name="values">The variable values to write</param>
        /// <param name="timeout">Optional timeout</param>
        public virtual Task<WriteResult> WriteVariables(Origin origin, VariableValue[] values, Duration? timeout) {
            var errs = values.Select(v => new VariableError(v.Variable, "WriteVariables not implemented by module")).ToArray();
            return Task.FromResult(WriteResult.Failure(errs));
        }

        /// <summary>
        /// Called by other modules or clients to invoke a module specific method using <see cref="Connection.CallMethod(string, string, NamedValue[])"/>.
        /// Use the Result&lt;DataValue&gt; return value to indicate failure or return the result of the method call, if any.
        /// Any exception is considered an error and leads to module restart.
        /// </summary>
        /// <param name="origin">Information about the originator/initiator of this method call</param>
        /// <param name="methodName">The name of the method call</param>
        /// <param name="parameters">The parameters for method call</param>
        public virtual Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {
            return Task.FromResult(Result<DataValue>.Failure("Method not implemented: " + methodName));
        }

        /// <summary>
        /// Called by other modules or clients to query possible values for a specific member of an object.
        /// </summary>
        /// <param name="member">Identifies the member to browse</param>
        /// <param name="continueID">Used to continue a prior incomplete browsing</param>
        public virtual Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {
            return Task.FromResult(new BrowseResult());
        }
    }

    public class BrowseResult
    {
        public bool HasMore { get; set; } = false;
        public int? ContinueID { get; set; } = null;

        public DataValue[] Values { get; set; } = Array.Empty<DataValue>();
    }

    public struct Origin : IEquatable<Origin>
    {
        public OriginType Type { get; set; }
        public string ID { get; set; }
        public string Name { get; set; }

        public override bool Equals(object obj) {
            if (obj is Origin) {
                return Equals((Origin)obj);
            }
            return false;
        }

        public bool Equals(Origin other) => Type == other.Type && ID == other.ID;

        public static bool operator ==(Origin lhs, Origin rhs) => lhs.Equals(rhs);

        public static bool operator !=(Origin lhs, Origin rhs) => !(lhs.Equals(rhs));

        public override string ToString() => Name ?? (ID ?? "");

        public override int GetHashCode() => (ID ?? "").GetHashCode();
    }

    public enum OriginType
    {
        Module,
        User
    }

    public struct ModuleInitInfo
    {
        public string ModuleID { get; set; }
        public string ModuleName { get; set; }
        public string LoginPassword { get; set; }
        public string LoginServer { get; set; }
        public int LoginPort { get; set; }
        public string DataFolder { get; set; }
        public NamedValue[] Configuration { get; set; }
        public Config GetConfigReader() => new Config(Configuration);

        [Json.JsonIgnore]
        public InProcApi InProcApi { get; set; }
    }

    public interface InProcApi
    {
        Task<object?> AddRequest(RequestBase req);
    }

    public interface ModuleThread
    {
        void Post(Action action);
        void Post<T>(Action<T> action, T parameter);
        void Post<T1,T2>(Action<T1, T2> action, T1 parameter1, T2 parameter2);
        void Post<T1, T2, T3>(Action<T1, T2, T3> action, T1 parameter1, T2 parameter2, T3 parameter3);
    }

    public interface Notifier
    {
        void Notify_VariableValuesChanged(List<VariableValue> values);
        void Notify_ConfigChanged(List<ObjectRef> changedObjects);
        void Notify_AlarmOrEvent(AlarmOrEventInfo eventInfo);
    }

    public class AlarmOrEventInfo
    {
        public Timestamp Time { get; set; } = Timestamp.Now;

        public Severity Severity { get; set; } = Severity.Info;

        /// <summary>
        /// Module specific category e.g. "SensorFailure", "ModuleRestart", "CommunicationLoss"
        /// </summary>
        public string Type { get; set; } = "";

        /// <summary>
        /// If true, indicates that a previous alarm of this type returned to normal (is not active anymore)
        /// </summary>
        public bool ReturnToNormal { get; set; } = false;

        /// <summary>
        /// Should contain all relevant information in one line of text
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Optional additional information potentially in multiple lines of text (e.g. StackTrace)
        /// </summary>
        public string Details { get; set; } = "";

        /// <summary>
        /// Optional specification of the affected object(s)
        /// </summary>
        public ObjectRef[] AffectedObjects { get; set; } = new ObjectRef[0];

        public Origin? Initiator { get; set; } = null;

        public static AlarmOrEventInfo Info(string type, string message, params ObjectRef[] affectedObjects) {
            return new AlarmOrEventInfo() {
                Severity = Severity.Info,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }

        public static AlarmOrEventInfo Warning(string type, string message, params ObjectRef[] affectedObjects) {
            return new AlarmOrEventInfo() {
                Severity = Severity.Warning,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }

        public static AlarmOrEventInfo Alarm(string type, string message, params ObjectRef[] affectedObjects) {
            return new AlarmOrEventInfo() {
                Severity = Severity.Alarm,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }

        public static AlarmOrEventInfo RTN(string type, string message, params ObjectRef[] affectedObjects) {
            return new AlarmOrEventInfo() {
                Severity = Severity.Info,
                Type = type,
                Message = message,
                ReturnToNormal = true,
                AffectedObjects = affectedObjects
            };
        }

        public override string ToString() => Severity + ": " + Message;
    }

    public enum Severity
    {
        Info = 1,
        Warning = 2,
        Alarm = 3
    }

    public struct WriteResult
    {
        private WriteResult(VariableError[]? failures) {
            FailedVariables = failures;
        }

        public VariableError[]? FailedVariables { get; set; }

        public bool IsOK() => FailedVariables == null || FailedVariables.Length == 0;

        public bool Failed() => !IsOK();

        public static WriteResult OK => new WriteResult(null);

        public static WriteResult Failure(VariableError[] failures) => new WriteResult(failures);

        public static WriteResult FromResults(IEnumerable<WriteResult> list) {
            if (list.All(r => r.IsOK())) return OK;
            return Failure(list.Where(r => r.Failed()).SelectMany(x => x.FailedVariables).ToArray());
        }
    }

    public struct VariableError
    {
        public VariableError(VariableRef variable, string error) {
            Variable = variable;
            Error = error;
        }

        public VariableError(ObjectRef obj, string variableName, string error) {
            Variable = new VariableRef(obj, variableName);
            Error = error;
        }

        public VariableRef Variable { get; set; }
        public string Error { get; set; }
    }

    #region ModuleMessages

    public abstract class ModuleMsg {

        public abstract byte GetMessageCode();

    }

    public class ParentInfoMsg : ModuleMsg
    {
        public int PID { get; set; }

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_ParentInfo;
    }

    public class InitOrThrowMsg : ModuleMsg
    {
        public ModuleInitInfo InitInfo { get; set; }
        public VariableValue[] RestoreVariableValues { get; set; } = new VariableValue[0];

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_InitOrThrow;
    }

    public class InitAbortMsg : ModuleMsg
    {
        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_InitAbort;
    }

    public class RunMsg : ModuleMsg
    {
        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_Run;
    }

    public class ShutdownMsg : ModuleMsg
    {
        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_Shutdown;
    }

    public class GetAllObjectsMsg : ModuleMsg
    {
        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_GetAllObjects;
    }

    public class GetObjectsByIDMsg : ModuleMsg
    {
        public ObjectRef[] IDs { get; set; } = Array.Empty<ObjectRef>();

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_GetObjectsByID;
    }

    public class GetMetaInfoMsg : ModuleMsg
    {
        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_GetMetaInfo;
    }

    public class GetObjectValuesByIDMsg : ModuleMsg
    {
        public ObjectRef[] ObjectIDs { get; set; } = Array.Empty<ObjectRef>();

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_GetObjectValuesByID;
    }

    public class GetMemberValuesMsg : ModuleMsg
    {
        public MemberRef[] Member { get; set; } = Array.Empty<MemberRef>();

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_GetMemberValues;
    }

    public class UpdateConfigMsg : ModuleMsg
    {
        public Origin Origin { get; set; }
        public ObjectValue[]? UpdateOrDeleteObjects { get; set; }
        public MemberValue[]? UpdateOrDeleteMembers { get; set; }
        public AddArrayElement[]? AddArrayElements { get; set; }

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_UpdateConfig;
    }

    public class ReadVariablesMsg : ModuleMsg
    {
        public Origin Origin { get; set; }
        public VariableRef[] Variables { get; set; } = Array.Empty<VariableRef>();
        public Duration? Timeout { get; set; } = null;

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_ReadVariables;
    }

    public class WriteVariablesMsg : ModuleMsg
    {
        public Origin Origin { get; set; }
        public VariableValue[] Values { get; set; } = Array.Empty<VariableValue>();
        public Duration? Timeout { get; set; } = null;

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_WriteVariables;
    }

    public class OnMethodCallMsg : ModuleMsg
    {
        public Origin Origin { get; set; }
        public string Method { get; set; } = "";
        public NamedValue[] Parameters { get; set; } = Array.Empty<NamedValue>();

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_OnMethodCall;
    }

    public class BrowseMsg : ModuleMsg
    {
        public MemberRef Member { get; set; }
        public int? ContinueID { get; set; }

        public override byte GetMessageCode() => ExternalModuleHost.ModuleHelper.ID_Browse;
    }

    #endregion




}