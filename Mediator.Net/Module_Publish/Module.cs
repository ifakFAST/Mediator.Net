// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using System.IO;
using Ifak.Fast.Mediator.Publish.MQTT;

namespace Ifak.Fast.Mediator.Publish;

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

    protected override void ModifyModelAfterInit() {

        string configVarFile = info.GetConfigReader().GetOptionalString("config-var-file", "").Trim();

        if (configVarFile != "") {

            if (!File.Exists(configVarFile)) {

                Console.Error.WriteLine($"config-var-file '{configVarFile}' not found!");
            }
            else {

                var mapConfigVar = new Dictionary<string, string>();

                string vars = File.ReadAllText(configVarFile, System.Text.Encoding.UTF8);
                var variables = StdJson.JObjectFromString(vars);
                var properties = variables.Properties().ToList();
                if (properties.Count > 0) {
                    Console.WriteLine($"Using variables as specified in config-var-file '{configVarFile}':");
                    foreach (var prop in properties) {
                        string key = "${" + prop.Name + "}";
                        mapConfigVar[key] = prop.Value.ToString();
                        Console.WriteLine($"{prop.Name} -> {prop.Value}");
                    }
                }

                model.ApplyVarConfig(mapConfigVar);
            }
        }
    }

    public override async Task Run(Func<bool> shutdown) {

        string certDir = info.GetConfigReader().GetOptionalString("cert-dir", ".");
        var tasks = new List<Task>();

        Task[] tasksVarPubSql = model.SQL
            .Where(sql => sql.VarPublish != null && sql.VarPublish.Enabled)
            .Select(sql => SQL.VarPubTask.MakeVarPubTask(sql, info, shutdown))
            .ToArray();

        tasks.AddRange(tasksVarPubSql);

        Task[] tasksVarPubUA = model.OPC_UA
            .Where(ua => ua.VarPublish != null && ua.VarPublish.Enabled)
            .Select(ua => OPC_UA.VarPubTask.MakeVarPubTask(ua, info, shutdown))
            .ToArray();

        tasks.AddRange(tasksVarPubUA);

        Task[] tasksVarPubMqtt = model.MQTT
            .Where(mqtt => mqtt.VarPublish != null && mqtt.VarPublish.Enabled)
            .Select(mqtt => MqttPublisher.MakeVarPubTask(mqtt, info, certDir, shutdown))
            .ToArray();

        tasks.AddRange(tasksVarPubMqtt);

        //Task[] tasksConfigPub = model.MQTT
        //    .Where(mqtt => mqtt.ConfigPublish != null)
        //    .Select(mqtt => MqttPublisher.MakeConfigPubTask(mqtt, info, certDir, shutdown))
        //    .ToArray();

        //tasks.AddRange(tasksConfigPub);

        //Task[] tasksVarRec = model.MQTT
        //   .Where(mqtt => mqtt.VarReceive != null)
        //   .Select(mqtt => MqttPublisher.MakeVarRecTask(mqtt, info, certDir, shutdown))
        //   .ToArray();

        //tasks.AddRange(tasksVarRec);

        //Task[] tasksConfigRec = model.MQTT
        //   .Where(mqtt => mqtt.ConfigReceive != null)
        //   .Select(mqtt => MqttPublisher.MakeConfigRecTask(mqtt, info, certDir, shutdown))
        //   .ToArray();

        //tasks.AddRange(tasksConfigRec);

        //Task[] tasksMethodPub = model.MQTT
        //  .Where(mqtt => mqtt.MethodPublish != null)
        //  .Select(mqtt => MqttPublisher.MakeMethodPubTask(mqtt, info, certDir, shutdown))
        //  .ToArray();

        //tasks.AddRange(tasksMethodPub);

        _ = StartCheckForModelFileModificationTask(shutdown);

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
