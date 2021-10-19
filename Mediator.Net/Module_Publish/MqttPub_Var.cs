using System;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet.Client;
using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeVarPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            var mqttOptions = MakeMqttOptions(certDir, config, "VarPub");
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
    }
}
