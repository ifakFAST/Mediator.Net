using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet.Client;
using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using System.Collections.Generic;
using Ifak.Fast.Mediator.BinSeri;
using MQTTnet;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeVarPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {
            
            var varPub = config.VarPublish!;
            var objRoot = ObjectRef.Make(varPub.ModuleID, varPub.RootObject);

            Timestamp t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
            await Time.WaitUntil(t, abort: shutdown);            

            Connection clientFAST = await EnsureConnectOrThrow(info, null);

            var publisher = new BufferedPubMQTT(info.DataFolder, certDir, config);

            var lastValues = new Dictionary<VariableRef, VTQ>();

            while (!shutdown()) {

                clientFAST = await EnsureConnectOrThrow(info, clientFAST);

                VariableValues values = RemoveEmptyT(Filter(await clientFAST.ReadAllVariablesOfObjectTree(objRoot), varPub));

                if (values.Count > 0) {
                    publisher.Post(new ChunkOfValues(t, values));
                }

                t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
                await Time.WaitUntil(t, abort: shutdown);
            }

            await clientFAST.Close();
            publisher.Close();
        }

        private static T[] GetChunckByLimit<T>(List<T> list, int limit) {
            int sum = 0;
            for (int i = 0; i < list.Count; i++) {
                var obj = list[i];
                string str = StdJson.ObjectToString(obj);
                sum += str.Length + 1;
                if (sum >= limit) {
                    if (i > 0) {
                        var res = list.Take(i).ToArray();
                        list.RemoveRange(0, i);
                        return res;
                    }
                    else {
                        list.RemoveAt(0); // drop the first item (already to big)
                        return GetChunckByLimit(list, limit);
                    }
                }
            }
            var result = list.ToArray();
            list.Clear();
            return result;
        }

        private static VariableValues Filter(VariableValues values, MqttVarPub config) {

            bool simpleOnly = config.SimpleTagsOnly;
            bool sendNull = config.SendTagsWithNull;

            if (!simpleOnly && sendNull) {
                return values;
            }

            var res = new VariableValues(values.Count);
            foreach (var vv in values) {

                DataValue v = vv.Value.V;

                if (simpleOnly && !sendNull) {
                    if (!v.IsArray && !v.IsObject && v.NonEmpty) {
                        res.Add(vv);
                    }
                }
                else if (simpleOnly && sendNull) {
                    if (!v.IsArray && !v.IsObject) {
                        res.Add(vv);
                    }
                }
                else if (!simpleOnly && !sendNull) {
                    if (v.NonEmpty) {
                        res.Add(vv);
                    }
                }
            }
            return res;
        }

        private static VariableValues RemoveEmptyT(VariableValues values) {
            if (values.All(vv => vv.Value.T.NonEmpty)) {
                return values;
            }
            return values.Where(vv => vv.Value.T.NonEmpty).ToList();
        }

        private static JObject FromVariableValue(VariableValue vv, MqttVarPub config) {

            var obj = new JObject();
            obj["ID"] = vv.Variable.Object.LocalObjectID;

            VTQ vtq = vv.Value;

            if (config.TimeAsUnixMilliseconds) {
                obj["T"] = vtq.T.JavaTicks;
            }
            else {
                obj["T"] = vtq.T.ToString();
            }

            if (config.QualityNumeric) {
                obj["Q"] = MapQualityToNumber(vtq.Q);
            }
            else {
                obj["Q"] = vtq.Q.ToString();
            }

            obj["V"] = JToken.Parse(vtq.V.JSON);

            return obj;
        }

        private static int MapQualityToNumber(Quality q) {
            switch (q) {
                case Quality.Good: return 1;
                case Quality.Bad: return 0;
                case Quality.Uncertain: return 2;
            }
            return 0;
        }

        //private static void Print(string s) {
        //    Console.WriteLine($"Thread {System.Threading.Thread.CurrentThread.ManagedThreadId}: {s}");
        //}

        public class RegCache
        {
            private readonly HashSet<VariableRef> registeredVars = new HashSet<VariableRef>();
            private MqttVarPub varPub;
            private string topic;

            public RegCache(MqttVarPub varPub, string topic) {
                this.varPub = varPub;
                this.topic = topic;
            }

            public async Task Register(IMqttClient clientMQTT, VariableValues allValues, MqttConfig config) {

                if (topic == "") return;

                string Now = Timestamp.Now.ToString();

                var newVarVals = allValues.Where(v => !registeredVars.Contains(v.Variable)).ToList();
                List<ObjItem> transformedValues = newVarVals.Select(vv => ObjItem.Make(vv.Variable, FromVariableValue(vv, varPub))).ToList();

                while (transformedValues.Count > 0) {

                    ObjItem[] chunck = GetChunckByLimit(transformedValues, config.MaxPayloadSize - 100);                    

                    string msg;
                    if (varPub.PubFormatReg == PubVarFormat.Object) {
                        var wrappedPayload = new {
                            now = Now,
                            tags = chunck.Select(obj => obj.Obj)
                        };
                        msg = StdJson.ObjectToString(wrappedPayload);
                    }
                    else {
                        msg = StdJson.ObjectToString(chunck.Select(obj => obj.Obj));
                    }

                    try {

                        var applicationMessage = new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(msg)
                            .Build();

                        await clientMQTT.PublishAsync(applicationMessage);

                        foreach (var vv in chunck) {
                            registeredVars.Add(vv.Var);
                        }

                        if (varPub.PrintPayload) {
                            Console.Out.WriteLine($"REG PUB: {topic}: {msg}");
                        }
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        Console.Error.WriteLine($"Reg Publish failed for topic {topic}: {e.Message}");
                        break;
                    }
                }
            }
        }

        public class ObjItem {
            public VariableRef Var { get; set; }
            public JObject? Obj { get; set; }
            public static ObjItem Make(VariableRef v, JObject obj) {
                return new ObjItem() {
                    Obj = obj,
                    Var = v,
                };
            }
        }

        public struct ChunkOfValues {

            public readonly Timestamp Time;
            public readonly VariableValues Values;

            public ChunkOfValues(Timestamp time, VariableValues values) {
                Time = time;
                Values = values;
            }
        }

        public class BufferedPubMQTT {

            enum State {
                Online,
                TransitionToBuffered,
                Buffered,
                TransitionToOnline,
            }

            private readonly AsyncQueue<ChunkOfValues> queue = new AsyncQueue<ChunkOfValues>();
            private State state;

            private IMqttClient? clientMQTT = null;
            private readonly MqttClientOptions mqttOptions;
            private readonly MqttConfig config;
            private readonly MqttVarPub varPub;
            private readonly RegCache reg;
            private readonly string dataFolder;

            private bool running = true;

            public BufferedPubMQTT(string dataFolder, string certDir, MqttConfig config) {
                this.dataFolder = dataFolder;
                this.mqttOptions = MakeMqttOptions(certDir, config, "VarPub");
                this.config = config;
                this.varPub = config.VarPublish!;
                string topicRegister = varPub.TopicRegistration.Trim() == "" ? "" : (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.TopicRegistration;
                this.reg = new RegCache(varPub, topicRegister);
                this.state = varPub.BufferIfOffline && HasBufferedChuncks() ? State.Buffered : State.Online;
                _ = Runner();
                _ = ClearBufferRunner();
            }

            private void SetState_Online() {
                if (state != State.Online) {
                    state = State.Online;
                    Console.WriteLine("Changing MQTT buffered state to Online");
                }
            }

            private void SetState_TransitionToBuffered() {
                if (state != State.TransitionToBuffered) {
                    state = State.TransitionToBuffered;
                    Console.WriteLine("Changing MQTT buffered state to TransitionToBuffered");
                }
            }

            private void SetState_Buffered() {
                if (state != State.Buffered) {
                    state = State.Buffered;
                    Console.WriteLine("Changing MQTT buffered state to Buffered");
                }
            }

            private void SetState_TransitionToOnline() {
                if (state != State.TransitionToOnline) {
                    state = State.TransitionToOnline;
                    Console.WriteLine("Changing MQTT buffered state to TransitionToOnline");
                }
            }

            public void Close() {
                running = false;
            }

            public void Post(ChunkOfValues chunk) {

                queue.Post(chunk);
            }

            public async Task Runner() {

                while (running) {

                    ChunkOfValues chunk = await queue.ReceiveAsync();                   

                    if (varPub.BufferIfOffline || queue.Count == 0) {
                        await Process(chunk);
                    }
                }
            }

            private async Task Process(ChunkOfValues chunk) {

                switch (state) {

                    case State.Online:
                        await Send(chunk);
                        break;

                    case State.Buffered:
                        await Buffer(chunk);
                        break;

                    case State.TransitionToBuffered:
                        while (state == State.TransitionToBuffered) {
                            await Task.Delay(100);
                        }
                        await Process(chunk);
                        break;                   

                    case State.TransitionToOnline:
                        while (state == State.TransitionToOnline) {
                            await Task.Delay(500);
                        }
                        await Process(chunk);
                        break;                           
                }
            }

            private async Task Send(ChunkOfValues chunk) {

                bool sendOK = await Send(clientMQTT, chunk.Values);

                // Print($"Send completed ok={sendOK}");

                if (!sendOK && varPub.BufferIfOffline) {

                    SetState_TransitionToBuffered();
                    await Buffer(chunk);
                    SetState_Buffered();
                }
            }

            private readonly Dictionary<VariableRef, VTQ>  lastSentValues = new Dictionary<VariableRef, VTQ>();

            private async Task<bool> Send(IMqttClient? clientMQTT, VariableValues values) {                

                clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);
                if (clientMQTT == null) { return false; }

                VariableValues changedValues = values
                  .Where(v => !lastSentValues.ContainsKey(v.Variable) || lastSentValues[v.Variable] != v.Value)
                  .ToList();

                string Now = Timestamp.Now.ToString();
                string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.Topic;

                await reg.Register(clientMQTT, changedValues, config);

                List<JObject> transformedValues = changedValues.Select(vv => FromVariableValue(vv, varPub)).ToList();

                while (transformedValues.Count > 0) {

                    JObject[] payload = GetChunckByLimit(transformedValues, config.MaxPayloadSize - 100);

                    string msg;
                    if (varPub.PubFormat == PubVarFormat.Object) {
                        var wrappedPayload = new {
                            now = Now,
                            tags = payload
                        };
                        msg = StdJson.ObjectToString(wrappedPayload);
                    }
                    else {
                        msg = StdJson.ObjectToString(payload);
                    }

                    try {
                        var applicationMessage = new MqttApplicationMessageBuilder()
                            .WithTopic(topic)
                            .WithPayload(msg)
                            .Build();

                        await clientMQTT.PublishAsync(applicationMessage);
                        if (varPub.PrintPayload) {
                            Console.Out.WriteLine($"PUB: {topic}: {msg}");
                        }
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        Console.Error.WriteLine($"Publish failed for topic {topic}: {e.Message}");
                        return false;
                    }
                }

                foreach (var vv in changedValues) {
                    lastSentValues[vv.Variable] = vv.Value;
                }

                return true;
            }

            private const string BuffDir = "MQTT_Buffer";
            private const string FilePrefix = "MQTT_";
            private const string FileSuffix = ".dat";

            private async Task Buffer(ChunkOfValues chunk) {

                string folder = Path.Combine(dataFolder, BuffDir);

                try {
                    Directory.CreateDirectory(folder);
                }
                catch {
                    return;
                }

                string file = $"{FilePrefix}{chunk.Time.JavaTicks}{FileSuffix}";
                string path = Path.Combine(folder, file);

                // Print($"Buffer... {file}");

                for (int i = 0; i < 3; i++) {

                    try {
                        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
                        VariableValue_Serializer.Serialize(fileStream, chunk.Values, Common.CurrentBinaryVersion);
                        fileStream.Close();
                        return;
                    }
                    catch {

                        try {
                            File.Delete(path);
                        }
                        catch { }

                        await Task.Delay(250);
                    }
                }
            }

            public async Task ClearBufferRunner() {

                while (running) {

                    if (state == State.Online) {
                        await Task.Delay(TimeSpan.FromSeconds(5));
                        continue;
                    }
                    else if (state == State.TransitionToBuffered) {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                        continue;
                    }

                    // Print("ClearBufferRunner read chunck...");

                    DataChunk? chunck = await ReadNextBufferedChunck();

                    if (chunck == null) {
                        SetState_Online();
                        continue;
                    }

                    int retry = 1;

                    while (running) {

                        bool sendOK = await Send(clientMQTT, chunck.Values);
                        if (sendOK) {
                            SetState_TransitionToOnline();
                            chunck.Delete();
                            break;
                        }
                        else {
                            SetState_Buffered();
                            int delaySeconds = Math.Min(15, retry++);
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        }
                    }
                }
            }

            private async Task<DataChunk?> ReadNextBufferedChunck() {

                DataChunk? chunk = ReadNextBufferedChunckIntern();
                if (chunk != null) return chunk;

                await Task.Delay(TimeSpan.FromMilliseconds(100));

                chunk = ReadNextBufferedChunckIntern();
                if (chunk != null) return chunk;

                await Task.Delay(TimeSpan.FromMilliseconds(300));

                return ReadNextBufferedChunckIntern();
            }

            private bool HasBufferedChuncks() {
                try {
                    foreach (string file in EnumBufferedFiles()) {
                        return true;
                    }
                }
                catch { }
                return false;
            }

            private DataChunk? ReadNextBufferedChunckIntern() {

                try {

                    IEnumerable<string> files = EnumBufferedFiles();

                    string fileNext = "";
                    long ticksNext = long.MaxValue;

                    foreach (string file in files) {
                        long ticks = JavaTicksFromFileName(file, long.MaxValue);
                        if (ticks < ticksNext) {
                            ticksNext = ticks;
                            fileNext = file;
                        }
                    }

                    if (fileNext == "") {
                        return null;
                    }

                    byte[] data = File.ReadAllBytes(fileNext);
                    using var stream = new MemoryStream(data);

                    try {
                        VariableValues values = VariableValue_Serializer.Deserialize(stream);
                        return new DataChunk(values, fileNext);
                    }
                    catch {
                        File.Delete(fileNext);
                        return ReadNextBufferedChunckIntern();
                    }
                }
                catch {
                    return null;
                }
            }

            private IEnumerable<string> EnumBufferedFiles() {

                try {

                    string path = Path.Combine(dataFolder, BuffDir);

                    if (!Directory.Exists(path)) {
                        return new string[0];
                    }

                    return Directory.EnumerateFiles(path, $"{FilePrefix}*{FileSuffix}");
                }
                catch (Exception ex) {
                    Console.Error.WriteLine($"EnumBufferedFiles: {ex.Message}");
                    return new string[0];
                }
            }

            private static long JavaTicksFromFileName(string str, long defaultValue) {
                try {
                    int i = str.LastIndexOf(FilePrefix);
                    if (i < 0) { return defaultValue; }
                    i += FilePrefix.Length;
                    int j = str.LastIndexOf(FileSuffix);
                    if (j < 0) { return defaultValue; }
                    ReadOnlySpan<char> span = str;
                    ReadOnlySpan<char> spanNum = span.Slice(i, j - i);
                    if (long.TryParse(spanNum, out var t)) { return t; }
                    return defaultValue;
                }
                catch { return defaultValue; }
            }

            public class DataChunk {

                public readonly VariableValues Values;
                private readonly string FileName;

                public DataChunk(VariableValues values, string fileName) {
                    Values = values;
                    FileName = fileName;
                }

                public void Delete() {
                    try { 
                        File.Delete(FileName); 
                    }
                    catch { }
                }
            }
        }
    }
}
