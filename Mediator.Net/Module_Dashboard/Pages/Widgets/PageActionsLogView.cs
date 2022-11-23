// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using System.Globalization;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets {

    [IdentifyWidget(id: "PageActionsLogView")]
    public class PageActionsLogView : WidgetBaseWithConfig<PageActionsLogViewConfig> {

        public override string DefaultHeight => "";

        public override string DefaultWidth => "100%";

        private VariableRef GetPageActionLog() => Context.GetPageActionLogVariable();

        public override async Task OnActivate() {
            VariableRef vr = GetPageActionLog();
            await Connection.EnableVariableHistoryChangedEvents(vr);
        }

        public async Task<ReqResult> UiReq_ReadValues() {
            return ReqResult.OK(await ReadValues());
        }

        public async Task<LogEntry[]> ReadValues() {
            LogAction[] actions = await Context.GetLoggedPageActions(1000);
            return actions
                .OrderByDescending(x => x.Time)
                .Select(LogEntry.Make)
                .ToArray();
        }
      
        public override async Task OnVariableHistoryChanged(List<HistoryChange> changes) {
            var entries = await ReadValues();
            await Context.SendEventToUI("OnValuesChanged", entries);
        }
    }

    public sealed class LogEntry {

        public long Timestamp { get; set; }
        public string Time { get; set; } = "";
        public string User { get; set; } = "";
        public string Action { get; set; } = "";

        public static LogEntry Make(LogAction log) {

            string time = log.Time.ToDateTime().ToLocalTime().ToString("yyyy'-'MM'-'dd\u00A0HH':'mm", CultureInfo.InvariantCulture);

            return new LogEntry() {
                Timestamp = log.Time.JavaTicks,
                Time = time,
                User = log.UserLogin,
                Action = log.Action,
            };
        }
    }

    public class PageActionsLogViewConfig { }
}
