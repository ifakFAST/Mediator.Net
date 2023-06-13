using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Packets;
using MQTTnet.Server;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeConfigRecTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            SynchronizationContext theSyncContext = SynchronizationContext.Current!;

            var mqttOptions = MakeMqttOptions(certDir, config, "ConfigRec");
            var configRec = config.ConfigReceive!;
            string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + configRec.Topic;

            Connection clientFAST = await EnsureConnectOrThrow(info, null);

            while (!shutdown()) {

                IMqttClient? clientMQTT = await EnsureConnect(mqttOptions, null); ;

                if (clientMQTT == null) {
                    Console.WriteLine("Can not connect to MQTT broker");
                    await Task.Delay(5000);
                    continue;
                }

                clientFAST = await EnsureConnectOrThrow(info, clientFAST);
                var reader = new LargePayloadReader(configRec.MaxBuckets);

                clientMQTT.ApplicationMessageReceivedAsync += e => {
                    var promise = new TaskCompletionSource<bool>();
                    theSyncContext!.Post(_ => {
                        Task task = OnReceivedConfigWriteRequest(clientMQTT, reader, configRec.ModuleID, topic, clientFAST, e);
                        task.ContinueWith(completedTask => promise.CompleteFromTask(completedTask));
                    }, null);
                    return promise.Task;
                };

                var topics = GetTopicsToSubscribe(topic, bucktesCount: configRec.MaxBuckets);

                foreach (var top in topics) {
                    Console.WriteLine($"Subscribing to topic {top.Topic}");
                    await clientMQTT.SubscribeAsync(top);
                }

                while (!shutdown()) {

                    try {
                        await clientMQTT.PingAsync(CancellationToken.None);
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        Console.Error.WriteLine($"MakeConfigRecTask: Connection broken during Ping. Trying to reconnect. Err: {e.Message}");
                        break;
                    }

                    try {
                        clientFAST = await EnsureConnectOrThrow(info, clientFAST);
                    }
                    catch (Exception) {
                        Console.Error.WriteLine("Connection to FAST core broken. Trying to reconnect...");
                        break;
                    }

                    await Time.WaitUntil(Timestamp.Now + Duration.FromSeconds(6), abort: shutdown);
                }

                await CloseIntern(clientMQTT);
            }
        }

        private static List<MqttTopicFilter> GetTopicsToSubscribe(string topicBase, int bucktesCount) {

            var res = new List<MqttTopicFilter>(bucktesCount + 1);

            var topicInfo = new MqttTopicFilter() {
                QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                Topic = $"{topicBase}/info"
            };

            res.Add(topicInfo);

            for (int i = 1; i <= bucktesCount; ++i) {
                var topicBucket = new MqttTopicFilter() {
                    QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                    Topic = $"{topicBase}/data{i - 1}"
                };
                res.Add(topicBucket);
            }

            return res;
        }

        private static async Task OnReceivedConfigWriteRequest(IMqttClient clientMQTT, LargePayloadReader reader, string moduleID, string topicBase, Connection clientFAST, MqttApplicationMessageReceivedEventArgs arg) {

            var msg = arg.ApplicationMessage;

            await arg.AcknowledgeAsync(CancellationToken.None);

            if (msg.Topic.EndsWith("/info")) {
                reader.SetInfo(msg.PayloadSegment);
                string payload = Encoding.UTF8.GetString(msg.PayloadSegment);
                Console.WriteLine($"Got Info msg! ClientID: {arg.ClientId}; Topic: {msg.Topic}; QOS: {msg.QualityOfServiceLevel}; Payload: {payload}");
            }
            else {

                int bucket = GetBucketNumberFromTopicName(msg.Topic);
                reader.SetBucket(bucket, msg.PayloadSegment.ToArray());

                Console.WriteLine($"Got Data msg! ClientID: {arg.ClientId}; Topic: {msg.Topic}; Bucket: {bucket}; QOS: {msg.QualityOfServiceLevel}; Payload.Len: {msg.PayloadSegment.Count}");
            }

            string? content = reader.Content();

            if (content != null) {

                try {
                    var nv = new NamedValue("config", content);
                    DataValue value = await clientFAST.CallMethod(moduleID, "SetConfigString", nv);
                    Console.WriteLine("Stored new config!");
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    Console.Error.WriteLine($"Failed to set new config: {e.Message}");

                    byte[] payload = StdJson.ObjectToBytes(new {
                        Hash = reader.ContentHash,
                        Error = e.Message,
                        Time = Timestamp.Now.ToString()
                    }, indented: true);

                    var msgInfo = new MqttApplicationMessage() {
                        QualityOfServiceLevel = MqttQualityOfServiceLevel.AtLeastOnce,
                        Retain = true,
                        Topic = $"{topicBase}/error",
                        PayloadFormatIndicator = MqttPayloadFormatIndicator.CharacterData,
                        PayloadSegment = payload,
                    };

                    await clientMQTT.PublishAsync(msgInfo);
                }
            }
        }

        private static int GetBucketNumberFromTopicName(string topic) {
            const string prefix = "data";
            int idx = topic.LastIndexOf(prefix);
            string num = topic.Substring(idx + prefix.Length);
            return int.Parse(num);
        }

    }
}
