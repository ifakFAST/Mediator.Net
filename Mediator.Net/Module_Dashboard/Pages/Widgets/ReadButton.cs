// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    // [IdentifyWidget(id: "ReadButton")]
    public class ReadButton : WidgetBaseWithConfig<ReadButtonConfig>
    {
        public override string DefaultHeight => "";

        public override string DefaultWidth => "100%";

        ReadButtonConfig configuration => Config;

        public override async Task OnActivate() {
            if (configuration.Variable.HasValue) {
                var variable = configuration.Variable.Value;
                await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), variable);
            }
        }

        public async Task<ReqResult> UiReq_ReadVar() {
            if (configuration.Variable.HasValue) {
                VTQ vtq = await Connection.ReadVariable(configuration.Variable.Value);
                return ReqResult.OK(vtq.V.JSON);
            }
            else {
                return ReqResult.OK("");
            }
        }

        public async Task<ReqResult> UiReq_Button(string param1) {
            await Task.Delay(1);
            return ReqResult.OK(param1);
        }

        public override Task OnVariableValueChanged(List<VariableValue> variables) {
            if (configuration.Variable.HasValue) {
                foreach (VariableValue vv in variables) {
                    if (vv.Variable == configuration.Variable.Value) {
                        var content = new {
                            NewVal = vv.Value.V.JSON
                        };
                        return Context.SendEventToUI("OnVarChanged", content);
                    }
                }
            }
            return Task.FromResult(true);
        }
    }

    public class ReadButtonConfig
    {
        public VariableRef? Variable { get; set; } = null;

        public bool ShouldSerializeFVariable() => Variable.HasValue;
    }
}
