// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public interface Identifiable {
        public string ID { get; set; }
        public string Name { get; set; }
    }

    public class Input : Identifiable {
        public string ID { get; set; } = "";
        public string Name { get; set; }
        public string Unit { get; private set; }
        public double DefaultValue { get; private set; }
        public VTQ VTQ { get; internal set; }

        public Input(string name, string unit = "", double defaultValue = 0.0) {
            Name = name;
            Unit = unit;
            DefaultValue = defaultValue;
            VTQ = VTQ.Make(defaultValue, Timestamp.Now, Quality.Good);
        }

        public double Value => VTQ.V.GetDouble();
        public Timestamp Time => VTQ.T;
        public Quality Quality => VTQ.Q;

        public bool IsGood => VTQ.Q == Quality.Good;
        public bool IsUncertain => VTQ.Q == Quality.Uncertain;
        public bool IsGoodOrUncertain => VTQ.Q == Quality.Good || VTQ.Q == Quality.Uncertain;
        public bool IsBad => VTQ.Q == Quality.Bad;
        public bool IsNotBad => VTQ.Q != Quality.Bad;
        public bool IsNotGood => VTQ.Q != Quality.Good;

        public static implicit operator VTQ(Input d) => d.VTQ;
        public static implicit operator double(Input d) => d.Value;

        public DataValue GetDefaultValue() => DataValue.FromDouble(DefaultValue);
    }

    public class Output : Identifiable
    {
        public string ID { get; set; } = "";
        public string Name { get; set; }
        public string Unit { get; private set; }
        public double DefaultValue { get; private set; }
        public VTQ VTQ { get; set; }
        public int? RoundDigits { get; set; }

        public Output(string name, string unit = "", double defaultValue = 0.0, int? roundDigits = 6) {
            Name = name;
            Unit = unit;
            DefaultValue = defaultValue;
            VTQ = VTQ.Make(defaultValue, Timestamp.Now, Quality.Good);
            RoundDigits = roundDigits;
        }

        public double Value {
            get => VTQ.V.GetDouble();
            set {
                double v = value;
                if (RoundDigits.HasValue) {
                    try {
                        v = Math.Round(v, RoundDigits.Value);
                    }
                    catch (Exception) { }
                }
                VTQ = VTQ.WithValue(DataValue.FromDouble(v));
            }
        }

        public Timestamp Time {
            get => VTQ.T;
            set => VTQ = VTQ.WithTime(value);
        }

        public Quality Quality {
            get => VTQ.Q;
            set => VTQ = VTQ.WithQuality(value);
        }

        public bool IsGood => VTQ.Q == Quality.Good;
        public bool IsUncertain => VTQ.Q == Quality.Uncertain;
        public bool IsGoodOrUncertain => VTQ.Q == Quality.Good || VTQ.Q == Quality.Uncertain;
        public bool IsBad => VTQ.Q == Quality.Bad;
        public bool IsNotBad => VTQ.Q != Quality.Bad;
        public bool IsNotGood => VTQ.Q != Quality.Good;

        public static implicit operator VTQ(Output d) => d.VTQ;
        public static implicit operator double(Output d) => d.Value;
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

    public class State : AbstractState
    {
        public double DefaultValue { get; private set; }
        public double Value { get; set; }
        public bool NeedHighPrecision { get; set; }

        public State(string name, string unit, double defaultValue, bool needHighPrecision = false) {
            Name = name;
            Unit = unit;
            DefaultValue = defaultValue;
            Value = defaultValue;
            NeedHighPrecision = needHighPrecision;
        }

        public static implicit operator double(State d) => d.Value;

        internal override DataValue GetValue() {
            double v = Value;
            if (NeedHighPrecision) {
                return DataValue.FromDouble(v);
            }
            try {
                float f = (float)v;
                return DataValue.FromFloat(f);
            }
            catch(Exception) {
                return DataValue.FromDouble(v);
            }
        }

        internal override DataValue GetDefaultValue() => DataValue.FromDouble(DefaultValue);

        internal override void SetValueFromDataValue(DataValue v) {
            double? vv = v.AsDouble();
            if (vv.HasValue) {
                Value = vv.Value;
            }
            else {
                Value = DefaultValue;
            }
        }

        internal override int GetDimension() => 1;

        internal override DataType GetDataType() => DataType.Float64;

    }

    public interface EventSink
    {
        void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo);
    }

    public interface EventProvider
    {
        public EventSink EventSinkRef { get; set; }
    }

    public class Alarm : AbstractState, EventProvider
    {
        public int Value { get; private set; } = 0;
        public EventSink EventSinkRef { get; set; }

        public Alarm(string name) {
            Name = name;
        }

        public Level? GetLevel {
            get {
                if (Value == 0) return null;
                if (Value == 1) return Level.Warn;
                if (Value == 2) return Level.Alarm;
                return Level.Alarm;
            }
        }

        public void Set(Level level, string message = null) {
            if (CheckCallbackFailed()) return;
            if (level == Level.Warn && Value != (int)Level.Warn) {
                Value = (int)Level.Warn;
                var info = AdapterAlarmOrEvent.Warning(ID, MakeMsg(message));
                EventSinkRef.Notify_AlarmOrEvent(info);
            }
            else if (level == Level.Alarm && Value != (int)Level.Alarm) {
                Value = (int)Level.Alarm;
                var info = AdapterAlarmOrEvent.Alarm(ID, MakeMsg(message));
                EventSinkRef.Notify_AlarmOrEvent(info);
            }
        }

        public void Clear(string message = null) {
            if (Value != 0) {
                Value = 0;
                string msg = string.IsNullOrEmpty(message) ? "Cleared" : message;
                var info = AdapterAlarmOrEvent.Info(ID, MakeMsg(msg));
                EventSinkRef.Notify_AlarmOrEvent(info);
            }
        }

        internal override DataValue GetValue() => DataValue.FromInt(Value);

        internal override DataValue GetDefaultValue() => DataValue.FromInt(0);

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.GetInt();
        }

        internal override int GetDimension() => 1;

        internal override DataType GetDataType() => DataType.Byte;

        private string MakeMsg(string message) {
            return string.IsNullOrEmpty(message) ? Name : $"{Name}: {message}";
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

    public enum Priority
    {
        Low = 1,      // = Warning
        Medium = 2,   // = Alarm
        High = 3,
        Critical = 4,
    }

    public class EventLog : EventProvider
    {
        public EventSink EventSinkRef { get; set; }
        public string MessagePrefix { get; set; } = "";

        public EventLog(string messagePrefix = "") {
            MessagePrefix = messagePrefix;
        }

        public void Info(string id, string message) {
            if (CheckEventSinkFailed()) return;
            var info = AdapterAlarmOrEvent.Info(id, MakeMsg(message));
            EventSinkRef.Notify_AlarmOrEvent(info);
        }

        public void Warn(string id, string message) {
            if (CheckEventSinkFailed()) return;
            var info = AdapterAlarmOrEvent.Warning(id, MakeMsg(message));
            EventSinkRef.Notify_AlarmOrEvent(info);
        }

        public void Alarm(string id, string message) {
            if (CheckEventSinkFailed()) return;
            var info = AdapterAlarmOrEvent.Alarm(id, MakeMsg(message));
            EventSinkRef.Notify_AlarmOrEvent(info);
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
