using System;
using System.Threading.Tasks;
using MQTTnet.Client;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeConfigPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            var mqttOptions = MakeMqttOptions(certDir, config, "ConfigPub");
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
    }
}
