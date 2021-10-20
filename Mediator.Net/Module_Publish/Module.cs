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

            string certDir = info.GetConfigReader().GetOptionalString("cert-dir", ".");
            var tasks = new List<Task>();

            foreach (var mqt in model.MQTT) {
                string file = mqt.TopicRootFile.Trim();
                if (file != "") {
                    if (!File.Exists(file)) {
                        Console.WriteLine($"TopicRootFile '{file}' not found!");
                    }
                    else {
                        string topic = File.ReadAllText(file, System.Text.Encoding.UTF8).Trim();
                        mqt.TopicRoot = topic;
                        Console.WriteLine($"Using root topic '{topic}' as specified in TopicRootFile '{file}'");
                    }
                }
            }

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
        public string TopicRootFile { get; set; } = ""; // optional file name containing the topic root to be used instead of TopicRoot

        public MqttVarPub?        VarPublish { get; set; } = null;
        public MqttVarReceive?    VarReceive { get; set; } = null;
        public MqttConfigPub?     ConfigPublish { get; set; } = null;
        public MqttConfigReceive? ConfigReceive { get; set; } = null;
        public MqttMethodPub?     MethodPublish { get; set; } = null;
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
        public int PayloadLimit { get; set; } = 500;
        public bool PrintPayload { get; set; } = true;

        public string ModuleID { get; set; } = "IO";
        public string RootObject { get; set; } = "Root";

        public bool TimeAsUnixMilliseconds { get; set; } = false;
        public bool QualityNumeric { get; set; } = false;

        public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
        public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);
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
    }
}
