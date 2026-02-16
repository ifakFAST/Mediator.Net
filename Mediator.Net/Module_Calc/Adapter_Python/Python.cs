// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_Python;

[Identify(id: "Python", showWindowVisible: false, showDefinition: true, definitionLabel: "Script", definitionIsCode: true, codeLang: "python")]
public class Python : ExternalAdapter
{
    protected override string GetCommand(Mediator.Config config) {
        string assemblyFile = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (OperatingSystem.IsWindows()) {
            return System.IO.Path.ChangeExtension(assemblyFile, "exe");
        }
        return System.IO.Path.ChangeExtension(assemblyFile, null)!;
    }

    protected override string GetArgs(Mediator.Config config) {
        return "{PORT} AdapterPython";
    }
}
