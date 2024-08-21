// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator.EventLog
{
    [XmlRoot(Namespace = "Module_EventLog", ElementName = "EventLog_Model")]
    public class EventLogConfig : ModelObject
    {
        [XmlIgnore]
        public string ID { get; set; } = "Root";

        [XmlIgnore]
        public string Name { get; set; } = "EventLog_Model";

        public string TextForNoActiveWarningsAndAlarms { get; set; } = "";

        public VariableRef? CountActiveWarningsAndAlarms { get; set; }
        public VariableRef? ActiveWarningsAndAlarms { get; set; }
        public bool ShouldSerializeCountActiveWarningsAndAlarms() => CountActiveWarningsAndAlarms.HasValue;
        public bool ShouldSerializeActiveWarningsAndAlarms() => ActiveWarningsAndAlarms.HasValue;

        public MailNotificationSettings MailNotificationSettings { get; set; } = new MailNotificationSettings();

        protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

            var variableLastEvent = new Variable(
                name: "LastEvent",
                type: DataType.Struct,
                dimension: 1,
                defaultValue: DataValue.FromJSON("{}"),
                remember: false,
                history: History.None
            );

            //var variableWarnAlarmCount = new Variable(
            //    name: "CountActiveWarningsAndAlarms",
            //    type: DataType.UInt32,
            //    dimension: 1,
            //    defaultValue: DataValue.FromInt(0),
            //    remember: false,
            //    history: History.None
            //);

            return new Variable[] {
               variableLastEvent,
               //variableWarnAlarmCount
            };
        }
    }

    public class MailNotificationSettings
    {
        public SmtpSettings SmtpSettings { get; set; } = new SmtpSettings();
        public List<MailNotification> Notifications { get; set; } = new List<MailNotification>();
    }

    public class SmtpSettings
    {
        [XmlAttribute("server")]
        public string Server { get; set; } = "";

        [XmlAttribute("port")]
        public int Port { get; set; } = 25;

        [XmlAttribute("user")]
        public string AuthUser { get; set; } = "";

        [XmlAttribute("pass")]
        public string AuthPass { get; set; } = "";

        [XmlAttribute("ssl")]
        public SslOptions SslOptions { get; set; } = SslOptions.Auto;

        [XmlAttribute("from")]
        public string From { get; set; } = "";
    }

    public class MailNotification
    {
        [XmlAttribute("enabled")]
        public bool Enabled { get; set; } = false;

        [XmlAttribute("sources")]
        public string Sources { get; set; } = "*";

        [XmlAttribute("to")]
        public string To { get; set; } = "";

        [XmlAttribute("subject")]
        public string Subject { get; set; } = "";
    }

    public enum SslOptions
    {
        None = 0,
        Auto = 1,
        SslOnConnect = 2,
        StartTls = 3,
        StartTlsWhenAvailable = 4
    }
}
