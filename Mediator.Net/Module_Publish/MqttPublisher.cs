using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet;
using System.Threading;
using Ifak.Fast.Mediator.Util;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using Ifak.Fast.Json.Linq;
using MQTTnet.Protocol;
using System.Text;

namespace Ifak.Fast.Mediator.Publish
{
    public class MqttPublisher
    {
        public static async Task MakeConfigPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            var mqttOptions = MakeMqttOptions(certDir, config);
            var configPub = config.ConfigPublish!;
            string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + configPub.Topic;

            bool configChanged = false;

            Action onConfigChanged = () => {
                Console.WriteLine("onConfigChanged called");
                configChanged = true;
            };

            Connection clientFAST = await EnsureConnectOrThrow(info, null, onConfigChanged, configPub.ModuleID);

            Func<bool> abortWait = () => {
                return configChanged || shutdown();
            };

            Timestamp t = Time.GetNextNormalizedTimestamp(configPub.PublishInterval, configPub.PublishOffset);
            await Time.WaitUntil(t, abort: abortWait);
            configChanged = false;

            IMqttClient? clientMQTT = null;

            while (!shutdown()) {

                clientFAST = await EnsureConnectOrThrow(info, clientFAST, onConfigChanged, configPub.ModuleID);

                DataValue value = await clientFAST.CallMethod(configPub.ModuleID, "GetConfigString");

                clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);

                if (clientMQTT != null) {

                    string payload = value.GetString() ?? "";

                    var messages = MakeMessages(payload, topic, config.MaxPayloadSize);

                    try {
                        await clientMQTT.PublishAsync(messages);
                        if (configPub.PrintPayload) {
                            Console.Out.WriteLine($"PUB: {topic}: {payload}");
                        }
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        Console.Error.WriteLine($"Publish failed for topic {topic}: {e.Message}");
                    }
                }

                t = Time.GetNextNormalizedTimestamp(configPub.PublishInterval, configPub.PublishOffset);
                await Time.WaitUntil(t, abort: abortWait);
                configChanged = false;
            }

            await clientFAST.Close();
            Close(clientMQTT);
        }

        private static List<MqttApplicationMessage> MakeMessages(string payload, string topicBase, int maxPayloadSize) {

            List<ReadOnlyMemory<byte>> listData = LargePayloadWriter.GetPayloadAndBuckets(payload, maxPayloadSize);

            var msgInfo = new MqttApplicationMessage() {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                Retain = true,
                Topic = $"{topicBase}/config/reported/info",
                PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                Payload = listData[0].ToArray(),
            };

            var res = new List<MqttApplicationMessage>(listData.Count);

            res.Add(msgInfo);

            for (int i = 1; i < listData.Count; ++i) {
                var msgBucket = new MqttApplicationMessage() {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                    Retain = true,
                    Topic = $"{topicBase}/config/reported/data{i - 1}",
                    PayloadFormatIndicator = MqttPayloadFormatIndicator.Unspecified,
                    Payload = listData[i].ToArray(),
                };
                res.Add(msgBucket);
            }

            return res;
        }

        public static async Task MakeVarRecTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            SynchronizationContext theSyncContext = SynchronizationContext.Current!;

            var mqttOptions = MakeMqttOptions(certDir, config);
            var varRec = config.VarReceive!;
            string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varRec.Topic;

            bool configChanged = false;

            Action onConfigChanged = () => {
                Console.WriteLine("MakeVarRecTask: onConfigChanged called");
                configChanged = true;
            };

            Connection clientFAST = await EnsureConnectOrThrow(info, null, onConfigChanged, varRec.ModuleID);

            while (!shutdown()) {

                IMqttClient? clientMQTT = await EnsureConnect(mqttOptions, null); ;

                if (clientMQTT == null) {
                    Console.WriteLine("Can not connect to MQTT broker");
                    await Task.Delay(5000);
                    continue;
                }

                clientFAST = await EnsureConnectOrThrow(info, clientFAST, onConfigChanged, varRec.ModuleID);

                List<ObjectInfo> objs = await clientFAST.GetAllObjects(varRec.ModuleID);

                ObjectInfo[] writableObjs = objs.Where(obj => obj.Variables.Any(v => v.Writable)).ToArray();

                MqttTopicFilter[] topics = writableObjs.Select(obj => new MqttTopicFilter() {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                    Topic = $"{topic}/{obj.ID.LocalObjectID}"
                }).ToArray();

                clientMQTT.UseApplicationMessageReceivedHandler((arg) => {
                    var promise = new TaskCompletionSource<bool>();
                    theSyncContext!.Post(_ => {
                        Task task = OnReceivedVarWriteRequest(varRec, clientFAST, arg);
                        task.ContinueWith(completedTask => promise.CompleteFromTask(completedTask));
                    }, null);
                    return promise.Task;
                });

                foreach (var top in topics) {
                    Console.WriteLine($"Subscribing to topic {top.Topic}");
                    await clientMQTT.SubscribeAsync(top);
                }

                while (!configChanged && !shutdown()) {

                    try {
                        await clientMQTT.PingAsync(CancellationToken.None);
                    }
                    catch (Exception) {
                        Console.Error.WriteLine("Connection broken. Trying to reconnect...");
                        break;
                    }

                    try {
                        clientFAST = await EnsureConnectOrThrow(info, clientFAST, onConfigChanged, varRec.ModuleID);
                    }
                    catch (Exception) {
                        Console.Error.WriteLine("Connection to FAST core broken. Trying to reconnect...");
                        break;
                    }

                    await Time.WaitUntil(Timestamp.Now + Duration.FromSeconds(6), abort: () => configChanged || shutdown());
                }

                configChanged = false;

                await CloseIntern(clientMQTT);
            }
        }

        private static async Task OnReceivedVarWriteRequest(MqttVarReceive mqtt, Connection clientFAST, MqttApplicationMessageReceivedEventArgs arg) {

            await arg.AcknowledgeAsync(CancellationToken.None);

            var msg = arg.ApplicationMessage;
            string objID = GetObjIdFromTopicName(msg.Topic);

            Console.WriteLine($"On got write req for {objID}");

            byte[] payloadBytes = msg.Payload;
            if (payloadBytes != null && payloadBytes.Length > 0) {

                string payload = Encoding.UTF8.GetString(msg.Payload);
                DataValue value = DataValue.FromJSON(payload);

                var variable = VariableRef.Make(mqtt.ModuleID, objID, "Value");
                VTQ vtq = VTQ.Make(value, Timestamp.Now, Quality.Good);
                try {
                    await clientFAST.WriteVariable(variable, vtq);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    Console.Error.WriteLine($"Failed to write variable {variable}: {e.Message}");
                }
            }
        }

        private static string GetObjIdFromTopicName(string topic) {
            int idx = topic.LastIndexOf('/');
            return topic.Substring(idx + 1);
        }

        public static async Task MakeVarPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            var mqttOptions = MakeMqttOptions(certDir, config);
            var varPub = config.VarPublish!;
            string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.Topic;

            Timestamp t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
            await Time.WaitUntil(t, abort: shutdown);

            ObjectRef objRoot = ObjectRef.Make(varPub.ModuleID, varPub.RootObject);

            Connection clientFAST = await EnsureConnectOrThrow(info, null);
            IMqttClient? clientMQTT = null;

            while (!shutdown()) {

                clientFAST = await EnsureConnectOrThrow(info, clientFAST);

                VariableValues values = await clientFAST.ReadAllVariablesOfObjectTree(objRoot);

                clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);

                if (clientMQTT != null) {

                    while (values.Count > 0) {

                        int BatchSize = Math.Min(varPub.PayloadLimit, values.Count);
                        var batch = values.Take(BatchSize).ToArray();
                        values.RemoveRange(0, BatchSize);

                        JObject[] payload = batch.Select(vv => FromVariableValue(vv, varPub)).ToArray();

                        string msg = StdJson.ObjectToString(payload);

                        try {
                            await clientMQTT.PublishAsync(topic, msg);
                            if (varPub.PrintPayload) {
                                Console.Out.WriteLine($"PUB: {topic}: {msg}");
                            }
                        }
                        catch (Exception exp) {
                            Exception e = exp.GetBaseException() ?? exp;
                            Console.Error.WriteLine($"Publish failed for topic {topic}: {e.Message}");
                            break;
                        }
                    }
                }

                t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
                await Time.WaitUntil(t, abort: shutdown);
            }

            await clientFAST.Close();
            Close(clientMQTT);
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

        private static async Task<Connection> EnsureConnectOrThrow(ModuleInitInfo info, Connection? client, Action? onConfigChanged = null, string? moduleID = null) {

            if (client != null && !client.IsClosed) {
                try {
                    await client.Ping();
                    return client;
                }
                catch (Exception) {
                    Task _ = client.Close();
                    return await EnsureConnectOrThrow(info, null);
                }
            }

            try {
                EventListener? listener = onConfigChanged == null ? null : new MyEventListener(onConfigChanged);
                Connection con = await HttpConnection.ConnectWithModuleLogin(info, listener);
                if (onConfigChanged != null && moduleID != null) {
                    var objRoot = await con.GetRootObject(moduleID);
                    await con.EnableConfigChangedEvents(objRoot.ID);
                }
                return con;
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                string msg = $"Failed ifakFAST connection: {e.GetType().FullName} {e.Message}";
                Console.Error.WriteLine(msg);
                throw new Exception(msg);
            }
        }

        private static async Task<IMqttClient?> EnsureConnect(IMqttClientOptions? mqttOptions, IMqttClient? mqttClient) {

            if (mqttClient != null && mqttClient.IsConnected) {
                return mqttClient;
            }

            Close(mqttClient);

            var factory = new MqttFactory();
            var client = factory.CreateMqttClient();

            try {
                await client.ConnectAsync(mqttOptions, CancellationToken.None);
                return client;
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                Console.Error.WriteLine($"Failed MQTT connection: {e.Message}");
                client.Dispose();
                return null;
            }
        }

        private static void Close(IMqttClient? mqttClient) {

            if (mqttClient != null) {
                Task _ = CloseIntern(mqttClient);
            }
        }

        private static async Task CloseIntern(IMqttClient client) {
            try {
                await client.DisconnectAsync();
            }
            catch (Exception) { }
            try {
                client.Dispose();
            }
            catch (Exception) { }
        }

        private static IMqttClientOptions MakeMqttOptions(string certDir, MqttConfig config) {

            var builder = new MqttClientOptionsBuilder()
                .WithClientId(config.ClientID)
                .WithTcpServer(config.Endpoint);

            if (config.CertFileCA != "") {

                var caCert = X509Certificate.CreateFromCertFile(Path.Combine(certDir, config.CertFileCA));
                var clientCert = new X509Certificate2(Path.Combine(certDir, config.CertFileClient), "");

                builder = builder
                .WithTls(new MqttClientOptionsBuilderTlsParameters() {
                    UseTls = true,
                    SslProtocol = System.Security.Authentication.SslProtocols.Tls12,
                    Certificates = new List<X509Certificate>()
                    {
                        clientCert,
                        caCert
                    }
                });
            }

            return builder
                .WithCleanSession()
                .Build();
        }

    }

    public class MyEventListener : EventListener
    {
        private readonly Action onConfigChanged;

        public MyEventListener(Action onConfigChanged) {
            this.onConfigChanged = onConfigChanged;
        }

        public Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
            return Task.FromResult(true);
        }

        public Task OnConfigChanged(List<ObjectRef> changedObjects) {
            onConfigChanged();
            return Task.FromResult(true);
        }

        public Task OnConnectionClosed() {
            return Task.FromResult(true);
        }

        public Task OnVariableHistoryChanged(List<HistoryChange> changes) {
            return Task.FromResult(true);
        }

        public Task OnVariableValueChanged(VariableValues variables) {
            return Task.FromResult(true);
        }
    }

    internal static class TaskUtil
    {
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
}
