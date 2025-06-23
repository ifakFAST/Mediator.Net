using MQTTnet;
using MQTTnet.Client;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using static Ifak.Fast.Mediator.Publish.MQTT.MqttPublisher;

namespace Ifak.Fast.Mediator.Publish.MQTT;

internal class MqttPub_Var_PerVariable : BufferedVarPub {

    private IMqttClient? clientMQTT = null;
    private readonly MqttClientOptions mqttOptions;
    private readonly MqttConfig config;
    private readonly MqttVarPub varPub;
    private readonly MqttRegCache reg;

    public MqttPub_Var_PerVariable(string dataFolder, string certDir, MqttConfig config) 
        : base(dataFolder, config.VarPublish!.BufferIfOffline) {

        this.config = config;
        this.varPub = config.VarPublish!;
        this.mqttOptions = MakeMqttOptions(certDir, config, "VarPub");
        string topicRegister = varPub.TopicRegistration.Trim() == "" ? "" : (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.TopicRegistration;
        this.reg = new MqttRegCache(varPub, topicRegister);

        Start();
    }

    protected override string BuffDirName => "MQTT_Publish_PerVar";
    internal override string PublisherID => config.ID + "_PerVar";

    protected override async Task<bool> DoSend(VariableValues values) {

        clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);
        if (clientMQTT == null) { return false; }

        VariableValues changedValues = Util.RemoveEmptyTimestamp(values)
          .Where(v => !lastSentValues.ContainsKey(v.Variable) || lastSentValues[v.Variable] != v.Value)
          .ToList();

        await reg.Register(clientMQTT, changedValues, config);

        // Publish each variable to its own topic
        foreach (var vv in changedValues) {

            string topic = BuildTopicForVariable(vv);

            try {

                VarInfo v = GetVariableInfoOrThrow(vv.Variable);
                JObject payload = FromVariableValue(vv, v, varPub);

                string msg = StdJson.ObjectToString(payload);
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

    private static JObject FromVariableValue(VariableValue vv, VarInfo info, MqttVarPub config) {

        var obj = new JObject();

        VTQ vtq = vv.Value;

        obj["Name"] = info.Name;

        if (!string.IsNullOrWhiteSpace(info.Variable.Unit)) {
            obj["Unit"] = info.Variable.Unit;
        }

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

    private string BuildTopicForVariable(VariableValue vv) {
        string variableId = MqttPub_Var_Util.GetVariableId(vv);
        string topicTemplate = varPub.TopicTemplate;
        string topic = topicTemplate.Replace("{ID}", EncodeTopic(variableId));
        return (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + topic;
    }

    private static string EncodeTopic(string topic) {

        if (string.IsNullOrEmpty(topic))
            return topic;

        // Worst-case each char becomes three characters ("%XX")
        var sb = new StringBuilder(topic.Length * 3);

        foreach (char c in topic) {
            switch (c) {
                case '+': sb.Append("%2B"); break;
                case '#': sb.Append("%23"); break;
                case '/': sb.Append("%2F"); break;
                case '%': sb.Append("%25"); break;
                case ' ': sb.Append("%20"); break;
                default: sb.Append(c); break;
            }
        }

        return sb.ToString();
    }
}
