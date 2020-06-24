using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc
{
    public abstract class CalculationBase
    {
        public abstract Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback);

        // public abstract Task ReinitializeAfterConfigChange(Calculation def); ?

        public abstract Task<StepResult> Step(Timestamp t, InputValue[] inputValues);

        public abstract Task Shutdown();
    }

    public class InitParameter
    {
        public Calculation Calculation { get; set; }
        public StateValue[] LastState { get; set; }
        public OutputValue[] LastOutput { get; set; }
        public string ConfigFolder { get; set; }
        public string DataFolder { get; set; }
        public NamedValue[] ModuleConfig { get; set; }
    }

    public class InitResult
    {
        public InputDef[] Inputs { get; set; }
        public OutputDef[] Outputs { get; set; }
        public StateDef[] States { get; set; }
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
        public string InputID { get; set; }
        public VTQ Value { get; set; }
        public VariableRef? AttachedVariable { get; set; }
        public override string ToString() => InputID + " = " + Value.ToString();
    }

    public class OutputValue
    {
        public string OutputID { get; set; }
        public VTQ Value { get; set; }
        public override string ToString() => OutputID + " = " + Value.ToString();
    }

    public class StateValue
    {
        public string StateID { get; set; }
        public DataValue Value { get; set; }
        public override string ToString() => StateID + " = " + Value.ToString();
    }

    public class StepResult
    {
        public OutputValue[] Output { get; set; }
        public StateValue[] State { get; set; }
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
        public string[] AffectedDataItems { get; set; } = new string[0]; // optional, specifies which data items are affected

        public override string ToString() => Message;

        public static AdapterAlarmOrEvent Info(string type, string message, params string[] affectedDataItems) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Info,
                Type = type,
                Message = message,
                AffectedDataItems = affectedDataItems
            };
        }

        public static AdapterAlarmOrEvent Warning(string type, string message, params string[] affectedDataItems) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Warning,
                Type = type,
                Message = message,
                AffectedDataItems = affectedDataItems
            };
        }

        public static AdapterAlarmOrEvent Alarm(string type, string message, params string[] affectedDataItems) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Alarm,
                Type = type,
                Message = message,
                AffectedDataItems = affectedDataItems
            };
        }
    }
}
