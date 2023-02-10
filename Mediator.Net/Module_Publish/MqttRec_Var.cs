using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Packets;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeVarRecTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            SynchronizationContext theSyncContext = SynchronizationContext.Current!;

            var mqttOptions = MakeMqttOptions(certDir, config, "VarRec");
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

                clientMQTT.ApplicationMessageReceivedAsync += e => {
                    var promise = new TaskCompletionSource<bool>();
                    theSyncContext!.Post(_ => {
                        Task task = OnReceivedVarWriteRequest(varRec, clientFAST, e);
                        task.ContinueWith(completedTask => promise.CompleteFromTask(completedTask));
                    }, null);
                    return promise.Task;
                };

                foreach (var top in topics) {
                    Console.WriteLine($"Subscribing to topic {top.Topic}");
                    await clientMQTT.SubscribeAsync(top);
                }

                while (!configChanged && !shutdown()) {

                    try {
                        await clientMQTT.PingAsync(CancellationToken.None);
                    }
                    catch (Exception exp) {
                        Exception e = exp.GetBaseException() ?? exp;
                        Console.Error.WriteLine($"MakeVarRecTask: Connection broken during Ping. Trying to reconnect. Err: {e.Message}");
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

                string payload = Encoding.UTF8.GetString(payloadBytes);
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

    }
}
