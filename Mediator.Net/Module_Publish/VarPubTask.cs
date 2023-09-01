// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using ObjectRefs = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectRef>;

namespace Ifak.Fast.Mediator.Publish;

internal class VarPubTask {

    public static async Task MakeVarPubTask(BufferedVarPub publisher, VarPubCommon varPub, ModuleInitInfo info, Func<bool> shutdown) {

        ObjectRefs rootObjects = varPub.RootObjects;

        Duration cycle = varPub.PublishInterval;
        Duration offset = varPub.PublishOffset;
        bool enableChangeBasedVariableUpdates = cycle == Duration.Zero;

        async Task WaitForNextCycle() {
            Timestamp t = Time.GetNextNormalizedTimestamp(cycle, offset);
            await Time.WaitUntil(t, abort: shutdown);
        }

        if (enableChangeBasedVariableUpdates) {
            cycle = Duration.FromMinutes(1);
            offset = Duration.Zero;
        }
        else {
            await WaitForNextCycle();
        }

        VarMetaManager varMetaMan = new VarMetaManager();

        async Task OnConfigurationChanged(Connection client, ObjectRefs changedObjects) {
            await varMetaMan.OnConfigChanged(client);
        }

        async Task PublishRelevantVariableValues(Connection client, VariableValues allValues) {
            VariableValues values = Filter(allValues, varPub);
            await varMetaMan.Check(values, client);
            publisher.UpdateVarInfos(varMetaMan.variables2Info);
            publisher.Post(values);
        }

        Task OnVariablesChangedEvent(Connection client, VariableValues allValues) {
            return PublishRelevantVariableValues(client, allValues);
        }

        async Task RegisterForChangeEvents(Connection client) {
            if (enableChangeBasedVariableUpdates) {
                await client.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), rootObjects.ToArray());
            }
            await client.EnableConfigChangedEvents(rootObjects.ToArray());
        }

        var wrapper = new ConnectionWrapper(info) {
            OnConnectionCreated = RegisterForChangeEvents,
            OnConfigurationChanged = OnConfigurationChanged,
            OnVariableValueChanged = OnVariablesChangedEvent
        };

        while (!shutdown()) {
            Connection clientFAST = await wrapper.EnsureConnectionOrThrow();
            VariableValues allValues = await clientFAST.ReadAllVariablesOfObjectTrees(rootObjects);
            await PublishRelevantVariableValues(clientFAST, allValues);
            await WaitForNextCycle();
        }

        await wrapper.Close();
        publisher.Close();
    }

    private static VariableValues Filter(VariableValues values, VarPubCommon config) {
        var criteria = new Util.FilterCriteria(
            SimpleTagsOnly: config.SimpleTagsOnly,
            NumericTagsOnly: config.NumericTagsOnly,
            SendTagsWithNull: config.SendTagsWithNull,
            RemoveEmptyTimestamp: true);
        return Util.Filter(values, criteria);
    }
}
