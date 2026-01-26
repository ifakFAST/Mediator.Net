// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.EventLog;

public class Module : ModelObjectModule<EventLogConfig>, EventListener
{
    private Connection connection = new ClosedConnection();
    private List<AggregatedEvent> aggregatedWarningsAndAlarms = new List<AggregatedEvent>(1000);
    private Timestamp latestUsedTimestamp = Timestamp.Now.AddHours(-1);
    private bool running = false;
    private readonly List<AlarmOrEvent> initBuffer = [];
    private ModuleInitInfo info;

    public override async Task Init(ModuleInitInfo info,
                                    VariableValue[] restoreVariableValues,
                                    Notifier notifier,
                                    ModuleThread moduleThread) {

        this.info = info;

        await base.Init(info, restoreVariableValues, notifier, moduleThread);

        await ConnectAndEnableEvents();
    }

    private async Task ConnectAndEnableEvents() {
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

        _ = KeepSessionAlive();
        _ = UpdateVariables();
        _ = StartCheckForModelFileModificationTask(shutdown);

        foreach (var entry in initBuffer) {
            await OnAlarmOrEvent(entry);
        }
        initBuffer.Clear();

        while (!shutdown()) {
            await Task.Delay(500);
        }

        running = false;
         _ = connection.Close();
    }

    private async Task KeepSessionAlive() {

        while (running) {

            await Task.Delay(TimeSpan.FromMinutes(15));

            if (running) {

                try {
                    await connection.Ping();
                }
                catch (Exception) { }
            }
        }
    }

    private async Task UpdateVariables() {

        int? lastValue = null;
        string lastValueStr = "";

        while (running) {

            await Task.Delay(TimeSpan.FromSeconds(10));

            if (running) {

                try {

                    AggregatedEvent[] alarms = aggregatedWarningsAndAlarms
                                                   .Where(it => !it.ReturnedToNormal && it.State != EventState.Ack)
                                                   .ToArray();

                    static string Str(AggregatedEvent e) {
                        string src = e.System ? "Sys" : e.ModuleID;
                        return $"{src}.{e.Type}";
                    }

                    int v = alarms.Length;
                    string vStr = string.Join(", ", alarms.Select(Str));

                    if (v != lastValue || vStr != lastValueStr) {

                        lastValue = v;
                        lastValueStr = vStr;
                        
                        Timestamp t = Timestamp.Now.TruncateMilliseconds();
                        VTQ vtqCount = VTQ.Make(v, t, Quality.Good);
                        VTQ vtqStr = VTQ.Make(vStr == "" ? model.TextForNoActiveWarningsAndAlarms : vStr, t, Quality.Good);

                        //var listVarValues = new List<VariableValue>(1);
                        //listVarValues.Add(VariableValue.Make(moduleID, "Root", "CountActiveWarningsAndAlarms", vtq));
                        //notifier?.Notify_VariableValuesChanged(listVarValues);
                        VariableValues values = new();

                        if (model.CountActiveWarningsAndAlarms.HasValue) {
                            values.Add(VariableValue.Make(model.CountActiveWarningsAndAlarms.Value, vtqCount));
                        }

                        if (model.ActiveWarningsAndAlarms.HasValue) {
                            values.Add(VariableValue.Make(model.ActiveWarningsAndAlarms.Value, vtqStr));
                        }

                        if (values.Count > 0) {
                            await connection.WriteVariables(values);
                        }

                    }
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    Console.Error.WriteLine($"Exception in UpdateVariables: {e.Message}");
                }
            }
        }
    }

    private async Task LoadData() {

        var result = new List<AggregatedEvent>();
        VariableRef varRef = GetVar();
        Timestamp t = Timestamp.Max;

        while (true) {

            List<VTTQ> data = await connection.HistorianReadRaw(varRef, Timestamp.Empty, t, 1000, BoundingMethod.TakeLastN);

            List<AggregatedEvent> events = data.Select(VTTQ2AggregatedEvent)
                .Where(ev => ev != null && ev.IsWarningOrAlarm() && ev.State != EventState.Reset && !(ev.State == EventState.Ack && ev.ReturnedToNormal))
                .Cast<AggregatedEvent>()
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

    private static AggregatedEvent? VTTQ2AggregatedEvent(VTTQ vttq) {
        return vttq.V.Object<AggregatedEvent>();
    }

    public Task OnConfigChanged(List<ObjectRef> changedObjects) { return Task.FromResult(true); }

    public Task OnVariableValueChanged(List<VariableValue> variables) { return Task.FromResult(true); }

    public Task OnVariableHistoryChanged(List<HistoryChange> changes) { return Task.FromResult(true); }

    public async Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
        foreach (AlarmOrEvent ae in alarmOrEvents) {
            await OnAlarmOrEvent(ae);
        }
    }

    private async Task OnAlarmOrEvent(AlarmOrEvent alarmOrEvent) {

        if (!running) {
            initBuffer.Add(alarmOrEvent);
            return;
        }

        AggregatedEvent? aggEvent = null;

        for (int i = aggregatedWarningsAndAlarms.Count - 1; i >= 0; i--) {
            var e = aggregatedWarningsAndAlarms[i];
            if (e.CanAggregateWith(alarmOrEvent)) {
                aggEvent = e;
                break;
            }
        }

        if (aggEvent != null) {

            aggEvent.AggregateWith(alarmOrEvent);

            if (aggEvent.ReturnedToNormal && aggEvent.State == EventState.Ack) {
                aggregatedWarningsAndAlarms.Remove(aggEvent);
            }

            DataValue data = DataValue.FromObject(aggEvent);
            VTQ vtq = new VTQ(aggEvent.TimeFirst, Quality.Good, data);
            await connection.HistorianModify(GetVar(), ModifyMode.Update, vtq);
        }
        else if (!alarmOrEvent.ReturnToNormal) {

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
        var settings = model.MailNotificationSettings;
        IEmailProvider provider = EmailProviderFactory.Create(settings);
        string fromAddress = EmailProviderFactory.GetFromAddress(settings);

        foreach (var no in settings.Notifications) {
            if (no.Enabled && SourceMatch(no.Sources, alarm)) {
                _ = SendNotification(alarm, no, provider, fromAddress);
            }
        }
    }

    private static async Task SendNotification(AlarmOrEvent alarm, MailNotification no, IEmailProvider provider, string fromAddress) {
        try {
            string source = alarm.IsSystem ? "System" : alarm.ModuleName;

            var body = new StringBuilder();
            body.AppendLine($"Severity: {alarm.Severity}");
            body.AppendLine($"Message:  {alarm.Message}");
            body.AppendLine($"Source:   {source}");
            body.AppendLine($"Time UTC: {alarm.Time}");
            body.AppendLine($"Time:     {alarm.Time.ToDateTime().ToLocalTime()}");
            if (!string.IsNullOrEmpty(alarm.Details)) {
                body.AppendLine($"Details:  {alarm.Details}");
            }

            string subject = no.Subject
                .Replace("{severity}", alarm.Severity.ToString())
                .Replace("{message}", alarm.Message)
                .Replace("{source}", source);

            var message = new EmailMessage {
                From = fromAddress,
                To = no.To,
                Subject = subject,
                Body = body.ToString()
            };

            await provider.SendAsync(message);
        }
        catch (Exception exp) {
            Console.Error.WriteLine("Failed to send notification mail: " + exp.Message);
        }
    }

    private static bool SourceMatch(string sources, AlarmOrEvent alarm) {
        if (string.IsNullOrEmpty(sources)) { return false; }
        if (sources == "*") { return true; }
        string[] ss = sources.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.ToLowerInvariant().Trim()).ToArray();
        if (alarm.IsSystem && ss.Contains("system")) { return true; }
        if (ss.Contains(alarm.ModuleID.ToLowerInvariant())) { return true; }
        if (ss.Contains(alarm.ModuleName.ToLowerInvariant())) { return true; }
        return false;
    }

    public Task OnConnectionClosed() {
        _ = CheckNeedConnectionRestart();
        return Task.FromResult(true);
    }

    private async Task CheckNeedConnectionRestart() {

        await Task.Delay(1000);

        if (running) {

            Console.Error.WriteLine($"{Timestamp.Now}: EventListener.OnConnectionClosed. Restarting connection...");
            Console.Error.Flush();

            try {
                await ConnectAndEnableEvents();
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                Console.Error.WriteLine($"{Timestamp.Now}: Restarting connection failed: {e.Message}");
                Console.Error.WriteLine($"{Timestamp.Now}: Terminating in 5 seconds...");
                Console.Error.Flush();
                await Task.Delay(5000);
                Environment.Exit(1);
            }
        }
    }

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

            if (evt.ReturnedToNormal) {
                aggregatedWarningsAndAlarms.Remove(evt);
            }
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

public sealed class AggregatedEvent {
    public Timestamp TimeFirst { get; set; }
    public Timestamp TimeLast { get; set; }
    public int Count { get; set; } = 1;
    public EventState State { get; set; } = EventState.New;
    public bool ReturnedToNormal { get; set; } = false;
    public InfoRTN? InfoRTN { get; set; } = null;
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

    public bool ShouldSerializeCount() => Count != 1;
    public bool ShouldSerializeModuleID() => !string.IsNullOrEmpty(ModuleID);
    public bool ShouldSerializeModuleName() => !string.IsNullOrEmpty(ModuleName);
    public bool ShouldSerializeSystem() => System;
    public bool ShouldSerializeReturnedToNormal() => ReturnedToNormal;
    public bool ShouldSerializeInfoRTN() => InfoRTN.HasValue;
    public bool ShouldSerializeInfoACK() => InfoACK.HasValue;
    public bool ShouldSerializeInfoReset() => InfoReset.HasValue;
    public bool ShouldSerializeObjects() => Objects != null && Objects.Length > 0;
    public bool ShouldSerializeInitiator() => Initiator.HasValue;
    public bool ShouldSerializeDetails() => !string.IsNullOrEmpty(Details);

    public bool IsWarningOrAlarm() => Severity == Severity.Warning || Severity == Severity.Alarm;

    public static AggregatedEvent FromEvent(AlarmOrEvent e) => new AggregatedEvent() {
        TimeFirst = e.Time,
        TimeLast = e.Time,
        Count = 1,
        State = EventState.New,
        ReturnedToNormal = e.ReturnToNormal,
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
        /*  Severity == e.Severity && */
            ModuleID == e.ModuleID &&
            System == e.IsSystem &&
            Arrays.Equals(Objects, e.AffectedObjects);

    public void AggregateWith(AlarmOrEvent e) {
        if (e.ReturnToNormal) {
            ReturnedToNormal = true;
            InfoRTN = new InfoRTN() {
                Time = e.Time,
                Message = e.Message,
            };
        }
        else {
            ReturnedToNormal = false;
            // InfoRTN = null;
            Severity = e.Severity;
            TimeLast = e.Time;
            Count += 1;
            Initiator = e.Initiator;
            Message = e.Message;
            Details = e.Details;
        }
    }
}

public struct UserAction
{
    public Timestamp Time { get; set; }
    public string UserID { get; set; }
    public string UserName { get; set; }
    public string Comment { get; set; }
}

public struct InfoRTN
{
    public Timestamp Time { get; set; }
    public string Message { get; set; }
}

public enum EventState
{
    New,
    Ack,
    Reset
}
