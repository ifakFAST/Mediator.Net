// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using MQTTnet.Client;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeMethodPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            var mqttOptions = MakeMqttOptions(certDir, config, "MethodPub");
            var methodPub = config.MethodPublish!;
            string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + methodPub.Topic;

            Connection clientFAST = await EnsureConnectOrThrow(info, null);

            Timestamp t = Time.GetNextNormalizedTimestamp(methodPub.PublishInterval, methodPub.PublishOffset);
            await Time.WaitUntil(t, abort: shutdown);

            IMqttClient? clientMQTT = null;

            while (!shutdown()) {

                clientFAST = await EnsureConnectOrThrow(info, clientFAST);

                DataValue value = DataValue.Empty;

                try {
                    value = await clientFAST.CallMethod(methodPub.ModuleID, methodPub.MethodName);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    Console.Error.WriteLine($"Failed to call method {methodPub.MethodName}: {e.Message}");
                }

                if (value.NonEmpty) {

                    clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);

                    if (clientMQTT != null) {

                        string payload = value.JSON;

                        var messages = MakeMessages(payload, topic, config.MaxPayloadSize);

                        try {
                            
                            foreach (var msg in messages) {
                                await clientMQTT.PublishAsync(msg);
                            }

                            if (methodPub.PrintPayload) {
                                Console.Out.WriteLine($"PUB: {topic}: {payload}");
                            }
                        }
                        catch (Exception exp) {
                            Exception e = exp.GetBaseException() ?? exp;
                            Console.Error.WriteLine($"Publish failed for topic {topic}: {e.Message}");
                        }
                    }
                }

                t = Time.GetNextNormalizedTimestamp(methodPub.PublishInterval, methodPub.PublishOffset);
                await Time.WaitUntil(t, abort: shutdown);
            }

            await clientFAST.Close();
            Close(clientMQTT);
        }
    }
}
