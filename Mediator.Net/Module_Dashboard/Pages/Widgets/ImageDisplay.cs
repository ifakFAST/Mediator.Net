// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using System.IO;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    [IdentifyWidget(id: "ImageDisplay")]
    public class ImageDisplay : WidgetBaseWithConfig<ImageDisplayConfig>
    {
        public override string DefaultHeight => "";

        public override string DefaultWidth => "100%";

        ImageDisplayConfig configuration => Config;

        public override Task OnActivate() {
            return Task.FromResult(true);
        }

        public async Task<ReqResult> UiReq_SetStaticImage(string fileName, byte[] data) {
            
            string imgPath = await Context.SaveWebAsset(Path.GetExtension(fileName), data);

            configuration.ImgPath = imgPath;
            configuration.Mode = "Static";
            await Context.SaveWidgetConfiguration(configuration);
            return ReqResult.OK();
        }
    }

    public class ImageDisplayConfig
    {
        public string ImgPath { get; set; } = "";
        public string Mode { get; set; } = "Static";
    }
}
