using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using static Ifak.Fast.Mediator.IO.Adapter_MQTT.MQTT_Util;

namespace Ifak.Fast.Mediator.IO.Adapter_MQTT;

[Identify("MQTT")]
public class MQTT : AdapterBase {

    private Adapter? config = null;
    private MqttClientOptions mqttOptions = new();
    private AdapterCallback? callback = null;
    private IMqttClient? clientMQTT = null;

    private string valueProperty = "";
    private string timestampProperty = "";
    private bool overrideTimestamp = false;
    private bool autoCreateDataItems = false;
    private string autoCreateDataItems_RootTopic = ""; // e.g. "my/sensors" (no leading or trailing slashes!)

    private readonly BufferNewDataItems bufferNewDataItems = new();

    private CancellationTokenSource? cancelSource;
    private readonly Dictionary<string, List<string>> mapTopicsToReadableDataItemIDs = [];
    private readonly Dictionary<string, string> mapDataItemID2Topic = [];

    private bool runLoopTerminated = false;

    public override bool SupportsScheduledReading => false;

    public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        string certBaseDir = Environment.CurrentDirectory;

        this.config = config;
        this.callback = callback;

        static bool Str2Bool(string str) => str.Equals("true", StringComparison.OrdinalIgnoreCase);

        valueProperty = config.GetConfigByName("ValueProperty", defaultValue: "");
        timestampProperty = config.GetConfigByName("TimestampProperty", defaultValue: "");
        overrideTimestamp = Str2Bool(config.GetConfigByName("OverrideTimestamp", defaultValue: "false"));
        autoCreateDataItems = Str2Bool(config.GetConfigByName("AutoCreateDataItems", defaultValue: "false"));
        autoCreateDataItems_RootTopic = config
            .GetConfigByName("AutoCreateDataItems_RootTopic", defaultValue: "")
            .Trim('/');

        Console.Out.WriteLine($"MQTT Adapter: overrideTimestamp={overrideTimestamp}, autoCreateDataItems={autoCreateDataItems}, autoCreateDataItems_RootTopic={autoCreateDataItems_RootTopic}");

        var mqttConfig = MqttConfigFromAdapterConfig(config);
        this.mqttOptions = MakeMqttOptions(certBaseDir, mqttConfig);

        mapTopicsToReadableDataItemIDs.Clear();

        // Build hierarchies for looking up parent nodes and topic prefixes
        Dictionary<string, string?> itemToParentMap = BuildNodeHierarchy(config);
        Dictionary<string, Node> nodeMap = BuildNodeMap(config);

        List<DataItem> allDataItems = config.GetAllDataItems();

        foreach (DataItem item in allDataItems) {

            string topic = GetEffectiveTopic(item, itemToParentMap, nodeMap);
            string id = item.ID;

            if (!string.IsNullOrEmpty(topic)) {

                mapDataItemID2Topic[id] = topic;

                if (item.Read) {
                    if (mapTopicsToReadableDataItemIDs.TryGetValue(topic, out List<string>? items)) {
                        items.Add(id);
                    }
                    else {
                        mapTopicsToReadableDataItemIDs[topic] = [id];
                    }
                }
            }
        }

        return Task.FromResult(Array.Empty<Group>());
    }

    private static MqttConfig MqttConfigFromAdapterConfig(Adapter config) {

        var c = new Mediator.Config(config.Config);

        return new MqttConfig() {
            Endpoint = config.Address,
            ClientIDPrefix = c.GetOptionalString("ClientIDPrefix", ""),
            CertFileCA = c.GetOptionalString("CertFileCA", ""),
            CertFileClient = c.GetOptionalString("CertFileClient", ""),
            KeyFileClient = c.GetOptionalString("KeyFileClient", ""),
            User = config.Login.HasValue ? config.Login.Value.UserName : "",
            Pass = config.Login.HasValue ? config.Login.Value.Password : "",
            IgnoreCertificateRevocationErrors = c.GetOptionalBool("IgnoreCertificateRevocationErrors", false),
            IgnoreCertificateChainErrors = c.GetOptionalBool("IgnoreCertificateChainErrors", false),
            AllowUntrustedCertificates = c.GetOptionalBool("AllowUntrustedCertificates", false),
            TLS = c.GetOptionalBoolNullable("TLS", null)
        };
    }

    public override void StartRunning() {

        if (config != null && !string.IsNullOrWhiteSpace(config.Address)) {
            _ = RunLoop(config.Address);
        }
        else {
            runLoopTerminated = true;
        }
    }

    private async Task RunLoop(string brokerEndpoint) {

        clientMQTT = null;
        const int ConnectRetryDelaySeconds = 5;

        this.cancelSource = new CancellationTokenSource();

        while (true) {

            CancellationTokenSource? cancelSrc = this.cancelSource;

            if (cancelSrc == null || cancelSrc.IsCancellationRequested) {
                break;
            }

            try {
                PrintLine($"Connecting to MQTT broker {brokerEndpoint}...");
                clientMQTT = await ConnectOrThrow(mqttOptions, cancelSrc.Token);
                PrintLine($"Connected to MQTT broker {brokerEndpoint}.");
                ReturnToNormal("Connect", $"Reconnected to MQTT broker {brokerEndpoint}.");
            }
            catch (Exception exp) {

                if (cancelSrc.IsCancellationRequested) {
                    break;
                }

                Exception e = exp.GetBaseException() ?? exp;
                string msg = $"Can not connect to MQTT broker {brokerEndpoint} ({e.Message}). Retrying in {ConnectRetryDelaySeconds} seconds...";
                LogWarn("Connect", msg);
                await Time.WaitSeconds(ConnectRetryDelaySeconds, abort: () => cancelSrc.IsCancellationRequested);
                continue;
            }

            clientMQTT.ApplicationMessageReceivedAsync += OnReceivedMessage;

            List<string> topics = mapTopicsToReadableDataItemIDs.Keys.ToList();

            if (autoCreateDataItems) {
                topics.Add(autoCreateDataItems_RootTopic + "/#");
            }

            foreach (string top in topics) {
                try {
                    await clientMQTT.SubscribeAsync(top, MqttQualityOfServiceLevel.AtLeastOnce, cancelSrc.Token);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    string msg = $"Subscribing to topic {top} failed! Resetting connection. Reconnecting in {ConnectRetryDelaySeconds} seconds...";
                    LogWarn("Subscribe", msg, dataItem: null, details: e.ToString());
                    CloseConnection();
                    await Time.WaitSeconds(ConnectRetryDelaySeconds, abort: () => cancelSrc.IsCancellationRequested);
                    break;
                }
            }

            if (clientMQTT != null) {
                ReturnToNormal("Subscribe", $"Subscribed to {topics.Count} topics");
            }

            while (!cancelSrc.IsCancellationRequested && clientMQTT != null) {
                try {
                    await clientMQTT.PingAsync(cancelSrc.Token);
                    await Time.WaitSeconds(10, abort: () => cancelSrc.IsCancellationRequested);
                }
                catch (Exception) {
                    string msg = $"MQTT connection broken. Trying to reconnect in {ConnectRetryDelaySeconds} seconds...";
                    LogWarn("Connect", msg);
                    CloseConnection();
                    await Time.WaitSeconds(ConnectRetryDelaySeconds, abort: () => cancelSrc.IsCancellationRequested);
                    break;
                }

                DataItemUpsert[] newDataItems = bufferNewDataItems.GetAllNewItemsIfLastUpdateOlderThan(Duration.FromSeconds(10));
                if (newDataItems.Length > 0) {
                    callback?.UpdateConfig(new ConfigUpdate() {
                        DataItemUpserts = newDataItems
                    });
                }
            }
        }

        CloseConnection();
        runLoopTerminated = true;
    }

    private async Task OnReceivedMessage(MqttApplicationMessageReceivedEventArgs arg) {

        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // Caution: This method will be called concurrently to the adapter main thread!
        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

        var Now = Timestamp.Now;

        MqttApplicationMessage msg = arg.ApplicationMessage;
        string topic = msg.Topic;

        try {
            await arg.AcknowledgeAsync(CancellationToken.None);
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            string err = $"Failed to acknowledge received message for topic {topic}";
            LogWarn("Acknowledge", err, dataItem: null, details: e.ToString());
        }

        if (mapTopicsToReadableDataItemIDs.TryGetValue(topic, out List<string>? ids)) {

            SendAssignedValues(topic, ids, Now, msg.PayloadSegment);
        }
        else if(autoCreateDataItems) {

            CreateNewDataItemFromTopic(topic, msg.PayloadSegment);
        }
    }

    private void CreateNewDataItemFromTopic(string topic, ArraySegment<byte> payloadBytes) {

        string? id = GetIdFromTopic(topic);
        if (id == null) {
            return;
        }

        (VTQ vtq, string unit)? payload = ParsePayload(topic, Timestamp.Now, payloadBytes);

        DataType GuessDataType() {
            if (!payload.HasValue) return DataType.Float64;
            DataValue dv = payload.Value.vtq.V;
            if (dv.IsString) return DataType.String;
            if (dv.IsBool) return DataType.Bool;
            if (dv.AsDouble().HasValue) return DataType.Float64;
            return DataType.JSON;
        }

        int GuessDimension() {
            if (!payload.HasValue) return 1;
            DataValue dv = payload.Value.vtq.V;
            if (dv.IsArray) return 0;
            return 1;
        }

        var item = new DataItemUpsert() {
            ParentNodeID = null,
            ID = id,
            Name = id,
            Unit = payload.HasValue ? payload.Value.unit : "",
            Type = GuessDataType(),
            Dimension = GuessDimension(),
            Read = true,
            Write = false,
            Address = topic
        };

        bufferNewDataItems.Add(item);

        //callback?.UpdateConfig(new ConfigUpdate() {
        //    DataItemUpserts = [item]
        //});
    }

    private string? GetIdFromTopic(string topic) {

        topic = topic.Trim('/');

        if (!topic.StartsWith(autoCreateDataItems_RootTopic)) {
            return null;
        }

        string topicSuffix = topic
            .Substring(autoCreateDataItems_RootTopic.Length)
            .Trim('/');

        return topicSuffix.Replace('/', '_').Replace(" ", "_");
    }

    private void SendAssignedValues(string topic, List<string> ids, Timestamp now, ArraySegment<byte> payloadBytes) {

        (VTQ vtq, string unit)? payload = ParsePayload(topic, now, payloadBytes);

        if (payload == null) {
            return;
        }

        var dataItems = new DataItemValue[ids.Count];
        for (int i = 0; i < ids.Count; i++) {
            string id = ids[i];
            dataItems[i] = new DataItemValue(id, payload.Value.vtq);
        }
        callback?.Notify_DataItemsChanged(dataItems);
    }

    private (VTQ vtq, string unit)? ParsePayload(string topic, Timestamp now, ArraySegment<byte> payloadBytes) {

        string unit = "";
        var vtq = VTQ.Make(DataValue.Empty, now, Quality.Good);

        if (payloadBytes.Array == null || payloadBytes.Count <= 0) {
            return (vtq, unit);
        }

        string payload;
        try {
            payload = Encoding.UTF8.GetString(payloadBytes);
        }
        catch (Exception) {
            string err = $"Rejected invalid value for topic {topic}: Expected UTF8 string. Payload length: {payloadBytes.Count} bytes.";
            LogWarn("Value", err);
            return null;
        }

        try {

            var options = new JsonNodeOptions { PropertyNameCaseInsensitive = true };
            JsonNode jsonNode = JsonNode.Parse(payload, options) ?? throw new Exception("Invalid JSON");

            if (jsonNode is JsonObject obj) {

                VTQ? vtqFromObj = TryParseVTQ(obj, valueProperty, timestampProperty);
                if (vtqFromObj.HasValue) {
                    vtq = vtqFromObj.Value;
                    if (overrideTimestamp) {
                        vtq = vtq.WithTime(now);
                    }

                    JsonNode? unitNode = obj["unit"];
                    if (unitNode is JsonValue jv && jv.TryGetValue(out string? str)) {
                        unit = str;
                    }
                }
                else {
                    vtq.V = DataValue.FromJSON(payload);
                }
            }
            else {
                vtq.V = DataValue.FromJSON(payload);
            }
        }
        catch (Exception) {
            vtq.V = DataValue.FromString(payload);
        }

        return (vtq, unit);
    }

    public static VTQ? TryParseVTQ(JsonObject obj, string valueProperty, string timestampProperty) {

        bool hasValueProperty = !string.IsNullOrWhiteSpace(valueProperty);
        bool hasTimestampProperty = !string.IsNullOrWhiteSpace(timestampProperty);
        
        string[] valueKeys     = hasValueProperty     ? [valueProperty]     : ["Value", "V", "Val"];
        string[] timestampKeys = hasTimestampProperty ? [timestampProperty] : ["Timestamp", "Time", "T"];

        JsonNode? FindFirst(string[] keys) {
            foreach (var key in keys) {
                JsonNode? node = obj[key];
                if (node != null) {
                    return node;
                }
            }
            return null;
        }

        try {

            JsonNode? va = FindFirst(valueKeys);
            JsonNode? ts = FindFirst(timestampKeys);

            if (va is null || ts is not JsonValue jvalue_ts) {
                return null;
            }

            DataValue value = ParseValue(va);
            Timestamp timestamp = ParseTimestampOrThrow(jvalue_ts);

            DateTime Now = DateTime.UtcNow;
            Timestamp tsMin = Timestamp.FromDateTime(Now.AddYears(-15));
            Timestamp tsMax = Timestamp.FromDateTime(Now.AddYears(15));

            if (timestamp < tsMin || timestamp > tsMax) {
                return null;
            }

            return VTQ.Make(value, timestamp, Quality.Good);
        }
        catch (Exception) {
            return null;
        }
    }

    private static DataValue ParseValue(JsonNode va) {
        string json = va.ToJsonString();
        return DataValue.FromJSON(json);
    }

    private static Timestamp ParseTimestampOrThrow(JsonValue ts) {

        if (ts.TryGetValue(out string? str)) {
           return Timestamp.FromISO8601(str);
        }

        if (ts.TryGetValue(out long timestamp)) {
            if (timestamp < uint.MaxValue) {// heuristic to test for seconds vs. milliseconds
                timestamp *= 1000;
            }
            return Timestamp.FromJavaTicks(timestamp);
        }

        throw new Exception("Invalid timestamp");
    }

    public override Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {
        VTQ[] res = items.Select(it => it.LastValue).ToArray();
        return Task.FromResult(res);
    }

    public override async Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {

        IMqttClient? client = this.clientMQTT;
        if (client == null) {
            FailedDataItemWrite[] res = values.Select(it => new FailedDataItemWrite(it.ID, "Not connected")).ToArray();
            return WriteDataItemsResult.Failure(res);
        }

        CancellationTokenSource? cancelSrc = this.cancelSource;
        if (cancelSrc == null || cancelSrc.IsCancellationRequested) {
            FailedDataItemWrite[] res = values.Select(it => new FailedDataItemWrite(it.ID, "Not connected")).ToArray();
            return WriteDataItemsResult.Failure(res);
        }

        var failedWrites = new List<FailedDataItemWrite>();

        foreach (DataItemValue div in values) {

            string id = div.ID;

            if (mapDataItemID2Topic.TryGetValue(id, out var topic)) {

                string payload = div.Value.V.JSON;

                var msg = new MqttApplicationMessageBuilder()
                               .WithTopic(topic)
                               .WithPayload(payload)
                               .WithContentType("application/json")
                               .WithRetainFlag(true)
                               .WithPayloadFormatIndicator(MqttPayloadFormatIndicator.CharacterData)
                               .Build();

                try {
                    await client.PublishAsync(msg, cancelSrc.Token);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    Console.Error.WriteLine($"MQTT publish to topic {topic} failed: {e.Message}");
                    failedWrites.Add(new FailedDataItemWrite(id, "Publish failed"));
                }

            }
            else {
                failedWrites.Add(new FailedDataItemWrite(id, "No topic found"));
            }
        }

        if (failedWrites.Count == 0) {
            return WriteDataItemsResult.OK;
        }
        else {
            return WriteDataItemsResult.Failure(failedWrites.ToArray());
        }
    }

    public override async Task Shutdown() {
        cancelSource?.Cancel();
        cancelSource?.Dispose();
        cancelSource = null;
        while (!runLoopTerminated) {
            await Task.Delay(50);
        }
    }

    private static Dictionary<string, string?> BuildNodeHierarchy(Adapter config) {
        var mapID2ParentNodeID = new Dictionary<string, string?>();

        void VisitNodes(List<Node> nodes, string? parentID) {
            foreach (Node node in nodes) {
                mapID2ParentNodeID[node.ID] = parentID;
                foreach (DataItem dataItem in node.DataItems) {
                    mapID2ParentNodeID[dataItem.ID] = node.ID;
                }
                VisitNodes(node.Nodes, node.ID);
            }
        }

        VisitNodes(config.Nodes, null);
        foreach (DataItem dataItem in config.DataItems) {
            mapID2ParentNodeID[dataItem.ID] = null;
        }

        return mapID2ParentNodeID;
    }

    private static Dictionary<string, Node> BuildNodeMap(Adapter config) {
        var nodeMap = new Dictionary<string, Node>();

        void VisitNodes(List<Node> nodes) {
            foreach (Node node in nodes) {
                nodeMap[node.ID] = node;
                VisitNodes(node.Nodes);
            }
        }

        VisitNodes(config.Nodes);
        return nodeMap;
    }

    private static string GetEffectiveTopic(DataItem item, Dictionary<string, string?> itemToParentMap, Dictionary<string, Node> nodeMap) {
        var prefixes = new List<string>();

        // Start with the DataItem's address
        string address = item.Address;

        // Walk up the Node hierarchy collecting TopicPrefix values
        string? currentNodeID = itemToParentMap.GetValueOrDefault(item.ID);
        while (currentNodeID != null) {
            if (nodeMap.TryGetValue(currentNodeID, out Node? node)) {
                var nodeConfig = new Mediator.Config(node.Config);
                string topicPrefix = nodeConfig.GetOptionalString("TopicPrefix", "");
                if (!string.IsNullOrEmpty(topicPrefix)) {
                    prefixes.Insert(0, topicPrefix.Trim('/'));
                }
                currentNodeID = itemToParentMap.GetValueOrDefault(currentNodeID);
            }
            else {
                break;
            }
        }

        // Build the effective topic
        if (prefixes.Count > 0) {
            prefixes.Add(address.Trim('/'));
            return string.Join("/", prefixes);
        }
        else {
            return address;
        }
    }
    public override Task<string[]> BrowseAdapterAddress() => Task.FromResult(Array.Empty<string>());

    public override Task<string[]> BrowseDataItemAddress(string? idOrNull) => Task.FromResult(Array.Empty<string>());

    private void CloseConnection() {
        Close(this.clientMQTT);
        this.clientMQTT = null;
    }

    private void PrintLine(string msg) {
        string name = config?.Name ?? "";
        Console.Out.WriteLine(name + ": " + msg);
    }

    private void LogWarn(string type, string msg, string? dataItem = null, string? details = null) {

        var ae = new AdapterAlarmOrEvent() {
            Time = Timestamp.Now,
            Severity = Severity.Warning,
            Type = type,
            Message = msg,
            Details = details ?? "",
            AffectedDataItems = string.IsNullOrEmpty(dataItem) ? [] : [dataItem]
        };

        callback?.Notify_AlarmOrEvent(ae);
    }

    private void ReturnToNormal(string type, string msg, params string[] affectedDataItems) {
        callback?.Notify_AlarmOrEvent(AdapterAlarmOrEvent.MakeReturnToNormal(type, msg, affectedDataItems));
    }
}

internal static class TaskUtil {

    internal static void CompleteFromTask(this TaskCompletionSource<bool> promise, Task completedTask) {

        if (completedTask.IsCompletedSuccessfully) {
            promise.SetResult(true);
        }
        else if (completedTask.IsFaulted) {
            promise.SetException(completedTask.Exception!);
        }
        else {
            promise.SetCanceled();
        }
    }
}

sealed class BufferNewDataItems {

    private readonly Dictionary<string, DataItemUpsert> items = new();
    private Timestamp timeLastNewAdd = Timestamp.Now;
    private readonly object lockObj = new();

    public void Add(DataItemUpsert item) {
        lock (lockObj) {
            if (!items.ContainsKey(item.ID)) {
                timeLastNewAdd = Timestamp.Now;
                Console.Out.WriteLine($"MQTT BufferNewDataItems: Add new DataItem {item.ID}");
            }
            items[item.ID] = item;
        }
    }

    public DataItemUpsert[] GetAllNewItemsIfLastUpdateOlderThan(Duration duration) {
        lock (lockObj) {
            Timestamp now = Timestamp.Now;
            if (now - timeLastNewAdd < duration) {
                return [];
            }
            var res = items
                .Values
                .OrderBy(it => it.ID)
                .ToArray();

            items.Clear();
            timeLastNewAdd = now;
            return res;
        }
    }
}
