// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator;

public class Configuration
{
    public string ClientListenHost { get; set; } = "localhost";
    public int ClientListenPort { get; set; } = 8080;
    public TimestampWarnMode TimestampCheckWarning { get; set; } = TimestampWarnMode.Always;
    public List<Module> Modules { get; set; } = [];
    public UserManagement UserManagement { get; set; } = new();
    public List<Location> Locations { get; set; } = [];

    public void Normalize(string configFileName, NLog.Logger logger) {
        foreach (var m in Modules) {
            m.Normalize(configFileName, logger);
        }
    }
}

public enum TimestampWarnMode
{
    Always,
    OnlyWhenHistory,
    Never
}

public class Module
{
    public const string ExternalModule = "Ifak.Fast.Mediator.ExternalModule";

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
    public string ImplClass { get; set; } = ExternalModule;

    public string ExternalCommand { get; set; } = "";
    public string ExternalArgs { get; set; } = "";

    public List<NamedValue> Config { get; set; } = [];

    public List<HistoryDB> HistoryDBs { get; set; } = [];

    public void Normalize(string configFileName, NLog.Logger logger) {

        ExternalCommand = ExternalCommand.Trim();
        ExternalArgs = ExternalArgs.Trim();

        string extCmdOrig = ExternalCommand;
        string extArgsOrig = ExternalArgs;

        if (ExternalCommand == "dotnet") {

            string pattern = @"^\.\/Bin\/Module_(\w+)\/Module_\1\.dll\s+\{PORT\}$";
            Match match = Regex.Match(ExternalArgs, pattern);

            if (match.Success) {
                string moduleName = match.Groups[1].Value;
                ExternalArgs = $"./Bin/Mediator/Module_{moduleName}.dll" + " {PORT}";
            }
        }

        if (Program.IsSelfContained && ExternalCommand == "dotnet") {

            string pattern = @"^\.\/Bin\/Mediator\/Module_(\w+)\.dll\s+\{PORT\}$";
            Match match = Regex.Match(ExternalArgs, pattern);

            if (match.Success) {
                string moduleName = match.Groups[1].Value;
                ExternalCommand = $"./Bin/Mediator/Module_{moduleName}";
                ExternalArgs = "{PORT}";
            }
        }

        bool externalCmdChanged  = extCmdOrig  != ExternalCommand;
        bool externalArgsChanged = extArgsOrig != ExternalArgs;

        if (externalCmdChanged || externalArgsChanged) {
            logger.Info($"In file {configFileName}:");
            if (externalCmdChanged) {
                logger.Info($"- Changed ExternalCommand of Module {Name}:");
                logger.Info($"  * From: {extCmdOrig}");
                logger.Info($"  *   To: {ExternalCommand}");
            }
            if (externalArgsChanged) {
                logger.Info($"- Changed ExternalArgs of Module {Name}:");
                logger.Info($"  * From: {extArgsOrig}");
                logger.Info($"  *   To: {ExternalArgs}");
            }
            logger.Info($"Consider updating the corresponding entries in file {configFileName}.");
        }

        for (int i = 0; i < Config.Count; i++) {
            NamedValue nv = Config[i];
            if (nv.Name == "view-assemblies") {
                string v = nv.Value;
                v = v.Replace("./Bin/Module_EventLog/Module_EventLog.dll", "./Bin/Mediator/Module_EventLog.dll");
                v = v.Replace("./Bin/Module_Calc/Module_Calc.dll", "./Bin/Mediator/Module_Calc.dll");
                if (v != nv.Value) {
                    Config[i] = new NamedValue(nv.Name, v);
                    logger.Info($"- Changed <NamedValue name=\"view-assemblies\"> of Module {Name}:");
                    logger.Info($"  * From: {nv.Value}");
                    logger.Info($"  *   To: {v}");
                }
            }
        }
    }
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

    [XmlAttribute("allowOutOfOrderAppend")]
    public bool AllowOutOfOrderAppend { get; set; } = false;

    [XmlAttribute("retentionTime")]
    public string RetentionTime { get; set; } = "";

    [XmlAttribute("retentionCheckInterval")]
    public string RetentionCheckInterval { get; set; } = "1 h";

    [XmlAttribute("maxConcurrentReads")]
    public int MaxConcurrentReads { get; set; } = 0;

    public string AggregationCache { get; set; } = "";

    public ArchiveSettings Archive { get; set; } = new ArchiveSettings();

    public string[] Variables { get; set; } = [];

    public string[] Settings { get; set; } = [];
}

public class ArchiveSettings
{
    [XmlAttribute("path")]
    public string Path { get; set; } = "";

    [XmlAttribute("olderThanDays")]
    public int OlderThanDays { get; set; } = 30;

    [XmlAttribute("checkEveryHours")]
    public int CheckEveryHours { get; set; } = 24;
}

public class UserManagement
{
    public List<User> Users { get; set; } = [];
    public List<Role> Roles { get; set; } = [];
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

    public List<NamedValue> Config { get; set; } = [];

    public bool ShouldSerializeConfig() => Config.Count > 0;
}
