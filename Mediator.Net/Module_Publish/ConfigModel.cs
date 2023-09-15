// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Xml.Serialization;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish;

[XmlRoot(Namespace = "Module_Publish", ElementName = "Publish_Model")]
public class Model : ModelObject {

    [XmlAttribute("id")]
    public string ID { get; set; } = "Root";

    [XmlAttribute("name")]
    public string Name { get; set; } = "Publish_Model";

    public List<MqttConfig> MQTT { get; set; } = new List<MqttConfig>();
    public List<SQLConfig>  SQL  { get; set; } = new List<SQLConfig>();
    public List<OpcUaConfig> OPC_UA { get; set; } = new List<OpcUaConfig>();

    public bool ShouldSerializeMQTT() { return MQTT.Count > 0; }
    public bool ShouldSerializeSQL()  { return SQL.Count > 0; }
    public bool ShouldSerializeOPC_UA() { return OPC_UA.Count > 0; }

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var mm in MQTT) {
            mm.ApplyVarConfig(vars);
        }
        foreach (var sql in SQL) {
            sql.ApplyVarConfig(vars);
        }
    }
}

public class MqttConfig : ModelObject {
    
    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
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

    public MqttVarPub? VarPublish { get; set; } = null;
    public MqttVarReceive? VarReceive { get; set; } = null;
    public MqttConfigPub? ConfigPublish { get; set; } = null;
    public MqttConfigReceive? ConfigReceive { get; set; } = null;
    public MqttMethodPub? MethodPublish { get; set; } = null;

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


public interface VarPubCommon {
    List<ObjectRef> RootObjects { get; set; }
    bool BufferIfOffline { get; set; }
    bool SimpleTagsOnly { get; set; }
    bool NumericTagsOnly { get; set; }
    bool SendTagsWithNull { get; set; }
    Duration PublishInterval { get; set; }
    Duration PublishOffset { get; set; }
}

public class MqttVarPub : ModelObject, VarPubCommon {
    
    [XmlAttribute("name")]
    public string Name { get; set; } = "VarPub";

    protected override string GetID(IEnumerable<IModelObject> parents) {
        var mqttConfig = (MqttConfig)parents.First();
        return mqttConfig.ID + ".VarPub";
    }

    public string Topic { get; set; } = "";
    public string TopicRegistration { get; set; } = "";

    public bool PrintPayload { get; set; } = true;

    public string ModuleID { get; set; } = "";   // Deprecated, use RootObjects instead
    public string RootObject { get; set; } = ""; // Deprecated, use RootObjects instead

    public bool ShouldSerializeModuleID() => ModuleID != "";
    public bool ShouldSerializeRootObject() => RootObject != "";

    [XmlArrayItem("RootObject")]
    public List<ObjectRef> RootObjects { get; set; } = new List<ObjectRef>();

    public PubVarFormat PubFormat { get; set; } = PubVarFormat.Array;
    public PubVarFormat PubFormatReg { get; set; } = PubVarFormat.Array;

    public bool BufferIfOffline { get; set; } = false;

    public bool SimpleTagsOnly { get; set; } = true;
    public bool NumericTagsOnly { get; set; } = false;
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

public enum PubVarFormat {
    Array,
    Object
}

public class MqttConfigPub : ModelObject {
    
    [XmlAttribute("name")]
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

public class MqttVarReceive : ModelObject {
    
    [XmlAttribute("name")]
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

public class MqttConfigReceive : ModelObject {
    
    [XmlAttribute("name")]
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

public class MqttMethodPub : ModelObject {

    [XmlAttribute("name")]
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

public class SQLConfig : ModelObject {

    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    public Database DatabaseType { get; set; } = Database.PostgreSQL;

    public string ConnectionString { get; set; } = "";
    //public string CertFileCA { get; set; } = "";
    //public string CertFileClient { get; set; } = "";

    public bool IgnoreCertificateRevocationErrors { get; set; } = false;
    public bool IgnoreCertificateChainErrors { get; set; } = false;
    public bool AllowUntrustedCertificates { get; set; } = false;

    public SQLVarPub? VarPublish { get; set; } = null;

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            ConnectionString = ConnectionString.Replace(entry.Key, entry.Value);
            //CertFileCA = CertFileCA.Replace(entry.Key, entry.Value);
            //CertFileClient = CertFileClient.Replace(entry.Key, entry.Value);
        }
        VarPublish?.ApplyVarConfig(vars);
    }
}

public enum Database {
   // MSSQL,
   // MySQL,
    PostgreSQL,
   // SQLite
}

public class SQLVarPub : ModelObject, VarPubCommon {

    [XmlAttribute("name")]
    public string Name { get; set; } = "VarPub";

    protected override string GetID(IEnumerable<IModelObject> parents) {
        var sqlConfig = (SQLConfig)parents.First();
        return sqlConfig.ID + ".VarPub";
    }

    public string QueryTagID2Identifier { get; set; } = "";
    public string QueryRegisterTag { get; set; } = "";
    public string QueryPublish { get; set; } = "";

    [XmlArrayItem("RootObject")]
    public List<ObjectRef> RootObjects { get; set; } = new List<ObjectRef>();

    public bool BufferIfOffline { get; set; } = false;

    public bool SimpleTagsOnly { get; set; } = true;
    public bool NumericTagsOnly { get; set; } = false;
    public bool SendTagsWithNull { get; set; } = false;

    public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
    public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            QueryPublish = QueryPublish.Replace(entry.Key, entry.Value);
            QueryTagID2Identifier = QueryTagID2Identifier.Replace(entry.Key, entry.Value);
            QueryRegisterTag = QueryRegisterTag.Replace(entry.Key, entry.Value);
        }
    }
}


public class OpcUaConfig : ModelObject {

    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    public string Host { get; set; } = "";
    public ushort Port { get; set; } = 4840;
    public string LogLevel { get; set; } = "Info";
    public bool AllowAnonym { get; set; } = false;
    public string LoginUser { get; set; } = "";
    public string LoginPass { get; set; } = "";

    public string ServerCertificateFile { get; set; } = "";

    public OpcUaVarPub? VarPublish { get; set; } = null;

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            LoginUser = LoginUser.Replace(entry.Key, entry.Value);
            LoginPass = LoginPass.Replace(entry.Key, entry.Value);
            ServerCertificateFile = ServerCertificateFile.Replace(entry.Key, entry.Value);
        }
        VarPublish?.ApplyVarConfig(vars);
    }
}

public class OpcUaVarPub : ModelObject, VarPubCommon {

    [XmlAttribute("name")]
    public string Name { get; set; } = "VarPub";

    protected override string GetID(IEnumerable<IModelObject> parents) {
        var uaConfig = (OpcUaConfig)parents.First();
        return uaConfig.ID + ".VarPub";
    }

    [XmlArrayItem("RootObject")]
    public List<ObjectRef> RootObjects { get; set; } = new List<ObjectRef>();

    public bool BufferIfOffline { get; set; } = false;

    public bool SimpleTagsOnly { get; set; } = true;
    public bool NumericTagsOnly { get; set; } = false;
    public bool SendTagsWithNull { get; set; } = false;

    public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
    public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
        }
    }
}