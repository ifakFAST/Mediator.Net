// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Configuration;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    [IdentifyWidget(id: "TextDisplay")]
    public class TextDisplay : WidgetBaseWithConfig<TextDisplayConfig>
    {
        public override string DefaultHeight => "";

        public override string DefaultWidth => "100%";

        TextDisplayConfig configuration => Config;

        public override Task OnActivate() {
            return Task.FromResult(true);
        }

        public async Task<ReqResult> UiReq_SaveConfig(string text, string mode) {
            configuration.Text = text;
            configuration.Mode = mode;
            await Context.SaveWidgetConfiguration(configuration);
            return ReqResult.OK();
        }
    }

    public class TextDisplayConfig
    {
        const string Sample = @"# Main Heading
## Second level heading
### Third level heading

You can **bold text** by using double asterisks or double underscores.

You can *italicize text* using single asterisks or single underscores.

You can use ~~strikethrough~~ by using two tildes.

You can create a horizontal line with three hyphens, asterisks, or underscores:

---

Unordered list:

- Unordered list item
- A second unordered list item
- The last item 

Numbered list:

1. Numbered list item 1
2. Numbered list item 2
3. Numbered list item 3

## Further notes

You can create a [link](https://example.com) like this.
You can also create tables:

| Column 1 | Column 2 |
| -------- | -------- |
| Cell 1   | Cell 2   |
| Cell 3   | Cell 4   |
";

        public string Text { get; set; } = Sample.Replace("\r\n", "\n");
        public string Mode { get; set; } = "Markdown";
    }
}
