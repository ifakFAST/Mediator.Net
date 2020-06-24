// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Ifak.Fast.Mediator.Calc.Adapter_Simba
{
    [Identify(id: "Simba", showWindowVisible: true, showDefinition: true, definitionLabel: "Simu Model", definitionIsCode: false)]
    public class Simba : ExternalAdapter
    {
        protected override string GetCommand(Mediator.Config config) {
            return Path.GetFullPath(config.GetString("simba-location"));
        }

        protected override string GetArgs(Mediator.Config config) {
            return "StartInProcSimbaController MediatorSim.dll MediatorSim.ControlAdapter.InProcSimbaControllerImpl {PORT}";
        }
    }
}
