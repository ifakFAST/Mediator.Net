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
    private string certDir = ".";
    private Func<bool> moduleShutdown = () => true;
    private bool stopForRestart;
    private readonly List<Task> runningTasks = [];

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

    protected override async Task OnConfigModelChanged(bool init) {

        await base.OnConfigModelChanged(init);

        if (init) return;

        await StopAllTasks();
        StartAllTasks();
    }

    public override async Task Run(Func<bool> shutdown) {

        certDir = info.GetConfigReader().GetOptionalString("cert-dir", ".");
        moduleShutdown = shutdown;

        StartAllTasks();

        _ = StartCheckForModelFileModificationTask(shutdown);

        while (!shutdown()) {
            await Task.Delay(100);
        }

        await StopAllTasks();
    }

    private void StartAllTasks() {

        stopForRestart = false;
        bool Stop() => stopForRestart || moduleShutdown();

        foreach (SQLConfig sql in model.SQL) {
            if (sql.VarPublish == null || !sql.VarPublish.Enabled) continue;
            runningTasks.Add(SQL.VarPubTask.MakeVarPubTask(sql, info, Stop));
        }

        foreach (OpcUaConfig ua in model.OPC_UA) {
            if (ua.VarPublish == null || !ua.VarPublish.Enabled) continue;
            runningTasks.Add(OPC_UA.VarPubTask.MakeVarPubTask(ua, info, Stop));
        }

        foreach (MqttConfig mqtt in model.MQTT) {
            if (mqtt.VarPublish == null || !mqtt.VarPublish.Enabled) continue;
            runningTasks.Add(MqttPublisher.MakeVarPubTask(mqtt, info, certDir, Stop));
        }
    }

    private async Task StopAllTasks() {

        stopForRestart = true;

        if (runningTasks.Count > 0) {
            try {
                await Task.WhenAll(runningTasks);
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                Console.Error.WriteLine($"StopAllTasks: {e.GetType().FullName} {e.Message}");
            }
        }

        runningTasks.Clear();
    }
}
