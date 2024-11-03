using System;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc
{
    public abstract class CalculationBase
    {
        public abstract Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback);

        // public abstract Task ReinitializeAfterConfigChange(Calculation def); ?

        public abstract Task<StepResult> Step(Timestamp t, Duration dt, InputValue[] inputValues);

        // Called from a different thread!
        public virtual void SignalStepAbort() { }

        public abstract Task Shutdown();
    }

    public interface ConnectionConsumer {
        void SetConnectionRetriever(Func<Task<Connection>> retriever);
    }

    public class InitParameter
    {
        public Calculation Calculation { get; set; } = new Calculation();
        public StateValue[] LastState { get; set; } = new StateValue[0];
        public OutputValue[] LastOutput { get; set; } = new OutputValue[0];
        public string ConfigFolder { get; set; } = "";
        public string DataFolder { get; set; } = "";
        public string ModuleID { get; set; } = "";
        public NamedValue[] ModuleConfig { get; set; } = new NamedValue[0];
    }

    public class InitResult
    {
        public InputDef[] Inputs { get; set; } = new InputDef[0];
        public OutputDef[] Outputs { get; set; } = new OutputDef[0];
        public StateDef[] States { get; set; } = new StateDef[0];
        public bool ExternalStatePersistence { get; set; }
    }

    public class InputDef
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Description { get; set; } = "";
        public DataType Type { get; set; } = DataType.Float64;
        public int Dimension { get; set; } = 1;
        public DataValue? DefaultValue { get; set; }
        public VariableRef? DefaultVariable { get; set; }
        public override string ToString() => Name;
    }

    public class OutputDef
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Description { get; set; } = "";
        public DataType Type { get; set; } = DataType.Float64;
        public int Dimension { get; set; } = 1;
        public override string ToString() => Name;
    }

    public class StateDef
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Description { get; set; } = "";
        public DataType Type { get; set; } = DataType.Float64;
        public int Dimension { get; set; } = 1;
        public DataValue? DefaultValue { get; set; }
        public override string ToString() => Name;
    }

    public class InputValue
    {
        public string InputID { get; set; } = "";
        public VTQ Value { get; set; }
        public VariableRef? AttachedVariable { get; set; }
        public override string ToString() => InputID + " = " + Value.ToString();
    }

    public class OutputValue
    {
        public string OutputID { get; set; } = "";
        public VTQ Value { get; set; }
        public override string ToString() => OutputID + " = " + Value.ToString();
    }

    public class StateValue
    {
        public string StateID { get; set; } = "";
        public DataValue Value { get; set; }
        public override string ToString() => StateID + " = " + Value.ToString();
    }

    public class TriggerCalculation {
        public string CalcID { get; set; } = "";
        public Timestamp TriggerStep_t { get; set; }
        public Duration? TriggerStep_dt { get; set; }
        
        public override string ToString() {
            var t  = $"t: {TriggerStep_t}";
            var dt = TriggerStep_dt.HasValue ? $", dt: {TriggerStep_dt.Value}" : "";
            return $"{CalcID} {t}{dt}";
        }
    }

    public class StepResult
    {
        public OutputValue[] Output { get; set; } = new OutputValue[0];
        public StateValue[] State { get; set; } = new StateValue[0];
        public TriggerCalculation[] TriggeredCalculations { get; set; } = new TriggerCalculation[0];
    }

    public interface AdapterCallback
    {
        void Notify_NeedRestart(string reason);
        void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo);
    }

    public class AdapterAlarmOrEvent
    {
        public Timestamp Time { get; set; } = Timestamp.Now;
        public Severity Severity { get; set; } = Severity.Info;

        /// <summary>
        /// Adapter specific category e.g. SensorFailure, ModuleRestart, CommunicationLoss
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
        /// Optional specification of the affected data item(s)
        /// </summary>
        public string[] AffectedObjects { get; set; } = new string[0]; // optional, specifies which objects are affected

        public override string ToString() => Message;


        public static AdapterAlarmOrEvent ReturnToNormalEvent(string type, string message, params string[] affectedObjects) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Info,
                ReturnToNormal = true,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }

        public static AdapterAlarmOrEvent Info(string type, string message, params string[] affectedObjects) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Info,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }

        public static AdapterAlarmOrEvent Warning(string type, string message, params string[] affectedObjects) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Warning,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }

        public static AdapterAlarmOrEvent Alarm(string type, string message, params string[] affectedObjects) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Alarm,
                Type = type,
                Message = message,
                AffectedObjects = affectedObjects
            };
        }
    }
}
