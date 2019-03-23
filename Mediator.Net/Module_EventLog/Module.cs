// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.EventLog
{
    public class Module : ModelObjectModule<EventLogConfig>, EventListener
    {
        private Connection connection = null;
        private List<AggregatedEvent> aggregatedWarningsAndAlarms = new List<AggregatedEvent>(1000);
        private Timestamp latestUsedTimestamp = Timestamp.Now.AddHours(-1);
        private bool running = false;
        private List<AlarmOrEvent> initBuffer = new List<AlarmOrEvent>();

        public override async Task Init(ModuleInitInfo info,
                                        VariableValue[] restoreVariableValues,
                                        Notifier notifier,
                                        ModuleThread moduleThread) {

            await base.Init(info, restoreVariableValues, notifier, moduleThread);

            connection = await HttpConnection.ConnectWithModuleLogin(info, this);
            await connection.EnableAlarmsAndEvents(Severity.Info);
        }

        private VariableRef GetVar() => VariableRef.Make(moduleID, "Root", "LastEvent");

        public async override Task InitAbort() {
            await connection.Close();
        }

        public override async Task Run(Func<bool> shutdown) {

            await LoadData();

            running = true;

            foreach (var entry in initBuffer) {
                OnAlarmOrEvent(entry);
            }
            initBuffer.Clear();

            while (!shutdown()) {
                await Task.Delay(500);
            }

            var ignored = connection.Close();
        }

        private async Task LoadData() {

            var result = new List<AggregatedEvent>();
            VariableRef varRef = GetVar();
            Timestamp t = Timestamp.Max;

            while (true) {

                VTTQ[] data = await connection.HistorianReadRaw(varRef, Timestamp.Empty, t, 1000, BoundingMethod.TakeLastN);

                var events = data.Select(VTTQ2AggregatedEvent)
                    .Where(ev => ev.IsWarningOrAlarm() && ev.State != EventState.Reset)
                    .ToList();

                if (events.Count == 0) break;

                events.AddRange(result);
                result = events;

                t = data[0].T.AddMillis(-1);
            }

            aggregatedWarningsAndAlarms = result;

            if (aggregatedWarningsAndAlarms.Count > 0) {
                latestUsedTimestamp = aggregatedWarningsAndAlarms.Last().TimeFirst;
            }
        }

        private static AggregatedEvent VTTQ2AggregatedEvent(VTTQ vttq) {
            return vttq.V.Object<AggregatedEvent>();
        }

        public void OnConfigChanged(ObjectRef[] changedObjects) { }

        public void OnVariableValueChanged(VariableValue[] variables) { }

        public void OnVariableHistoryChanged(HistoryChange[] changes) { }

        public async void OnAlarmOrEvent(AlarmOrEvent alarmOrEvent) {

            if (!running) {
                initBuffer.Add(alarmOrEvent);
                return;
            }

            AggregatedEvent aggEvent = null;

            for (int i = aggregatedWarningsAndAlarms.Count - 1; i >= 0; i--) {
                var e = aggregatedWarningsAndAlarms[i];
                if (e.CanAggregateWith(alarmOrEvent)) {
                    aggEvent = e;
                    break;
                }
            }

            if (aggEvent != null) {

                aggEvent.AggreagteWith(alarmOrEvent);

                DataValue data = DataValue.FromObject(aggEvent);
                VTQ vtq = new VTQ(aggEvent.TimeFirst, Quality.Good, data);
                await connection.HistorianModify(GetVar(), ModifyMode.Update, vtq);
            }
            else {

                aggEvent = AggregatedEvent.FromEvent(alarmOrEvent);
                if (aggEvent.TimeFirst <= latestUsedTimestamp) {
                    Timestamp t = latestUsedTimestamp.AddMillis(1);
                    aggEvent.TimeFirst = t;
                    aggEvent.TimeLast = t;
                }
                if (aggEvent.IsWarningOrAlarm()) {
                    aggregatedWarningsAndAlarms.Add(aggEvent);
                }
                latestUsedTimestamp = aggEvent.TimeFirst;

                DataValue data = DataValue.FromObject(aggEvent);
                VTQ vtq = new VTQ(aggEvent.TimeFirst, Quality.Good, data);
                await connection.HistorianModify(GetVar(), ModifyMode.Insert, vtq);
            }
        }

        public void OnConnectionClosed() { }

        public override Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {

            if (methodName == "GetActiveAlarms") {
                var data = DataValue.FromObject(aggregatedWarningsAndAlarms);
                var res = Result<DataValue>.OK(data);
                return Task.FromResult(res);
            }

            if (methodName == "Ack" || methodName == "Reset") {

                try {
                    string comment = parameters.FirstOrDefault(n => n.Name == "Comment").Value;
                    string strKeys = parameters.FirstOrDefault(n => n.Name == "Keys").Value;
                    Timestamp[] keys = strKeys.Split(';').Select(s => long.Parse(s)).Select(n => Timestamp.FromJavaTicks(n)).ToArray();
                    bool ack = methodName == "Ack";
                    if (ack) {
                        DoACK(keys, origin, comment);
                    }
                    else {
                        DoReset(keys, origin, comment);
                    }
                    var res = Result<DataValue>.OK(DataValue.Empty);
                    return Task.FromResult(res);
                }
                catch (Exception exp) {
                    return Task.FromResult(Result<DataValue>.Failure(exp.Message));
                }
            }

            return base.OnMethodCall(origin, methodName, parameters);
        }

        private void DoACK(Timestamp[] keys, Origin user, string comment) {

            var vtqs = new List<VTQ>();

            foreach (Timestamp key in keys) {
                var evt = aggregatedWarningsAndAlarms.FirstOrDefault(e => e.TimeFirst == key);
                if (evt == null) {
                    Console.WriteLine("No event found with timestamp " + key);
                    continue;
                }
                evt.State = EventState.Ack;
                evt.InfoACK = new UserAction() {
                    Time = Timestamp.Now.TruncateMilliseconds(),
                    UserID = user.ID,
                    UserName = user.Name,
                    Comment = comment
                };

                DataValue data = DataValue.FromObject(evt);
                vtqs.Add(new VTQ(evt.TimeFirst, Quality.Good, data));
            }

            connection.HistorianModify(GetVar(), ModifyMode.Update, vtqs.ToArray());
        }

        private void DoReset(Timestamp[] keys, Origin user, string comment) {

            var vtqs = new List<VTQ>();

            foreach (Timestamp key in keys) {
                var evt = aggregatedWarningsAndAlarms.FirstOrDefault(e => e.TimeFirst == key);
                if (evt == null) {
                    Console.WriteLine("No event found with timestamp " + key);
                    continue;
                }
                evt.State = EventState.Reset;
                evt.InfoReset = new UserAction() {
                    Time = Timestamp.Now.TruncateMilliseconds(),
                    UserID = user.ID,
                    UserName = user.Name,
                    Comment = comment
                };
                aggregatedWarningsAndAlarms.Remove(evt);

                DataValue data = DataValue.FromObject(evt);
                vtqs.Add(new VTQ(evt.TimeFirst, Quality.Good, data));
            }

            connection.HistorianModify(GetVar(), ModifyMode.Update, vtqs.ToArray());
        }
    }

    public class AggregatedEvent {
        public Timestamp TimeFirst { get; set; }
        public Timestamp TimeLast { get; set; }
        public int Count { get; set; } = 1;
        public EventState State { get; set; } = EventState.New;
        public UserAction? InfoACK { get; set; } = null;
        public UserAction? InfoReset { get; set; } = null;

        public Severity Severity { get; set; } = Severity.Info;
        public string ModuleID { get; set; } = "";
        public string ModuleName { get; set; } = "";
        public bool System { get; set; } = false; // if true, then this notification originates from the Meditor core instead of a module
        public string Type { get; set; } = ""; // module specific category e.g. "SensorFailure", "ModuleRestart", "CommunicationLoss"
        public ObjectRef[] Objects { get; set; } = new ObjectRef[0]; // optional, specifies which objects are affected
        public Origin? Initiator { get; set; } = null;

        public string Message { get; set; } = ""; // one line of text
        public string Details { get; set; } = ""; // optional, potentially multiple lines of text

        public bool IsWarningOrAlarm() => Severity == Severity.Warning || Severity == Severity.Alarm;

        public static AggregatedEvent FromEvent(AlarmOrEvent e) => new AggregatedEvent() {
            TimeFirst = e.Time,
            TimeLast = e.Time,
            Count = 1,
            State = EventState.New,
            Severity = e.Severity,
            ModuleID = e.ModuleID,
            ModuleName = e.ModuleName,
            System = e.IsSystem,
            Type = e.Type,
            Objects = e.AffectedObjects,
            Initiator = e.Initiator,
            Message = e.Message,
            Details = e.Details
        };

        public bool CanAggregateWith(AlarmOrEvent e) =>
            Type == e.Type &&
                Severity == e.Severity &&
                ModuleID == e.ModuleID &&
                System == e.IsSystem &&
                Arrays.Equals(Objects, e.AffectedObjects);

        public void AggreagteWith(AlarmOrEvent e) {
            TimeLast = e.Time;
            Count += 1;
            Initiator = e.Initiator;
            Message = e.Message;
            Details = e.Details;
        }
    }

    public struct UserAction
    {
        public Timestamp Time { get; set; }
        public string UserID { get; set; }
        public string UserName { get; set; }
        public string Comment { get; set; }
    }

    public enum EventState
    {
        New,
        Ack,
        Reset
    }
}