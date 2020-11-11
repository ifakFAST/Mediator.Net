// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Dashboard;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;

namespace Ifak.Fast.Mediator.EventLog
{
    [Identify(id: "EventLog", bundle: "Generic", path: "eventlog.html", icon:"mdi-alert-circle-outline")]
    public class View_EventLog : ViewBase
    {
        const string Module = "EventLog";

        private VariableRef Var = VariableRef.Make(Module, "Root", "LastEvent");

        private ActiveError[] lastAlarms = new ActiveError[0];
        private ActiveError[] lastEvents = new ActiveError[0];
        private TimeRange lastTimeRange = new TimeRange();

        public override Task OnActivate() {
            Connection.EnableVariableHistoryChangedEvents(Var);
            return Task.FromResult(true);
        }

        public override async Task<NaviAugmentation?> GetNaviAugmentation() {
            DataValue res = await Connection.CallMethod(Module, "GetActiveAlarms");
            AggregatedEvent[] errors = res.Object<AggregatedEvent[]>();
            bool anyAlarm = errors.Any(err => err.Severity == Severity.Alarm);
            bool anyWarn = errors.Any(err => err.Severity == Severity.Warning);
            if (anyWarn || anyAlarm) {
                return new NaviAugmentation() {
                    IconColor = (anyAlarm ? "red" : "orange")
                };
            }
            else {
                return new NaviAugmentation() {
                    IconColor = ""
                };
            }
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            switch (command) {

                case "Load": {

                        var time = parameters.Object<TimeRange>();

                        var alarms = await GetActiveAlarms();
                        var events = await GetEvents(time, alarms);

                        lastAlarms = alarms;
                        lastEvents = events;
                        lastTimeRange = time;

                        return ReqResult.OK(new {
                            Alarms = alarms,
                            Events = events
                        });
                    }

                case "AckReset": {

                        var para = parameters.Object<AckResetParams>();
                        string method = para.Ack ? "Ack" : "Reset";
                        var comment = new NamedValue("Comment", para.Comment);
                        var keys = new NamedValue("Keys", string.Join(';', para.Timestamps.Select(x => x.ToString(CultureInfo.InvariantCulture))));
                        await Connection.CallMethod(Module, method, comment, keys);
                        var alarms = await GetActiveAlarms();
                        var events = await GetEvents(para.TimeRange, alarms);

                        lastAlarms = alarms;
                        lastEvents = events;
                        lastTimeRange = para.TimeRange;

                        return ReqResult.OK(new {
                            Alarms = alarms,
                            Events = events
                        });
                    }

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        private async Task<ActiveError[]> GetActiveAlarms () {
            DataValue res = await Connection.CallMethod(Module, "GetActiveAlarms");
            AggregatedEvent[] errors = res.Object<AggregatedEvent[]>();
            return errors.Select(Transform).Reverse().ToArray();
        }

        private async Task<ActiveError[]> GetEvents(TimeRange range, ActiveError[] alarms) {

            Timestamp tStart = range.GetStart();
            Timestamp tEnd = range.GetEnd();

            VTTQ[] data = await Connection.HistorianReadRaw(Var, tStart, tEnd, 10000, BoundingMethod.TakeLastN);
            var events = data.Select(VTTQ2Event).ToList();

            Func<Timestamp, bool> isInRange = t => (t >= tStart && t <= tEnd);

            return events.Concat(alarms.Where(a => !isInRange(a.TimeFirst) && isInRange(a.TimeLast)))
                .OrderByDescending(x => x.T)
                .ToArray();
        }

        private static bool IsInRange(Timestamp x, Timestamp rangeStart, Timestamp rangeEnd) {
            return (x >= rangeStart && x <= rangeEnd);
        }

        private static ActiveError VTTQ2Event(VTTQ vttq) {
            return Transform(vttq.V.Object<AggregatedEvent>());
        }

        public async override Task OnVariableHistoryChanged(HistoryChange[] changes) {

            var alarms = await GetActiveAlarms();
            var events = await GetEvents(lastTimeRange, alarms);

            var newOrChangedAlarms = alarms.Where(a => {
                var oldVersion = lastAlarms.FirstOrDefault(x => x.T == a.T);
                if (oldVersion == null) return true;
                return !StdJson.ObjectsDeepEqual(a, oldVersion);
            }).ToArray();

            long[] removedAlarms = lastAlarms.Where(la => alarms.All(a => a.T != la.T)).Select(x => x.T).ToArray();

            var newOrChangedEvents = events.Where(a => {
                var oldVersion = lastEvents.FirstOrDefault(x => x.T == a.T);
                if (oldVersion == null) return true;
                return !StdJson.ObjectsDeepEqual(a, oldVersion);
            }).ToArray();

            if (newOrChangedAlarms.Length > 0 || newOrChangedEvents.Length > 0 || removedAlarms.Length > 0) {

                lastAlarms = alarms;
                lastEvents = events;

                await Context.SendEventToUI("Event", new {
                    Alarms = newOrChangedAlarms,
                    Events = newOrChangedEvents,
                    RemovedAlarms = removedAlarms,
                });
            }
        }

        private static ActiveError Transform(AggregatedEvent ev) {
            return new ActiveError() {
                T = ev.TimeFirst.JavaTicks,
                TimeFirstLocal = MakeLocal(ev.TimeFirst),
                TimeLastLocal = MakeLocal(ev.TimeLast),
                TimeAckLocal = ev.InfoACK.HasValue ? MakeLocal(ev.InfoACK.Value.Time) : "",
                TimeResetLocal = ev.InfoReset.HasValue ? MakeLocal(ev.InfoReset.Value.Time) : "",
                TimeRTNLocal = ev.InfoRTN.HasValue ? MakeLocal(ev.InfoRTN.Value.Time) : "",
                Source = ev.System ? "System" : ev.ModuleName,
                TimeFirst = ev.TimeFirst,
                TimeLast = ev.TimeLast,
                Count = ev.Count,
                State = ev.State,
                InfoACK = ev.InfoACK,
                InfoReset = ev.InfoReset,
                Severity = ev.Severity,
                ModuleID = ev.ModuleID,
                ModuleName = ev.ModuleName,
                System = ev.System,
                Type = ev.Type,
                Objects = ev.Objects,
                Initiator = ev.Initiator,
                RTN = ev.ReturnedToNormal,
                Msg = MakeShortString(ev.Message),
                Message = ev.Message,
                Details = ev.Details,
            };
        }

        private static string MakeShortString(string s) {
            const int Max = 200;
            if (s.Length <= Max) return s;
            return s.Substring(0, Max - 3) + "...";
        }

        private static string MakeLocal(Timestamp t) {
            return t.ToDateTime().ToLocalTime().ToString("yyyy'-'MM'-'dd\u00A0HH':'mm':'ss", CultureInfo.InvariantCulture);
        }
    }

    public class ActiveError
    {
        public long T { get; set; }
        public string TimeFirstLocal { get; set; }
        public string TimeLastLocal { get; set; }
        public string TimeAckLocal { get; set; } = "";
        public string TimeResetLocal { get; set; } = "";
        public string TimeRTNLocal { get; set; } = "";

        public string Source { get; set; } = "";

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
        public bool RTN { get; set; } = false; // ReturnToNormal
        public InfoRTN? InfoRTN { get; set; } = null;
        public string Msg { get; set; } = "";
        public string Message { get; set; } = ""; // one line of text
        public string Details { get; set; } = ""; // optional, potentially multiple lines of text
    }

    public class AckResetParams
    {
        public bool Ack { get; set; }
        public string Comment { get; set; }
        public long[] Timestamps { get; set; }
        public TimeRange TimeRange { get; set; }
    }
}
