// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Publish.SQL;

internal class VarPubTask {

    public static Task MakeVarPubTask(SQLConfig config, ModuleInitInfo info, Func<bool> shutdown) {
        
        var publisher = config.DatabaseType switch {
            Database.PostgreSQL => new SQLPubVar_Postgres(info.DataFolder, config),
            _ => throw new Exception("Unknown DatabaseType: " + config.DatabaseType)
        };

        return Publish.VarPubTask.MakeVarPubTask(publisher, config.VarPublish!, info, shutdown);
    }
}
