using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using System.IO;

namespace Ifak.Fast.Mediator.Publish
{
    public class Module : ModelObjectModule<Model>
    {

        private ModuleInitInfo info;

        public override async Task Init(ModuleInitInfo info,
                                        VariableValue[] restoreVariableValues,
                                        Notifier notifier,
                                        ModuleThread moduleThread) {

            this.info = info;

            await base.Init(info, restoreVariableValues, notifier, moduleThread);
        }

        public override async Task Run(Func<bool> shutdown) {

            var mapConfigVar = new Dictionary<string, string>();

            string configVarFile = info.GetConfigReader().GetOptionalString("config-var-file", "").Trim();
            if (configVarFile != "") {
                if (!File.Exists(configVarFile)) {
                    Console.WriteLine($"config-var-file '{configVarFile}' not found!");
                }
                else {
                    string vars = File.ReadAllText(configVarFile, System.Text.Encoding.UTF8);
                    var variables = StdJson.JObjectFromString(vars);
                    Console.WriteLine($"Using variables as specified in config-var-file '{configVarFile}':");
                    foreach (var prop in variables.Properties()) {
                        string key = "${" + prop.Name + "}";
                        mapConfigVar[key] = prop.Value.ToString();
                        Console.WriteLine($"{prop.Name} -> {prop.Value}");
                    }
                }
            }

            model.ApplyVarConfig(mapConfigVar);

            string certDir = info.GetConfigReader().GetOptionalString("cert-dir", ".");
            var tasks = new List<Task>();

            Task[] tasksVarPub = model.MQTT
                .Where(mqtt => mqtt.VarPublish != null)
                .Select(mqtt => MqttPublisher.MakeVarPubTask(mqtt, info, certDir, shutdown))
                .ToArray();

            tasks.AddRange(tasksVarPub);

            Task[] tasksConfigPub = model.MQTT
                .Where(mqtt => mqtt.ConfigPublish != null)
                .Select(mqtt => MqttPublisher.MakeConfigPubTask(mqtt, info, certDir, shutdown))
                .ToArray();

            tasks.AddRange(tasksConfigPub);

            Task[] tasksVarRec = model.MQTT
               .Where(mqtt => mqtt.VarReceive != null)
               .Select(mqtt => MqttPublisher.MakeVarRecTask(mqtt, info, certDir, shutdown))
               .ToArray();

            tasks.AddRange(tasksVarRec);

            Task[] tasksConfigRec = model.MQTT
               .Where(mqtt => mqtt.ConfigReceive != null)
               .Select(mqtt => MqttPublisher.MakeConfigRecTask(mqtt, info, certDir, shutdown))
               .ToArray();

            tasks.AddRange(tasksConfigRec);

            Task[] tasksMethodPub = model.MQTT
              .Where(mqtt => mqtt.MethodPublish != null)
              .Select(mqtt => MqttPublisher.MakeMethodPubTask(mqtt, info, certDir, shutdown))
              .ToArray();

            tasks.AddRange(tasksMethodPub);

            if (tasks.Count == 0) {

                while (!shutdown()) {
                    await Task.Delay(100);
                }
            }
            else {

                try {
                    await Task.WhenAll(tasks);
                }
                catch (Exception exp) {
                    if (!shutdown()) {
                        Exception e = exp.GetBaseException() ?? exp;
                        Console.Error.WriteLine($"Run: {e.GetType().FullName} {e.Message}");
                        return;
                    }
                }
            }
        }
    }

    [XmlRoot(Namespace = "Module_Publish", ElementName = "Publish_Model")]
    public class Model : ModelObject
    {
        [XmlIgnore]
        public string ID { get; set; } = "Root";

        [XmlIgnore]
        public string Name { get; set; } = "Publish_Model";

        public List<MqttConfig> MQTT { get; set; } = new List<MqttConfig>();

        public bool ShouldSerializeMQTT() { return MQTT.Count > 0; }

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var mm in MQTT) {
                mm.ApplyVarConfig(vars);
            }
        }
    }

    public class MqttConfig : ModelObject
    {
        [XmlAttribute]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute]
        public string Name { get; set; } = "";

        public string Endpoint { get; set; } = "";
        public string ClientIDPrefix { get; set; } = "";
        public string CertFileCA { get; set; } = "";
        public string CertFileClient { get; set; } = "";

        public bool IgnoreCertificateRevocationErrors { get; set; } = false;
        public bool IgnoreCertificateChainErrors { get; set; } = false;
        public bool AllowUntrustedCertificates { get; set; } = false;

        public int MaxPayloadSize { get; set; } = 128 * 1024;

        public string TopicRoot { get; set; } = "";

        public MqttVarPub?        VarPublish { get; set; } = null;
        public MqttVarReceive?    VarReceive { get; set; } = null;
        public MqttConfigPub?     ConfigPublish { get; set; } = null;
        public MqttConfigReceive? ConfigReceive { get; set; } = null;
        public MqttMethodPub?     MethodPublish { get; set; } = null;

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var entry in vars) {
                Endpoint = Endpoint.Replace(entry.Key, entry.Value);
                ClientIDPrefix = ClientIDPrefix.Replace(entry.Key, entry.Value);
                CertFileCA = CertFileCA.Replace(entry.Key, entry.Value);
                CertFileClient = CertFileClient.Replace(entry.Key, entry.Value);
                TopicRoot = TopicRoot.Replace(entry.Key, entry.Value);
            }
            VarPublish?.ApplyVarConfig(vars);
            VarReceive?.ApplyVarConfig(vars);
            ConfigPublish?.ApplyVarConfig(vars);
            ConfigReceive?.ApplyVarConfig(vars);
            MethodPublish?.ApplyVarConfig(vars);
        }
    }


    public class MqttVarPub : ModelObject
    {
        [XmlAttribute]
        public string Name { get; set; } = "VarPub";

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var mqttConfig = (MqttConfig)parents.First();
            return mqttConfig.ID + ".VarPub";
        }

        public string Topic { get; set; } = "";
        public string TopicRegistration { get; set; } = "";
        public int PayloadLimit { get; set; } = 500;
        public bool PrintPayload { get; set; } = true;

        public string ModuleID { get; set; } = "IO";
        public string RootObject { get; set; } = "Root";

        public bool NumericTagsOnly { get; set; } = true;
        public bool SendTagsWithNull { get; set; } = false;

        public bool TimeAsUnixMilliseconds { get; set; } = false;
        public bool QualityNumeric { get; set; } = false;

        public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
        public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var entry in vars) {
                Topic = Topic.Replace(entry.Key, entry.Value);
                TopicRegistration = TopicRegistration.Replace(entry.Key, entry.Value);
            }
        }
    }

    public class MqttConfigPub : ModelObject
    {
        [XmlAttribute]
        public string Name { get; set; } = "ConfigPub";

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var mqttConfig = (MqttConfig)parents.First();
            return mqttConfig.ID + ".ConfigPub";
        }

        public string Topic { get; set; } = "config/reported";

        public string ModuleID { get; set; } = "IO";
        public bool PrintPayload { get; set; } = true;

        public Duration PublishInterval { get; set; } = Duration.FromMinutes(5);
        public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var entry in vars) {
                Topic = Topic.Replace(entry.Key, entry.Value);
            }
        }
    }

    public class MqttVarReceive : ModelObject
    {
        [XmlAttribute]
        public string Name { get; set; } = "VarReceive";

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var mqttConfig = (MqttConfig)parents.First();
            return mqttConfig.ID + ".VarReceive";
        }

        public string Topic { get; set; } = "";

        public string ModuleID { get; set; } = "IO";

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var entry in vars) {
                Topic = Topic.Replace(entry.Key, entry.Value);
            }
        }
    }

    public class MqttConfigReceive : ModelObject
    {
        [XmlAttribute]
        public string Name { get; set; } = "ConfigReceive";

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var mqttConfig = (MqttConfig)parents.First();
            return mqttConfig.ID + ".ConfigReceive";
        }

        public string Topic { get; set; } = "";

        public string ModuleID { get; set; } = "IO";

        public int MaxBuckets { get; set; } = 100;

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var entry in vars) {
                Topic = Topic.Replace(entry.Key, entry.Value);
            }
        }
    }

    public class MqttMethodPub : ModelObject
    {
        [XmlAttribute]
        public string Name { get; set; } = "MethodPub";

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var mqttConfig = (MqttConfig)parents.First();
            return mqttConfig.ID + ".MethodPub";
        }

        public string Topic { get; set; } = "method/reported";

        public string ModuleID { get; set; } = "IO";
        public string MethodName { get; set; } = "BrowseAllAdapterDataItems";
        public bool PrintPayload { get; set; } = true;

        public Duration PublishInterval { get; set; } = Duration.FromHours(1);
        public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);

        public void ApplyVarConfig(Dictionary<string, string> vars) {
            foreach (var entry in vars) {
                Topic = Topic.Replace(entry.Key, entry.Value);
            }
        }
    }
}
