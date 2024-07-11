// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public interface Identifiable {
        public string ID { get; set; }
        public string Name { get; set; }
    }

    public class Calculation : Identifiable {

        public string CalcID { get; private set; }
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        
        public Timestamp? TriggerStep_t  = null;
        public Duration?  TriggerStep_dt = null;

        public Calculation(string id) {
            CalcID = id;
            ID = id;
            Name = id;
        }

        public void TriggerStep(Timestamp t) {
            TriggerStep_t = t;
            TriggerStep_dt = null;
        }

        public void TriggerStep(Timestamp t, Duration dt) {
            TriggerStep_t = t;
            TriggerStep_dt = dt;
        }
    }

    public abstract class InputBase : Identifiable {

        public string ID { get; set; } = "";
        public string Name { get; set; }
        public string Unit { get; protected set; }
        public VTQ VTQ { get; internal set; }
        public VariableRef? AttachedVariable { get; internal set; }

        public DataType Type { get; private set; }
        public int Dimension { get; private set; }
        private DataValue? dvDefaultValue { get; set; }

        public bool IsNull => VTQ.V.IsEmpty;
        public bool NonNull => VTQ.V.NonEmpty;

        protected InputBase(string name, string unit, DataType type, int dimension, DataValue defaultValue) {
            Name = name;
            Unit = unit;
            Type = type;
            Dimension = dimension;
            dvDefaultValue = defaultValue;
            VTQ = VTQ.Make(defaultValue, Timestamp.Now, Quality.Good);
        }

        public Timestamp Time => VTQ.T;
        public Quality Quality => VTQ.Q;

        public bool IsGood => VTQ.Q == Quality.Good;
        public bool IsUncertain => VTQ.Q == Quality.Uncertain;
        public bool IsGoodOrUncertain => VTQ.Q == Quality.Good || VTQ.Q == Quality.Uncertain;
        public bool IsBad => VTQ.Q == Quality.Bad;
        public bool IsNotBad => VTQ.Q != Quality.Bad;
        public bool IsNotGood => VTQ.Q != Quality.Good;

        public static implicit operator VTQ(InputBase d) => d.VTQ;

        public DataValue? GetDefaultValue() => dvDefaultValue;

        private VariableRef? defaultVariable { get; set; }

        public void SetDefaultVariable(VariableRef variable) {
            defaultVariable = variable;
            dvDefaultValue = null;
        }

        public VariableRef? GetDefaultVariable() => defaultVariable;

        internal Func<Task<Connection>> connectionGetter { get; set; } = () => Task.FromResult((Connection)new ClosedConnection());

        public List<VTQ> HistorianReadRaw(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter = QualityFilter.ExcludeNone) {

            VariableRef variable = AttachedVariable ?? throw new Exception($"No variable connected to input {ID}");
            
            VTTQs vttqs = new();
            string? errMsg = null;

            SingleThreadedAsync.Run(async () => {
                try {
                    Connection con = await connectionGetter();
                    vttqs = await con.HistorianReadRaw(variable, startInclusive, endInclusive, maxValues, bounding, filter);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    errMsg = e.Message;
                }
            });

            if (errMsg != null) {
                throw new Exception($"HistorianReadRaw failed for input {ID}: {errMsg}");
            }

            var res = new List<VTQ>(vttqs.Count);
            foreach (var vttq in vttqs) {
                res.Add(vttq.ToVTQ());
            }
            return res;
        }

        public long HistorianCount(Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter = QualityFilter.ExcludeNone) {

            VariableRef variable = AttachedVariable ?? throw new Exception($"No variable connected to input {ID}");

            long result = 0;
            string? errMsg = null;

            SingleThreadedAsync.Run(async () => {
                try {
                    Connection con = await connectionGetter();
                    result = await con.HistorianCount(variable, startInclusive, endInclusive, filter);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    errMsg = e.Message;
                }
            });

            if (errMsg != null) {
                throw new Exception($"HistorianCount failed for input {ID}: {errMsg}");
            }

            return result;
        }
    }

    public abstract class OutputBase : Identifiable {

        public string ID { get; set; } = "";
        public string Name { get; set; }
        public string Unit { get; protected set; }

        private VTQ theVTQ;

        public VTQ VTQ {
            internal get {
                return theVTQ;
            }
            set {
                ValueHasBeenAssigned = true;
                theVTQ = value;
            }
        }

        public DataType Type { get; private set; }
        public int Dimension { get; private set; }
        internal bool ValueHasBeenAssigned = false;

        protected OutputBase(string name, string unit, DataType type, int dimension) {
            Name = name;
            Unit = unit;
            Type = type;
            Dimension = dimension;
            theVTQ = VTQ.Make(DataValue.Empty, Timestamp.Now, Quality.Good);
        }

        public Timestamp Time {
            set => VTQ = theVTQ.WithTime(value);
        }

        public Quality Quality {
            set => VTQ = theVTQ.WithQuality(value);
        }
    }

    public abstract class AbstractState : Identifiable
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";

        internal abstract DataValue GetValue();
        internal abstract DataValue GetDefaultValue();
        internal abstract void SetValueFromDataValue(DataValue v);
        internal abstract int GetDimension();
        internal abstract DataType GetDataType();
    }

    public abstract class StateBase: AbstractState {

        protected DataType Type { get; private set; }
        protected int Dimension { get; private set; }
        protected DataValue theDefaultValue { get; private set; }

        public bool IsNull => GetValue().IsEmpty;
        public bool NonNull => GetValue().NonEmpty;

        protected StateBase(string name, string unit, DataType type, int dimension, DataValue defaultValue) {
            Name = name;
            Unit = unit;
            Type = type;
            Dimension = dimension;
            theDefaultValue = defaultValue;
        }

        internal override DataValue GetDefaultValue() => theDefaultValue;

        internal override int GetDimension() => Dimension;

        internal override DataType GetDataType() => Type;
    }

    public interface EventSink
    {
        void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo);
    }

    public interface EventProvider
    {
        public EventSink? EventSinkRef { get; set; }
    }

    public class Alarm : AbstractState, EventProvider
    {
        public int Value { get; private set; } = 0;
        public EventSink? EventSinkRef { get; set; }

        private bool prefixMessageWithName = true;

        public Alarm(string name) {
            Name = name;
        }

        public Alarm(string name, bool prefixMessageWithName) {
            Name = name;
            this.prefixMessageWithName = prefixMessageWithName;
        }

        public Level? GetLevel {
            get {
                if (Value == 0) return null;
                if (Value == 1) return Level.Warn;
                if (Value == 2) return Level.Alarm;
                return Level.Alarm;
            }
        }

        public void Set(Level level, string? message = null) {
            if (CheckCallbackFailed()) return;
            if (level == Level.Warn && Value != (int)Level.Warn) {
                Value = (int)Level.Warn;
                var info = AdapterAlarmOrEvent.Warning(ID, MakeMsg(message));
                EventSinkRef!.Notify_AlarmOrEvent(info);
            }
            else if (level == Level.Alarm && Value != (int)Level.Alarm) {
                Value = (int)Level.Alarm;
                var info = AdapterAlarmOrEvent.Alarm(ID, MakeMsg(message));
                EventSinkRef!.Notify_AlarmOrEvent(info);
            }
        }

        public void Clear(string? message = null) {
            if (Value != 0) {
                Value = 0;
                string msg = string.IsNullOrEmpty(message) ? "Cleared" : message;
                var rtn = AdapterAlarmOrEvent.ReturnToNormalEvent(ID, MakeMsg(msg));
                EventSinkRef!.Notify_AlarmOrEvent(rtn);
            }
        }

        internal override DataValue GetValue() => DataValue.FromInt(Value);

        internal override DataValue GetDefaultValue() => DataValue.FromInt(0);

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.GetInt();
        }

        internal override int GetDimension() => 1;

        internal override DataType GetDataType() => DataType.Byte;

        private string MakeMsg(string? message) {
            if (string.IsNullOrEmpty(message)) return Name;
            if (prefixMessageWithName) {
                return $"{Name}: {message}";
            }
            else {
                return message;
            }
        }

        private bool CheckCallbackFailed() {
            if (EventSinkRef != null) return false;
            Console.Error.WriteLine("Invalid Alarm object: Needs to be defined on class level!");
            return true;
        }
    }

    public enum Level
    {
        Warn = 1,
        Alarm = 2
    }

    //public enum Priority // http://www.igss.com/Files/Doc-Help/Webhelp/V14/Alm/Content/Alarm_Severity.htm
    //{
    //    Low = 1,      // = Warning
    //    Medium = 2,   // = Alarm
    //    High = 3,
    //    Critical = 4,
    //}

    public class EventLog : EventProvider
    {
        public EventSink? EventSinkRef { get; set; }
        public string MessagePrefix { get; set; } = "";

        public EventLog(string messagePrefix = "") {
            MessagePrefix = messagePrefix;
        }

        public void Info(string id, string message) {
            if (CheckEventSinkFailed()) return;
            var info = AdapterAlarmOrEvent.Info(id, MakeMsg(message));
            EventSinkRef!.Notify_AlarmOrEvent(info);
        }

        public void Warn(string id, string message) {
            if (CheckEventSinkFailed()) return;
            var info = AdapterAlarmOrEvent.Warning(id, MakeMsg(message));
            EventSinkRef!.Notify_AlarmOrEvent(info);
        }

        public void Alarm(string id, string message) {
            if (CheckEventSinkFailed()) return;
            var info = AdapterAlarmOrEvent.Alarm(id, MakeMsg(message));
            EventSinkRef!.Notify_AlarmOrEvent(info);
        }

        private string MakeMsg(string message) {
            return string.IsNullOrEmpty(MessagePrefix) ? message : $"{MessagePrefix} {message}";
        }

        private bool CheckEventSinkFailed() {
            if (EventSinkRef != null) return false;
            Console.Error.WriteLine("Invalid EventLog object: Needs to be defined on class level!");
            return true;
        }
    }
}
