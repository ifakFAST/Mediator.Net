using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using static Ifak.Fast.Mediator.Publish.MQTT.MqttPublisher;

namespace Ifak.Fast.Mediator.Publish.MQTT;

internal class MqttPub_Var_Buffer : BufferedVarPub {

    private IMqttClient? clientMQTT = null;
    private readonly MqttClientOptions mqttOptions;
    private readonly MqttConfig config;
    private readonly MqttVarPub varPub;
    private readonly RegCache reg;

    public MqttPub_Var_Buffer(string dataFolder, string certDir, MqttConfig config) 
        : base(dataFolder, config.VarPublish!.BufferIfOffline) {

        this.config = config;
        this.varPub = config.VarPublish!;
        this.mqttOptions = MakeMqttOptions(certDir, config, "VarPub");
        string topicRegister = varPub.TopicRegistration.Trim() == "" ? "" : (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.TopicRegistration;
        this.reg = new RegCache(varPub, topicRegister);

        Start();
    }

    protected override string BuffDirName => "MQTT_Publish";
    protected override string PublisherID => config.ID;

    protected override async Task<bool> DoSend(VariableValues values) {

        clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);
        if (clientMQTT == null) { return false; }

        VariableValues changedValues = Util.RemoveEmptyTimestamp(values)
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

    private sealed class RegCache {

        private record ObjItem(VariableRef Var, JObject? Obj);

        private readonly HashSet<VariableRef> registeredVars = new();
        private readonly MqttVarPub varPub;
        private readonly string topic;

        public RegCache(MqttVarPub varPub, string topic) {
            this.varPub = varPub;
            this.topic = topic;
        }

        public async Task Register(IMqttClient clientMQTT, VariableValues allValues, MqttConfig config) {

            if (topic == "") return;

            string Now = Timestamp.Now.ToString();

            var newVarVals = allValues.Where(v => !registeredVars.Contains(v.Variable)).ToList();
            List<ObjItem> transformedValues = newVarVals.Select(vv => new ObjItem(vv.Variable, FromVariableValue(vv, varPub))).ToList();

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

                    foreach (ObjItem vv in chunck) {
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
            obj["Q"] = Util.MapQualityToNumber(vtq.Q);
        }
        else {
            obj["Q"] = vtq.Q.ToString();
        }

        obj["V"] = JToken.Parse(vtq.V.JSON);

        return obj;
    }

    private static T[] GetChunckByLimit<T>(List<T> list, int limit) {
        int sum = 0;
        for (int i = 0; i < list.Count; i++) {
            T obj = list[i];
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
}
