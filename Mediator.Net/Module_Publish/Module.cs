// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    private readonly List<PubTask> runningTasks = [];
    // Serializes stopping and (re)starting of the publish tasks, because overlapping
    // config changes would otherwise lose track of tasks started in between:
    private readonly SemaphoreSlim tasksLock = new(1, 1);

    private static readonly Duration TaskRestartDelay = Duration.FromSeconds(30);

    private sealed class PubTask(string name, Func<Task> start) {
        public string Name { get; } = name;
        // A synchronous exception of the initial start (e.g. invalid config) propagates to the caller:
        public Task Task { get; private set; } = start();
        public Timestamp? RestartTime { get; set; } = null;
        public void Restart() {
            Task = SafeStart(start);
            RestartTime = null;
        }
        // Converts a synchronous exception of a restart into a faulted task so that it stays supervised:
        private static Task SafeStart(Func<Task> start) {
            try {
                return start();
            }
            catch (Exception e) {
                return Task.FromException(e);
            }
        }
    }

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

        await tasksLock.WaitAsync();
        try {
            await StopAllTasks();
            if (!moduleShutdown()) {
                StartAllTasks();
            }
        }
        finally {
            tasksLock.Release();
        }
    }

    public override async Task Run(Func<bool> shutdown) {

        certDir = info.GetConfigReader().GetOptionalString("cert-dir", ".");
        moduleShutdown = shutdown;

        StartAllTasks();

        _ = StartCheckForModelFileModificationTask(shutdown);

        while (!shutdown()) {
            await Task.Delay(250);
            SuperviseTasks();
        }

        await tasksLock.WaitAsync();
        try {
            await StopAllTasks();
        }
        finally {
            tasksLock.Release();
        }
    }

    private void StartAllTasks() {

        stopForRestart = false;
        bool Stop() => stopForRestart || moduleShutdown();

        foreach (SQLConfig sql in model.SQL) {
            if (sql.VarPublish == null || !sql.VarPublish.Enabled) continue;
            runningTasks.Add(new PubTask($"SQL {sql.Name}", () => SQL.VarPubTask.MakeVarPubTask(sql, info, Stop)));
        }

        foreach (OpcUaConfig ua in model.OPC_UA) {
            if (ua.VarPublish == null || !ua.VarPublish.Enabled) continue;
            runningTasks.Add(new PubTask($"OPC UA {ua.Name}", () => OPC_UA.VarPubTask.MakeVarPubTask(ua, info, Stop)));
        }

        foreach (MqttConfig mqtt in model.MQTT) {
            if (mqtt.VarPublish == null || !mqtt.VarPublish.Enabled) continue;
            runningTasks.Add(new PubTask($"MQTT {mqtt.Name}", () => MqttPublisher.MakeVarPubTask(mqtt, info, certDir, Stop)));
        }
    }

    // Restarts publish tasks that terminated although no stop was requested
    // (e.g. because of an unhandled exception):
    private void SuperviseTasks() {

        if (stopForRestart || moduleShutdown()) return;

        Timestamp now = Timestamp.Now;

        foreach (PubTask pt in runningTasks) {

            if (!pt.Task.IsCompleted) continue;

            if (!pt.RestartTime.HasValue) {
                string reason = "Task completed";
                if (pt.Task.Exception is AggregateException exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    reason = $"{e.GetType().FullName} {e.Message}";
                }
                else if (pt.Task.IsCanceled) {
                    reason = "Task canceled";
                }
                Console.Error.WriteLine($"Publish task {pt.Name} terminated unexpectedly ({reason}). Restarting in {TaskRestartDelay}...");
                pt.RestartTime = now + TaskRestartDelay;
            }
            else if (now >= pt.RestartTime.Value) {
                Console.WriteLine($"Restarting publish task {pt.Name}");
                pt.Restart();
            }
        }
    }

    private async Task StopAllTasks() {

        stopForRestart = true;

        // Tasks waiting for restart have already terminated and their error has been logged:
        Task[] tasks = runningTasks.Where(pt => !pt.RestartTime.HasValue).Select(pt => pt.Task).ToArray();

        if (tasks.Length > 0) {
            try {
                await Task.WhenAll(tasks);
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                Console.Error.WriteLine($"StopAllTasks: {e.GetType().FullName} {e.Message}");
            }
        }

        runningTasks.Clear();
    }
}
