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

    public static async Task MakeVarPubTask(BufferedVarPub publisher, VarPubCommon varPub, ModuleInitInfo info, Func<bool> shutdown) {

        ObjectRefs rootObjects = varPub.RootObjects;

        bool UseCyclicPublishing() {
            if (varPub.PublishMode == PubMode.Cyclic) return true;
            if (varPub.PublishMode == PubMode.OnVarValueUpdate && varPub.PublishInterval > Duration.Zero) return true;
            return false;
        }

        (Duration cycle, Duration offset) GetCycleAndOffset() {
            if (varPub.PublishMode == PubMode.Cyclic && varPub.PublishInterval > Duration.Zero) return (varPub.PublishInterval, varPub.PublishOffset);
            if (varPub.PublishMode == PubMode.OnVarValueUpdate && varPub.PublishInterval > Duration.Zero) return (varPub.PublishInterval, varPub.PublishOffset);
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

        while (!shutdown()) {
            Connection clientFAST = await wrapper.EnsureConnectionOrThrow();
            if (cyclicPublishing) {
                VariableValues allValues = await clientFAST.ReadAllVariablesOfObjectTrees(rootObjects);
                await PublishRelevantVariableValues(clientFAST, allValues);
            }
            await WaitForNextCycle();
        }

        await wrapper.Close();
        publisher.Close();
        await varMetaMan.Close();
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
