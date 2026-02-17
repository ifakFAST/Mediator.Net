// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Publish;

[XmlRoot(Namespace = "Module_Publish", ElementName = "Publish_Model")]
public class Model : ModelObject {

    [XmlAttribute("id")]
    public string ID { get; set; } = "Root";

    [XmlAttribute("name")]
    public string Name { get; set; } = "Publish_Model";

    public List<MqttConfig>  MQTT { get; set; } = [];
    public List<SQLConfig>   SQL  { get; set; } = [];
    public List<OpcUaConfig> OPC_UA { get; set; } = [];

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
        foreach (var ua in OPC_UA) {
            ua.ApplyVarConfig(vars);
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

    public string User { get; set; } = "";
    public string Pass { get; set; } = "";

    public bool NoCertificateValidation { get; set; } = false;
    public bool IgnoreCertificateRevocationErrors { get; set; } = false;
    public bool IgnoreCertificateChainErrors { get; set; } = false;
    public bool AllowUntrustedCertificates { get; set; } = false;

    public int MaxPayloadSize { get; set; } = 128 * 1024;

    public string TopicRoot { get; set; } = "";

    public MqttVarPub VarPublish { get; set; } = new MqttVarPub() { Enabled = false };

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            Endpoint = Endpoint.Replace(entry.Key, entry.Value);
            ClientIDPrefix = ClientIDPrefix.Replace(entry.Key, entry.Value);
            CertFileCA = CertFileCA.Replace(entry.Key, entry.Value);
            CertFileClient = CertFileClient.Replace(entry.Key, entry.Value);
            TopicRoot = TopicRoot.Replace(entry.Key, entry.Value);
        }
        VarPublish?.ApplyVarConfig(vars);
    }
}

public enum NaNHandling {
    Keep,
    ConvertToNull,
    ConvertToString,
    Remove
}

public enum PubMode {
    Cyclic, // IF PublishInterval = 0 THEN: publish on variable value update with additional 1 min cycle ELSE: Cyclic publish based on PublishInterval & PublishOffset
    OnVarValueUpdate,  // publish on variable value update   (+ additional cycle publish if PublishInterval > 0)
    OnVarHistoryUpdate // publish on variable history update (+ additional cycle publish if PublishInterval > 0)
}

public interface VarPubCommon {
    List<ObjectRef> RootObjects { get; set; }
    bool SimpleTagsOnly { get; set; }
    bool NumericTagsOnly { get; set; }
    bool SendTagsWithNull { get; set; }
    NaNHandling NaN_Handling { get; set; }
    Duration PublishInterval { get; set; }
    Duration PublishOffset { get; set; }
    PubMode PublishMode { get; set; }
}

public class MqttVarPub : VarPubCommon {
    
    public bool Enabled { get; set; } = true;

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

    public TopicMode Mode { get; set; } = TopicMode.Bulk;
    public string TopicTemplate { get; set; } = "{ID}";

    public bool BufferIfOffline { get; set; } = true;

    public bool SimpleTagsOnly { get; set; } = true;
    public bool NumericTagsOnly { get; set; } = false;
    public bool SendTagsWithNull { get; set; } = false;
    public NaNHandling NaN_Handling { get; set; } = NaNHandling.Keep;

    public bool TimeAsUnixMilliseconds { get; set; } = false;
    public bool QualityNumeric { get; set; } = false;

    public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
    public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);
    public PubMode PublishMode { get; set; } = PubMode.Cyclic;

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            Topic = Topic.Replace(entry.Key, entry.Value);
            TopicRegistration = TopicRegistration.Replace(entry.Key, entry.Value);
            TopicTemplate = TopicTemplate.Replace(entry.Key, entry.Value);
        }

        if (RootObjects.Count == 0 && ModuleID != "" && RootObject != "") {
            RootObjects.Add(ObjectRef.Make(ModuleID, RootObject));
            ModuleID = "";
            RootObject = "";
        }
    }
}

public enum PubVarFormat {
    Array,
    Object
}

public enum TopicMode {
    Bulk,            // All variables in one topic
    TopicPerVariable // One topic per variable
}

public class SQLConfig : ModelObject {

    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    public Database DatabaseType { get; set; } = Database.PostgreSQL;

    public string ConnectionString { get; set; } = "";

    public SQLVarPub VarPublish { get; set; } = new SQLVarPub() { Enabled = false };

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            ConnectionString = ConnectionString.Replace(entry.Key, entry.Value);
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

public class SQLVarPub : VarPubCommon {

    public bool Enabled { get; set; } = true;

    public string QueryTagID2Identifier { get; set; } = "";
    public string QueryRegisterTag { get; set; } = "";
    public string QueryPublish { get; set; } = "";

    [XmlArrayItem("RootObject")]
    public List<ObjectRef> RootObjects { get; set; } = new List<ObjectRef>();

    public bool BufferIfOffline { get; set; } = false;

    public bool LogWrites { get; set; } = false;

    public bool SimpleTagsOnly { get; set; } = true;
    public bool NumericTagsOnly { get; set; } = false;
    public bool SendTagsWithNull { get; set; } = false;
    public NaNHandling NaN_Handling { get; set; } = NaNHandling.Keep;

    public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
    public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);
    public PubMode PublishMode { get; set; } = PubMode.Cyclic;

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

    public OpcUaVarPub VarPublish { get; set; } = new OpcUaVarPub() { Enabled = false };

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
            LoginUser = LoginUser.Replace(entry.Key, entry.Value);
            LoginPass = LoginPass.Replace(entry.Key, entry.Value);
            ServerCertificateFile = ServerCertificateFile.Replace(entry.Key, entry.Value);
        }
        VarPublish.ApplyVarConfig(vars);
    }
}

public class OpcUaVarPub : VarPubCommon {

    public bool Enabled { get; set; } = true;

    [XmlArrayItem("RootObject")]
    public List<ObjectRef> RootObjects { get; set; } = [];

    public bool AllowClientWrites { get; set; } = true;

    public bool LocalObjectIDsForVariables { get; set; } = false;

    public bool SimpleTagsOnly { get; set; } = true;
    public bool NumericTagsOnly { get; set; } = false;
    public bool SendTagsWithNull { get; set; } = false;
    public NaNHandling NaN_Handling { get; set; } = NaNHandling.Keep;

    public Duration PublishInterval { get; set; } = Duration.FromSeconds(5);
    public Duration PublishOffset { get; set; } = Duration.FromSeconds(0);
    public PubMode PublishMode { get; set; } = PubMode.Cyclic;

    public void ApplyVarConfig(Dictionary<string, string> vars) {
        foreach (var entry in vars) {
        }
    }
}
