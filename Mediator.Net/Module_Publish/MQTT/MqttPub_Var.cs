// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Publish.MQTT;

public partial class MqttPublisher
{
    public static async Task MakeVarPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {
        
        var varPub = config.VarPublish!;
        List<ObjectRef> rootObjects = varPub.RootObjects;
        if (rootObjects.Count == 0 && varPub.RootObject != "" && varPub.ModuleID != "") {
            rootObjects.Add(ObjectRef.Make(varPub.ModuleID, varPub.RootObject));
        }

        Timestamp t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
        await Time.WaitUntil(t, abort: shutdown);

        Connection clientFAST = await Util.EnsureConnectOrThrow(info, null);

        var publisher = new MqttPub_Var_Buffer(info.DataFolder, certDir, config);

        while (!shutdown()) {

            clientFAST = await Util.EnsureConnectOrThrow(info, clientFAST);

            VariableValues allValues = await clientFAST.ReadAllVariablesOfObjectTrees(rootObjects);
            VariableValues values = Filter(allValues, varPub);

            publisher.Post(values);

            t = Time.GetNextNormalizedTimestamp(varPub.PublishInterval, varPub.PublishOffset);
            await Time.WaitUntil(t, abort: shutdown);
        }

        await clientFAST.Close();
        publisher.Close();
    }

    private static VariableValues Filter(VariableValues values, MqttVarPub config) {
        var criteria = new Util.FilterCriteria(
            SimpleTagsOnly: config.SimpleTagsOnly,
            NumericTagsOnly: config.NumericTagsOnly,
            SendTagsWithNull: config.SendTagsWithNull, 
            RemoveEmptyTimestamp: true);
        return Util.Filter(values, criteria);
    }
}
