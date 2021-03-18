// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    public class TestView1 : ViewBase
    {
        public override Task OnActivate() {

            return Task.FromResult(true);
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            switch (command) {
                case "Read":
                    Console.Out.WriteLine("Parameters: " + parameters.ToString());
                    VTQ vtq = await Connection.ReadVariable(VariableRef.Make("IO", "A", "Value"));
                    return ReqResult.OK(vtq);

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }
    }

    public class TestView2 : ViewBase
    {
        private VariableRef Var = VariableRef.Make("IO", "C", "Value");

        public override Task OnActivate() {
            return Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), Var);
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            switch (command) {
                case "Read":
                    Console.Out.WriteLine("Parameters: " + parameters.ToString());
                    VTQ vtq = await Connection.ReadVariable(Var);
                    return ReqResult.OK(vtq);

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        public override Task OnVariableValueChanged(List<VariableValue> variables) {
            Console.Out.WriteLine("OnVariableValueChanged " + variables[0].ToString());
            return Context.SendEventToUI("VarChange", variables);
        }
    }
}
