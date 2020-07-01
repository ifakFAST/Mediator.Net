// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MimeKit;
using MailKit.Security;
using System.Text;

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
            if (connection != null) {
                await connection.Close();
            }
        }

        public override async Task Run(Func<bool> shutdown) {

            await LoadData();

            running = true;

            foreach (var entry in initBuffer) {
                await OnAlarmOrEvent(entry);
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

        public Task OnConfigChanged(ObjectRef[] changedObjects) { return Task.FromResult(true); }

        public Task OnVariableValueChanged(VariableValue[] variables) { return Task.FromResult(true); }

        public Task OnVariableHistoryChanged(HistoryChange[] changes) { return Task.FromResult(true); }

        public async Task OnAlarmOrEvents(AlarmOrEvent[] alarmOrEvents) {
            foreach (AlarmOrEvent ae in alarmOrEvents) {
                await OnAlarmOrEvent(ae);
            }
        }

        private async Task OnAlarmOrEvent(AlarmOrEvent alarmOrEvent) {

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

                if (alarmOrEvent.Severity == Severity.Warning || alarmOrEvent.Severity == Severity.Alarm) {
                    NotifyAlarm(alarmOrEvent);
                }
            }
        }

        private void NotifyAlarm(AlarmOrEvent alarm) {
            foreach(var no in model.MailNotificationSettings.Notifications) {
                if (no.Enabled && SourceMatch(no.Sources, alarm)) {
                    Task ignored = SendMail(alarm, no, model.MailNotificationSettings.SmtpSettings);
                }
            }
        }

        private async Task SendMail(AlarmOrEvent alarm, MailNotification no, SmtpSettings settings) {

            try {

                string source = alarm.IsSystem ? "System" : alarm.ModuleName;

                var msg = new StringBuilder();
                msg.AppendLine($"Severity: {alarm.Severity}");
                msg.AppendLine($"Message:  {alarm.Message}");
                msg.AppendLine($"Source:   {source}");
                msg.AppendLine($"Time UTC: {alarm.Time}");
                msg.AppendLine($"Time:     {alarm.Time.ToDateTime().ToLocalTime()}");
                if (!string.IsNullOrEmpty(alarm.Details)) {
                    msg.AppendLine($"Details:  {alarm.Details}");
                }

                string subject = no.Subject
                    .Replace("{severity}", alarm.Severity.ToString())
                    .Replace("{message}", alarm.Message)
                    .Replace("{source}", source);

                var messageToSend = new MimeMessage(
                    from: new InternetAddress[] { InternetAddress.Parse(settings.From) },
                    to: InternetAddressList.Parse(no.To),
                    subject: subject,
                    body: new TextPart(MimeKit.Text.TextFormat.Plain) {
                        Text = msg.ToString()
                    }
                );

                using (var smtp = new MailKit.Net.Smtp.SmtpClient()) {
                    smtp.MessageSent += (sender, args) => { /* args.Response */ };
                    smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                    await smtp.ConnectAsync(settings.Server, settings.Port, (SecureSocketOptions)settings.SslOptions);
                    await smtp.AuthenticateAsync(settings.AuthUser, settings.AuthPass);
                    await smtp.SendAsync(messageToSend);
                    await smtp.DisconnectAsync(quit: true);
                    Console.Out.WriteLine($"Sent notification mail (to: {no.To}, subject: {subject})");
                }
            }
            catch (Exception exp) {
                Console.Error.WriteLine("Failed to send notification mail: " + exp.Message);
            }
        }

        private bool SourceMatch(string sources, AlarmOrEvent alarm) {
            if (string.IsNullOrEmpty(sources)) { return false; }
            if (sources == "*") { return true; }
            string[] ss = sources.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant().Trim()).ToArray();
            if (alarm.IsSystem && ss.Contains("system")) { return true; }
            if (ss.Contains(alarm.ModuleID.ToLowerInvariant())) { return true; }
            if (ss.Contains(alarm.ModuleName.ToLowerInvariant())) { return true; }
            return false;
        }

        public Task OnConnectionClosed() { return Task.FromResult(true); }

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