// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Publish.MQTT;

public partial class MqttPublisher
{
    public static Task MakeVarPubTask(MqttConfig config, ModuleInitInfo info, string certDir, Func<bool> shutdown) {

        var publisher = new MqttPub_Var_Buffer(info.DataFolder, certDir, config);

        return VarPubTask.MakeVarPubTask(publisher, config.VarPublish!, info, shutdown);
    }
}
