// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using ObjectRefs = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectRef>;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish;

internal class VarPubTask {

    private static readonly Duration RetryDelay = Duration.FromSeconds(10);

    public static async Task MakeVarPubTask(BufferedVarPub publisher, VarPubCommon varPub, ModuleInitInfo info, Func<bool> shutdown) {

        ObjectRefs rootObjects = varPub.RootObjects;

        bool UseCyclicPublishing() {
            if (varPub.PublishMode == PubMode.Cyclic) return true;
            if (varPub.PublishInterval > Duration.Zero) return true;
            return false;
        }

        (Duration cycle, Duration offset) GetCycleAndOffset() {
            if (varPub.PublishInterval > Duration.Zero) return (varPub.PublishInterval, varPub.PublishOffset);
            return (Duration.FromMinutes(1), Duration.Zero);
        }

        bool UseVarValueUpdateCyclicPublishing() {
            if (varPub.PublishMode == PubMode.Cyclic && varPub.PublishInterval == Duration.Zero) return true;
            if (varPub.PublishMode == PubMode.OnVarValueUpdate) return true;
            return false;
        }

        bool UseOnVarHistoryUpdateCyclicPublishing() {
            return varPub.PublishMode == PubMode.OnVarHistoryUpdate;
        }

        bool cyclicPublishing = UseCyclicPublishing();
        var (cycle, offset) = GetCycleAndOffset();

        bool enableChangeBasedVariableUpdates = UseVarValueUpdateCyclicPublishing();
        bool enableHistoryBasedVariableUpdates = UseOnVarHistoryUpdateCyclicPublishing();

        string strObjects = string.Join(", ", rootObjects.Select(r => r.ToString()));
        string cycleInfo = offset == Duration.Zero ? $"cycle: {cycle}" : $"cycle: {cycle}, offset: {offset}";
        string cyclicStr = cyclicPublishing ? $"CyclicPublish with {cycleInfo}" : "ChangeBasedOnly";
        string changeStr = enableChangeBasedVariableUpdates ? "VarValueUpdate" : (enableHistoryBasedVariableUpdates ? "VarHistoryUpdate" : "NoChangeBasedPublish");

        Console.WriteLine($"Starting variable publish task {publisher.PublisherID}: RootObjects={strObjects}; {cyclicStr}; {changeStr}");

        async Task WaitForNextCycle() {
            Timestamp t = Time.GetNextNormalizedTimestamp(cycle, offset);
            await Time.WaitUntil(t, abort: shutdown);
        }

        // Time at which the next regular cycle is due (a cycle is due at the first loop iteration).
        // Only advanced after a successful cyclic publish, so that a failed cycle stays due and is retried:
        Timestamp tNextCycleDue = Timestamp.Now;

        // After a failure, retry earlier than the next regular cycle (which may be far away):
        async Task WaitForNextCycleOrRetry(bool retry) {
            Timestamp tNow = Timestamp.Now;
            if (tNextCycleDue - tNow > cycle) { // system clock was set back
                tNextCycleDue = Time.GetNextNormalizedTimestamp(cycle, offset);
            }
            Timestamp t;
            if (retry) {
                // tNextCycleDue stays unchanged, so that a failed cycle is still published on retry:
                Timestamp tRetry = tNow + RetryDelay;
                Timestamp tCycle = tNextCycleDue > tNow ? tNextCycleDue : Time.GetNextNormalizedTimestamp(cycle, offset);
                t = tCycle < tRetry ? tCycle : tRetry;
            }
            else {
                if (tNextCycleDue <= tNow) { // no cyclic publishing, or missed cycle(s)
                    tNextCycleDue = Time.GetNextNormalizedTimestamp(cycle, offset);
                }
                t = tNextCycleDue;
            }
            await Time.WaitUntil(t, abort: shutdown);
        }

        if (!enableChangeBasedVariableUpdates && !enableHistoryBasedVariableUpdates) {
            await WaitForNextCycle();
        }

        VarMetaManager varMetaMan = new VarMetaManager();

        async Task OnConfigurationChanged(Connection client, ObjectRefs changedObjects) {
            await varMetaMan.OnConfigChanged(client);
            publisher.UpdateVarInfos(client, varMetaMan.Variables2Info);
            await publisher.OnConfigChanged();
        }

        async Task PublishRelevantVariableValues(Connection client, VariableValues allValues) {
            VariableValues values = Filter(allValues, varPub);
            await varMetaMan.Check(values, client);
            publisher.UpdateVarInfos(client, varMetaMan.Variables2Info);
            publisher.Post(values);
        }

        Task OnVariablesChangedEvent(Connection client, VariableValues allValues) {
            return PublishRelevantVariableValues(client, allValues);
        }

        async Task OnVariablesHistoryChangedEvent(Connection client, List<HistoryChange> changes) {
            VariableValues allValues = await ReadHistoricChanges(client, changes);
            await PublishRelevantVariableValues(client, allValues);
        }

        async Task RegisterForChangeEvents(Connection client) {
            if (enableChangeBasedVariableUpdates) {
                await client.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), rootObjects.ToArray());
            }
            if (enableHistoryBasedVariableUpdates) {
                await client.EnableVariableHistoryChangedEvents(rootObjects.ToArray());
            }
            await client.EnableConfigChangedEvents(rootObjects.ToArray());
        }

        var wrapper = new ConnectionWrapper(info) {
            OnConnectionCreated = RegisterForChangeEvents,
            OnConfigurationChanged = OnConfigurationChanged,
            OnVariableValueChanged = OnVariablesChangedEvent,
            OnVariableHistoryChanged = OnVariablesHistoryChangedEvent
        };

        try {
            while (!shutdown()) {
                bool failed = false;
                try {
                    Connection clientFAST = await wrapper.EnsureConnection();
                    if (shutdown()) {
                        break;
                    }
                    if (clientFAST.IsClosed) {
                        failed = true; // already logged by EnsureConnection
                    }
                    // Checked after EnsureConnection, which may take a while, so that a cycle
                    // that became due during a slow reconnect attempt is not skipped.
                    // A cycle that failed is published (late) on the next successful retry:
                    else if (cyclicPublishing && Timestamp.Now >= tNextCycleDue) {
                        VariableValues allValues = await clientFAST.ReadAllVariablesOfObjectTrees(rootObjects);
                        await PublishRelevantVariableValues(clientFAST, allValues);
                        tNextCycleDue = Time.GetNextNormalizedTimestamp(cycle, offset);
                    }
                }
                catch (Exception exp) {
                    if (shutdown()) {
                        break;
                    }
                    Exception e = exp.GetBaseException() ?? exp;
                    Console.Error.WriteLine($"Variable publish task {publisher.PublisherID}: {e.GetType().FullName} {e.Message}");
                    await CloseConnection(wrapper); // next EnsureConnection will reconnect
                    failed = true;
                }
                await WaitForNextCycleOrRetry(retry: failed);
            }
        }
        finally {
            Console.WriteLine($"Stopping variable publish task {publisher.PublisherID}");

            await CloseConnection(wrapper);
            publisher.Close();
            await varMetaMan.Close();
        }
    }

    private static async Task CloseConnection(ConnectionWrapper wrapper) {
        try {
            await wrapper.Close();
        }
        catch (Exception) { }
    }

    private static async Task<VariableValues> ReadHistoricChanges(Connection client, List<HistoryChange> changes) {
        VariableValues allValues = [];
        // Read historical values for each changed variable:
        foreach (HistoryChange change in changes) {
            try {
                var historyData = await client.HistorianReadRaw(
                    change.Variable,
                    change.ChangeStart,
                    change.ChangeEnd,
                    maxValues: 50000,
                    BoundingMethod.TakeFirstN,
                    QualityFilter.ExcludeNone);

                foreach (VTTQ vttq in historyData) {
                    allValues.Add(VariableValue.Make(change.Variable, vttq.ToVTQ()));
                }
            }
            catch (Exception ex) {
                Console.Error.WriteLine($"Error reading history for {change.Variable}: {ex.Message}");
            }
        }
        return allValues;
    }

    private static VariableValues Filter(VariableValues values, VarPubCommon config) {
        var criteria = new Util.FilterCriteria(
            SimpleTagsOnly: config.SimpleTagsOnly,
            NumericTagsOnly: config.NumericTagsOnly,
            SendTagsWithNull: config.SendTagsWithNull,
            NaN_Handling: config.NaN_Handling,
            RemoveEmptyTimestamp: false);
        return Util.Filter(values, criteria);
    }
}
