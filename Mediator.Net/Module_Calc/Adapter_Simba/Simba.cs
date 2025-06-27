// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Ifak.Fast.Mediator.Calc.Adapter_Simba;

[Identify(id: "Simba", showWindowVisible: true, showDefinition: true, definitionLabel: "Simu Model", definitionIsCode: false, subtypes: new string[] { "Control", "OnlineSimulation" })]
public class Simba : ExternalAdapter
{
    protected override string GetCommand(Mediator.Config config) {

        const string SIMBA_LOCATION = "simba-location";

        string simbaLoc = config.GetString(SIMBA_LOCATION).Trim();

        if (simbaLoc == "") {
            throw new Exception($"No SIMBA executable specified (setting '{SIMBA_LOCATION}' in AppConfig.xml)");
        }

        string fullLoc = Path.GetFullPath(simbaLoc);

        if (!File.Exists(fullLoc)) {
            throw new Exception($"SIMBA executable not found at {fullLoc} (setting '{SIMBA_LOCATION}' in AppConfig.xml)");
        }

        return fullLoc;
    }

    protected override string GetArgs(Mediator.Config config) {
        return "StartInProcSimbaController MediatorSim.dll MediatorSim.ControlAdapter.InProcSimbaControllerImpl {PORT}";
    }
}
