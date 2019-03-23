// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    [Identify(id: "URL", bundle: "URL", path: "not used")]
    public class View_ExtURL : ViewBase
    {
        public override Task OnActivate() {
            return Task.FromResult(true);
        }

        public override Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {
            return Task.FromResult(ReqResult.Bad(""));
        }
    }

    public class ViewURLConfig
    {
        public string URL { get; set; } = "";
    }
}
