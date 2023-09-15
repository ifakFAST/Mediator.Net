// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Publish.OPC_UA;

internal class VarPubTask {

    public static Task MakeVarPubTask(OpcUaConfig config, ModuleInitInfo info, Func<bool> shutdown) {

        var publisher = new UA_PubVar(info.DataFolder, config);

        return Publish.VarPubTask.MakeVarPubTask(publisher, config.VarPublish!, info, shutdown);
    }
}
