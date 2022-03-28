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

                string Now = Timestamp.Now.ToString();

                VariableValues values = Filter(await clientFAST.ReadAllVariablesOfObjectTree(objRoot), varPub);

                clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);

                if (clientMQTT != null) {

                    await reg.Register(clientMQTT, values, config);

                    List<JObject> transformedValues = values.Select(vv => FromVariableValue(vv, varPub)).ToList();

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
                        await clientMQTT.PublishAsync(topic, msg);

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
    }
}
