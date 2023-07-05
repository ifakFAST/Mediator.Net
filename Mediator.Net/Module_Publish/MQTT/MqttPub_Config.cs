// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using MQTTnet.Client;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish.MQTT;

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

        Connection clientFAST = await Util.EnsureConnectOrThrow(info, null, onConfigChanged, configPub.ModuleID);

        Func<bool> abortWait = () => {
            return configChanged || shutdown();
        };

        Timestamp t = Time.GetNextNormalizedTimestamp(configPub.PublishInterval, configPub.PublishOffset);
        await Time.WaitUntil(t, abort: abortWait);
        configChanged = false;

        IMqttClient? clientMQTT = null;

        while (!shutdown()) {

            clientFAST = await Util.EnsureConnectOrThrow(info, clientFAST, onConfigChanged, configPub.ModuleID);

            DataValue value = await clientFAST.CallMethod(configPub.ModuleID, "GetConfigString");

            clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);

            if (clientMQTT != null) {

                string payload = value.GetString() ?? "";

                var messages = MakeMessages(payload, topic, config.MaxPayloadSize);

                try {

                    foreach (var msg in messages) {
                        await clientMQTT.PublishAsync(msg);
                    }

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
