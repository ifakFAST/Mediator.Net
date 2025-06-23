using MQTTnet;
using MQTTnet.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish.MQTT;

internal static class MqttPub_Var_Util {

    public static JObject FromVariableValue(VariableValue vv, MqttVarPub config) {

        var obj = new JObject();

        obj["ID"] = GetVariableId(vv);

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

    public static string GetVariableId(VariableValue vv) {
        VariableRef vref = vv.Variable;
        if (vref.Name == "Value") {
            return vref.Object.LocalObjectID;
        }
        else {
            return vref.Object.LocalObjectID + "." + vref.Name;
        }
    }

    public static T[] GetChunckByLimit<T>(List<T> list, int limit) {
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

internal sealed class MqttRegCache {

    private record ObjItem(VariableRef Var, JObject? Obj);

    private readonly HashSet<VariableRef> registeredVars = new();
    private readonly MqttVarPub varPub;
    private readonly string topic;

    public MqttRegCache(MqttVarPub varPub, string topic) {
        this.varPub = varPub;
        this.topic = topic;
    }

    public async Task Register(IMqttClient clientMQTT, VariableValues allValues, MqttConfig config) {

        if (topic == "") return;

        string Now = Timestamp.Now.ToString();

        var newVarVals = allValues.Where(v => !registeredVars.Contains(v.Variable)).ToList();
        List<ObjItem> transformedValues = newVarVals.Select(vv => new ObjItem(vv.Variable, MqttPub_Var_Util.FromVariableValue(vv, varPub))).ToList();

        while (transformedValues.Count > 0) {

            ObjItem[] chunck = MqttPub_Var_Util.GetChunckByLimit(transformedValues, config.MaxPayloadSize - 100);

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