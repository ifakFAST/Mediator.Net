using System;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet.Client;
using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Publish
{
    public partial class MqttPublisher
    {
        public static async Task MakeVarPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

            var mqttOptions = MakeMqttOptions(certDir, config, "VarPub");
            var varPub = config.VarPublish!;
            string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.Topic;
            string topicRegister = varPub.TopicRegistration.Trim() == "" ? "" : (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.TopicRegistration;

            Timestamp t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
            await Time.WaitUntil(t, abort: shutdown);

            ObjectRef objRoot = ObjectRef.Make(varPub.ModuleID, varPub.RootObject);

            RegCache reg = new RegCache(varPub, topicRegister);

            Connection clientFAST = await EnsureConnectOrThrow(info, null);
            IMqttClient? clientMQTT = null;

            while (!shutdown()) {

                clientFAST = await EnsureConnectOrThrow(info, clientFAST);

                VariableValues values = Filter(await clientFAST.ReadAllVariablesOfObjectTree(objRoot), varPub);

                clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);

                if (clientMQTT != null) {

                    await reg.Register(clientMQTT, values);

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

        private static VariableValues Filter(VariableValues values, MqttVarPub config) {

            bool numsOnly = config.NumericTagsOnly;
            bool sendNull = config.SendTagsWithNull;

            if (!numsOnly && sendNull) {
                return values;
            }

            var res = new VariableValues(values.Count);
            foreach (var vv in values) {

                if (numsOnly && !sendNull) {
                    double? dbl = vv.Value.V.AsDouble();
                    if (dbl.HasValue) {
                        res.Add(vv);
                    }
                }
                else if (numsOnly && sendNull) {
                    double? dbl = vv.Value.V.AsDouble();
                    if (dbl.HasValue || vv.Value.V.IsEmpty) {
                        res.Add(vv);
                    }
                }
                else if (!numsOnly && !sendNull) {
                    if (!vv.Value.V.IsEmpty) {
                        res.Add(vv);
                    }
                }
            }
            return res;
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


        public class RegCache
        {
            private readonly HashSet<VariableRef> registeredVars = new HashSet<VariableRef>();
            private MqttVarPub varPub;
            private string topic;

            public RegCache(MqttVarPub varPub, string topic) {
                this.varPub = varPub;
                this.topic = topic;
            }

            public async Task Register(IMqttClient clientMQTT, VariableValues allValues) {

                if (topic == "") return;

                var newVarVals = allValues.Where(v => !registeredVars.Contains(v.Variable)).ToList();

                while (newVarVals.Count > 0) {

                    int BatchSize = Math.Min(varPub.PayloadLimit, newVarVals.Count);
                    var batch = newVarVals.Take(BatchSize).ToArray();
                    newVarVals.RemoveRange(0, BatchSize);

                    JObject[] payload = batch.Select(vv => FromVariableValue(vv, varPub)).ToArray();

                    string msg = StdJson.ObjectToString(payload);

                    try {
                        await clientMQTT.PublishAsync(topic, msg);

                        foreach (var vv in batch) {
                            registeredVars.Add(vv.Variable);
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
    }
}
