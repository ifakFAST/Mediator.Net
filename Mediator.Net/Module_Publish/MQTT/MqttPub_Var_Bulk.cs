using MQTTnet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using static Ifak.Fast.Mediator.Publish.MQTT.MqttPublisher;

namespace Ifak.Fast.Mediator.Publish.MQTT;

internal class MqttPub_Var_Bulk : BufferedVarPub {

    private IMqttClient? clientMQTT = null;
    private readonly MqttClientOptions mqttOptions;
    private readonly MqttConfig config;
    private readonly MqttVarPub varPub;
    private readonly MqttRegCache reg;

    public MqttPub_Var_Bulk(string dataFolder, string certDir, MqttConfig config) 
        : base(dataFolder, config.VarPublish!.BufferIfOffline) {

        this.config = config;
        this.varPub = config.VarPublish!;
        this.mqttOptions = MakeMqttOptions(certDir, config, "VarPub");
        string topicRegister = varPub.TopicRegistration.Trim() == "" ? "" : (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.TopicRegistration;
        this.reg = new MqttRegCache(varPub, topicRegister);

        Start();
    }

    protected override string BuffDirName => "MQTT_Publish";
    internal override string PublisherID => config.ID;

    protected override async Task<bool> DoSend(VariableValues values) {

        clientMQTT = await EnsureConnect(mqttOptions, clientMQTT);
        if (clientMQTT == null) { return false; }

        VariableValues changedValues = Util.RemoveEmptyTimestamp(values)
          .Where(v => !lastSentValues.ContainsKey(v.Variable) || lastSentValues[v.Variable] != v.Value)
          .ToList();

        string Now = Timestamp.Now.ToString();
        string topic = (string.IsNullOrEmpty(config.TopicRoot) ? "" : config.TopicRoot + "/") + varPub.Topic;

        await reg.Register(clientMQTT, changedValues, config);

        List<JObject> transformedValues = changedValues.Select(vv => MqttPub_Var_Util.FromVariableValue(vv, varPub)).ToList();

        while (transformedValues.Count > 0) {

            JObject[] payload = MqttPub_Var_Util.GetChunckByLimit(transformedValues, config.MaxPayloadSize - 100);

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



}
