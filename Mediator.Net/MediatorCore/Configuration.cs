// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator
{
    public class Configuration
    {
        public string ClientListenHost { get; set; } = "localhost";
        public int ClientListenPort { get; set; } = 8080;
        public TimestampWarnMode TimestampCheckWarning { get; set; } = TimestampWarnMode.Always;
        public List<Module> Modules { get; set; } = new List<Module>();
        public UserManagement UserManagement { get; set; } = new UserManagement();
        public List<Location> Locations { get; set; } = new List<Location>();
    }

    public enum TimestampWarnMode
    {
        Always,
        OnlyWhenHistory,
        Never
    }

    public class Module
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        [XmlAttribute("enabled")]
        public bool Enabled { get; set; } = true;

        [XmlAttribute("concurrentInit")]
        public bool ConcurrentInit { get; set; } = false;

        public string VariablesFileName { get; set; } = "";
        public string ImplAssembly { get; set; } = "";
        public string ImplClass { get; set; } = "Ifak.Fast.Mediator.ExternalModule";

        public string ExternalCommand { get; set; } = "";
        public string ExternalArgs { get; set; } = "";

        public List<NamedValue> Config { get; set; } = new List<NamedValue>();

        public List<HistoryDB> HistoryDBs { get; set; } = new List<HistoryDB>();
    }

    public class HistoryDB
    {
        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        public string ConnectionString { get; set; } = "";

        [XmlAttribute("type")]
        public string Type { get; set; } = "SQLite";

        [XmlAttribute("prioritizeReadRequests")]
        public bool PrioritizeReadRequests { get; set; } = true;

        public string[] Variables { get; set; } = new string[0];

        public string[] Settings { get; set; } = new string[0];
    }

    public class UserManagement
    {
        public List<User> Users { get; set; } = new List<User>();
        public List<Role> Roles { get; set; } = new List<Role>();
    }

    public class Location
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = "";

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        [XmlAttribute("longName")]
        public string LongName { get; set; } = "";

        [XmlAttribute("parent")]
        public string Parent { get; set; } = "";

        public List<NamedValue> Config { get; set; } = new List<NamedValue>();

        public bool ShouldSerializeConfig() => Config.Count > 0;
    }
}
