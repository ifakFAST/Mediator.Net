using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using MQTTnet;
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
    private readonly Dictionary<string, List<(string id, string? jsonPointer)>> mapTopicsToItemsAndPointers = [];
    private readonly Dictionary<string, (string topic, string? jsonPointer)> mapDataItemID2TopicAndPointer = [];

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

        mapTopicsToItemsAndPointers.Clear();

        // Build hierarchies for looking up parent nodes and topic prefixes
        Dictionary<string, string?> itemToParentMap = BuildNodeHierarchy(config);
        Dictionary<string, Node> nodeMap = BuildNodeMap(config);

        List<DataItem> allDataItems = config.GetAllDataItems();

        foreach (DataItem item in allDataItems) {

            string effectiveTopic = GetEffectiveTopic(item, itemToParentMap, nodeMap);
            string id = item.ID;

            if (!string.IsNullOrEmpty(effectiveTopic)) {

                // Parse topic and JSON pointer from the effective topic
                (string topic, string? jsonPointer) = ParseTopicAndJsonPointer(effectiveTopic);

                mapDataItemID2TopicAndPointer[id] = (topic, jsonPointer);

                if (item.Read) {
                    if (mapTopicsToItemsAndPointers.TryGetValue(topic, out List<(string id, string? jsonPointer)>? items)) {
                        items.Add((id, jsonPointer));
                    }
                    else {
                        mapTopicsToItemsAndPointers[topic] = [(id, jsonPointer)];
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
            NoCertificateValidation = c.GetOptionalBool("NoCertificateValidation", false),
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

            List<string> topics = mapTopicsToItemsAndPointers.Keys.ToList();

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

        if (mapTopicsToItemsAndPointers.TryGetValue(topic, out List<(string id, string? jsonPointer)>? items)) {

            SendAssignedValues(topic, items, Now, msg.Payload);
        }
        else if(autoCreateDataItems) {

            CreateNewDataItemFromTopic(topic, msg.Payload);
        }
    }

    private void CreateNewDataItemFromTopic(string topic, ReadOnlySequence<byte> payloadBytes) {

        string? id = GetIdFromTopic(topic);
        if (id == null) {
            return;
        }

        (VTQ vtq, string unit)? payload = ParsePayloadBytes(topic, Timestamp.Now, payloadBytes);

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

    private void SendAssignedValues(string topic, List<(string id, string? jsonPointer)> itemsWithPointers, Timestamp now, ReadOnlySequence<byte> payloadBytes) {

        // First, try to parse the full payload as JSON once
        JsonNode? rootNode = null;
        bool hasJsonPointers = itemsWithPointers.Any(item => !string.IsNullOrEmpty(item.jsonPointer));

        if (hasJsonPointers) {
            // We have at least one JSON pointer, so parse the payload as JSON
            if (payloadBytes.Length > 0) {
                try {
                    string payload = Encoding.UTF8.GetString(payloadBytes);
                    var options = new JsonNodeOptions { PropertyNameCaseInsensitive = true };
                    rootNode = JsonNode.Parse(payload, options);
                }
                catch (Exception) {
                    // Not valid JSON, can't use JSON pointers
                    rootNode = null;
                }
            }
        }

        var dataItems = new List<DataItemValue>();

        foreach ((string id, string? jsonPointer) in itemsWithPointers) {

            (VTQ vtq, string unit)? result = null;

            if (string.IsNullOrEmpty(jsonPointer)) {
                // No JSON pointer, use original behavior (parse entire payload)
                result = ParsePayloadBytes(topic, now, payloadBytes);
            }
            else if (rootNode != null) {
                // Extract value at JSON pointer path
                result = ExtractValueAtJsonPointer(rootNode, jsonPointer, topic, now);
            }
            // else: JSON pointer specified but payload isn't valid JSON - silently ignore

            if (result.HasValue) {
                dataItems.Add(new DataItemValue(id, result.Value.vtq));
            }
        }

        if (dataItems.Count > 0) {
            callback?.Notify_DataItemsChanged(dataItems.ToArray());
        }
    }

    private (VTQ vtq, string unit)? ParsePayloadBytes(string topic, Timestamp now, ReadOnlySequence<byte> payloadBytes) {

        if (payloadBytes.Length <= 0) {
            var vtq = VTQ.Make(DataValue.Empty, now, Quality.Good);
            string unit = "";
            return (vtq, unit);
        }

        string payload;
        try {
            payload = Encoding.UTF8.GetString(payloadBytes);
        }
        catch (Exception) {
            string err = $"Rejected invalid value for topic {topic}: Expected UTF8 string. Payload length: {payloadBytes.Length} bytes.";
            LogWarn("Value", err);
            return null;
        }

       return ParsePayloadString(topic, now, payload);
    }

    private (VTQ vtq, string unit)? ParsePayloadString(string topic, Timestamp now, string payload) {

        string unit = "";
        var vtq = VTQ.Make(DataValue.Empty, now, Quality.Good);

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

    /// <summary>
    /// Extract value from a JSON node at a specific JSON pointer path.
    /// Returns null if the path doesn't exist (silent ignore).
    /// </summary>
    private (VTQ vtq, string unit)? ExtractValueAtJsonPointer(JsonNode rootNode, string? jsonPointer, string topic, Timestamp now) {

        // Navigate to the target node using JSON pointer
        JsonNode? targetNode = NavigateJsonPointer(rootNode, jsonPointer ?? "");

        if (targetNode == null) {
            // Path not found - silently ignore
            return null;
        }

        // Now parse the target node as if it were the full payload
        string targetJson = targetNode.ToJsonString();
        return ParsePayloadString(topic, now, targetJson);
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

            if (mapDataItemID2TopicAndPointer.TryGetValue(id, out (string topic, string? jsonPointer) topicAndPointer)) {

                string topic = topicAndPointer.topic;
                // Note: JSON pointer is ignored for writes, entire value is published to the topic

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
                    prefixes.Insert(0, topicPrefix);
                }
                currentNodeID = itemToParentMap.GetValueOrDefault(currentNodeID);
            }
            else {
                break;
            }
        }

        // Build the effective topic
        if (prefixes.Count > 0) {
            prefixes.Add(address);
            return string.Join("", prefixes);
        }
        else {
            return address;
        }
    }

    /// <summary>
    /// Navigate to a specific path in a JsonNode using JSON Pointer notation.
    /// Example: "/params/p1" navigates to root["params"]["p1"]
    /// Returns null if the path doesn't exist.
    /// </summary>
    private static JsonNode? NavigateJsonPointer(JsonNode root, string jsonPointer) {

        if (string.IsNullOrEmpty(jsonPointer)) {
            return root;
        }

        // JSON Pointer should start with '/'
        if (!jsonPointer.StartsWith('/')) {
            return null;
        }

        // Remove leading "/" and split by "/"
        string[] pathSegments = jsonPointer.Substring(1).Split('/');

        JsonNode? current = root;

        foreach (string segment in pathSegments) {

            if (current == null) {
                return null;
            }

            // Unescape JSON Pointer special characters
            string unescapedSegment = segment.Replace("~1", "/").Replace("~0", "~");

            // Try to navigate to the next segment
            if (current is JsonObject obj) {
                if (!obj.TryGetPropertyValue(unescapedSegment, out JsonNode? next)) {
                    return null; // Property not found
                }
                current = next;
            }
            else if (current is JsonArray array) {
                // Array navigation - try to parse as integer index
                if (int.TryParse(unescapedSegment, out int index)) {
                    if (index >= 0 && index < array.Count) {
                        current = array[index];
                    }
                    else {
                        return null; // Index out of bounds
                    }
                }
                else {
                    return null; // Invalid array index
                }
            }
            else {
                return null; // Can't navigate further
            }
        }

        return current;
    }

    /// <summary>
    /// Parses the address to separate topic from JSON pointer notation.
    /// Examples:
    ///   "root/folder#/params/p1" -> ("root/folder", "/params/p1")
    ///   "root/folder/#" -> ("root/folder/#", null)
    ///   "root/folder/##/params/p1" -> ("root/folder/#", "/params/p1")
    ///   "root/folder" -> ("root/folder", null)
    /// </summary>
    private static (string topic, string? jsonPointer) ParseTopicAndJsonPointer(string topicWithPointer) {

        int idx = topicWithPointer.LastIndexOf("#/");
        if (idx == -1) {
            return (topicWithPointer, null);
        }

        string topic = topicWithPointer[..idx];
        string pointer = topicWithPointer[(idx + 1)..];
        return (topic, pointer);
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
    private readonly Lock lockObj = new();

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
