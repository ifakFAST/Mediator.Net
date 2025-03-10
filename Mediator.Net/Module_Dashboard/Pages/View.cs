// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Json.Linq;
using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;

namespace Ifak.Fast.Mediator.Dashboard.Pages
{
    [Identify(id: "Pages", bundle: "Generic", path: "pages.html", configType: typeof(Config), icon: "mdi-chart-line-variant")]
    public class View : ViewBase
    {
        private Config configuration = new();
        private readonly Dictionary<string, PageState> Pages = [];

        private PageState? activePage = null;
        private ImmutableDictionary<string, Type> widgetTypes = ImmutableDictionary<string, Type>.Empty;
        private readonly Dictionary<string, string> configVarValues = [];

        public override Task OnActivate() {

            if (Config.NonEmpty) {
                configuration = Config.Object<Config>() ?? new Config();
                // configuration = Example.Get();

                InitConfigVariables();
            }

            widgetTypes = Reflect
                .GetAllNonAbstractSubclasses(typeof(WidgetBase))
                .Where(t => t.GetCustomAttribute<IdentifyWidget>() != null)
                .ToImmutableDictionary(t => t.GetCustomAttribute<IdentifyWidget>()!.ID);

            foreach (var page in configuration.Pages) {
                Pages[page.ID] = new PageState(page, Connection, widgetTypes, this);
            }

            return Task.FromResult(true);
        }

        private void InitConfigVariables() {
            configVarValues.Clear();
            foreach (var v in configuration.ConfigVariables) {
                configVarValues[v.ID] = v.DefaultValue;
            }
        }

        public async Task<ReqResult> UiReq_Init() {

            if (configuration.Pages.Length > 0) {
                await UiReq_SwitchToPage(configuration.Pages.First().ID);
            }

            var result = ReqResult.OK(new {
                configuration,
                widgetTypes = widgetTypes.Keys.OrderBy(s => s).ToArray(),
            });

            return result;
        }

        public async Task<ReqResult> UiReq_SwitchToPage(string pageID) {

            if (pageID != "" && !Pages.ContainsKey(pageID)) {
                throw new Exception($"Unknown page id: {pageID}");
            }

            await DeactivatePage();

            if (pageID != "") {
                PageState page = Pages[pageID];
                await page.OnActivate();
                activePage = page;
            }

            return ReqResult.OK();
        }

        private async Task DeactivatePage() {
            if (activePage != null) {
                await activePage.OnDeactivate();
                await Connection.DisableChangeEvents(true, true, true);
                activePage = null;
            }
        }

        public async Task<ReqResult> UiReq_SaveConfigVariables(ConfigVariable[] configVariables) {

            // check if all variables are unique:
            var unique = new HashSet<string>();
            foreach (var v in configVariables) {

                if (string.IsNullOrWhiteSpace(v.ID)) {
                    throw new Exception("Config variable id must not be empty");
                }

                int idx = v.ID.IndexOfAny(['.', '$', '{', '}', '-', '>', '<']);
                if (idx >= 0) {
                    throw new Exception($"Invalid character '{v.ID[idx]}' in config variable id: {v.ID}");
                }

                if (!unique.Add(v.ID)) {
                    throw new Exception($"Duplicate config variable id: {v.ID}");
                }
            }

            configuration.ConfigVariables = configVariables;
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            InitConfigVariables();
            return ReqResult.OK();
        }

        public async Task<ReqResult> UiReq_SetConfigVariableValues(Dictionary<string, string> variableValues) {

            //foreach (var variableID in variableValues.Keys) {
            //    if (!configVarValues.ContainsKey(variableID)) {
            //        Console.Out.WriteLine($"Unknown config variable id: {variableID}");
            //    }
            //}

            foreach (var pair in variableValues) {
                string variableID = pair.Key;
                string value = pair.Value;
                if (configVarValues.ContainsKey(variableID)) {
                    configVarValues[variableID] = value;
                }
            }

            var payload = new {
                ChangedVarValues = variableValues
            };
            await Context.SendEventToUI("ConfigVariableValuesChanged", payload);

            return ReqResult.OK();
        }

        public async Task<ReqResult> UiReq_ConfigPageAdd(string pageID, string title) {

            var page = new Page() {
                ID = pageID,
                Name = title,
                Rows = new Row[] {
                    new Row() {
                        Columns = new Column[] {
                            new Column(),
                            new Column(),
                        }
                    },
                    new Row() {
                        Columns = new Column[] {
                            new Column(),
                            new Column(),
                        }
                    }
                }
            };

            var pages = configuration.Pages.ToList();
            pages.Add(page);
            configuration.Pages = pages.ToArray();

            Pages[page.ID] = new PageState(page, Connection, widgetTypes, this);

            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(configuration.Pages);
        }

        public async Task<ReqResult> UiReq_ConfigPageDuplicate(string pageID, string newPageID, string title) {

            CheckActivePage(pageID);

            string pageCopy = StdJson.ObjectToString(activePage!.Page);
            var page = StdJson.ObjectFromString<Page>(pageCopy)!;

            page.ID = newPageID;
            page.Name = title;

            var pages = configuration.Pages.ToList();
            pages.Add(page);
            configuration.Pages = pages.ToArray();

            Pages[page.ID] = new PageState(page, Connection, widgetTypes, this);

            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(configuration.Pages);
        }

        public async Task<ReqResult> UiReq_ConfigPageRename(string pageID, string title) {
            CheckActivePage(pageID);
            activePage!.Page.Name = title;
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK();
        }

        public async Task<ReqResult> UiReq_ConfigPageDelete(string pageID) {
            CheckActivePage(pageID);
            await DeactivatePage();

            configuration.Pages = configuration.Pages.Where(p => p.ID != pageID).ToArray();

            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK();
        }

        public async Task<ReqResult> UiReq_ConfigPageMoveLeft(string pageID) {

            CheckActivePage(pageID);

            int i = configuration.Pages.ToList().FindIndex(p => p.ID == pageID);
            if (i <= 0) throw new Exception("Can't move left");

            var tmp = configuration.Pages[i];
            configuration.Pages[i] = configuration.Pages[i - 1];
            configuration.Pages[i - 1] = tmp;

            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK();
        }

        public async Task<ReqResult> UiReq_ConfigPageMoveRight(string pageID) {

            CheckActivePage(pageID);

            int i = configuration.Pages.ToList().FindIndex(p => p.ID == pageID);
            if (i < 0 || i >= configuration.Pages.Length - 1) throw new Exception("Can't move right");

            var tmp = configuration.Pages[i];
            configuration.Pages[i] = configuration.Pages[i + 1];
            configuration.Pages[i + 1] = tmp;

            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK();
        }

        public async Task<ReqResult> UiReq_ConfigInsertRow(string pageID, int row, bool below) {
            CheckActivePage(pageID);
            activePage!.ConfigInsertRow(row, below);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigMoveRow(string pageID, int row, bool down) {
            CheckActivePage(pageID);
            activePage!.ConfigMoveRow(row, down);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigRemoveRow(string pageID, int row) {
            CheckActivePage(pageID);
            activePage!.ConfigRemoveRow(row);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigInsertCol(string pageID, int row, int col, bool right) {
            CheckActivePage(pageID);
            activePage!.ConfigInsertCol(row, col, right);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigMoveCol(string pageID, int row, int col, bool right) {
            CheckActivePage(pageID);
            activePage!.ConfigMoveCol(row, col, right);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigRemoveCol(string pageID, int row, int col) {
            CheckActivePage(pageID);
            activePage!.ConfigRemoveCol(row, col);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigSetColWidth(string pageID, int row, int col, ColumnWidth width) {
            CheckActivePage(pageID);
            activePage!.ConfigSetColWidth(row, col, width);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigWidgetAdd(string pageID, int row, int col, string type, string id) {
            CheckActivePage(pageID);
            activePage!.ConfigWidgetAdd(row, col, type, id);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigWidgetDelete(string pageID, string widgetID) {
            CheckActivePage(pageID);
            await activePage!.ConfigWidgetDelete(widgetID);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigWidgetMove(string pageID, int row, int col, int widget, bool down) {
            CheckActivePage(pageID);
            activePage!.ConfigWidgetMove(row, col, widget, down);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigWidgetSetHeight(string pageID, int row, int col, int widget, string newHeight) {
            CheckActivePage(pageID);
            activePage!.ConfigWidgetSetHeight(row, col, widget, newHeight);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigWidgetSetWidth(string pageID, int row, int col, int widget, string newWidth) {
            CheckActivePage(pageID);
            activePage!.ConfigWidgetSetWidth(row, col, widget, newWidth);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public async Task<ReqResult> UiReq_ConfigWidgetSetTitle(string pageID, int row, int col, int widget, string newTitle) {
            CheckActivePage(pageID);
            activePage!.ConfigWidgetSetTitle(row, col, widget, newTitle);
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            return ReqResult.OK(activePage.Page);
        }

        public enum ObjectFilter {
            WithVariables,
            WithMembers
        }

        public async Task<ReqResult> UiReq_ReadModuleObjects(string ModuleID, DataType ForType = DataType.Float64, ObjectFilter Filter = ObjectFilter.WithVariables) {

            ObjectInfos objects;

            try {
                objects = await Connection.GetAllObjects(ModuleID);
            }
            catch (Exception) {
                objects = [];
            }

            async Task<ObjInfo[]> GetObjInfos() {
                if (Filter == ObjectFilter.WithVariables) {
                    Func<Variable, bool> isMatch = GetMatchPredicate(ForType);
                    return ReadObjectsWithVariables(Connection, objects, isMatch);
                }
                else {
                    return await ReadObjectsWithMembers(Connection, objects);
                }
            }

            ObjInfo[] res = await GetObjInfos();
            return ReqResult.OK(new {
                Items = res
            });
        }

        private static Func<Variable, bool> GetMatchPredicate(DataType type) {
            if (type.IsNumeric() || type == DataType.Bool) {
                return (v) => v.IsNumeric || v.Type == DataType.Bool || v.Type == DataType.Timeseries;
            }
            return (v) => v.Type == type;
        }

        public sealed class ObjInfo {
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string[] Members { get; set; } = [];
            public string[] Variables { get; set; } = [];
        }

        public static async Task<ObjInfo[]> ReadObjectsWithMembers(Connection connection, ObjectInfos objects) {

            string[] moduleIDs = objects.Select(obj => obj.ID.ModuleID).Distinct().ToArray();
            var mapMeta = new Dictionary<string, Dictionary<string, ClassInfo>>();
            foreach (string moduleID in moduleIDs) {
                MetaInfos meta = await connection.GetMetaInfos(moduleID);
                mapMeta[moduleID] = meta.Classes.ToDictionary(cls => cls.FullName);
            }

            Func<SimpleMember, bool> isNumeric = (sm) => sm.Type.IsNumeric() || sm.Type == DataType.Bool || sm.Type == DataType.JSON;

            Func<ObjectInfo, bool> hasMembers = (obj) => {
                var mapClasses = mapMeta[obj.ID.ModuleID];
                if (!mapClasses.TryGetValue(obj.ClassNameFull, out ClassInfo? cls)) return false;
                return cls.SimpleMember.Any(isNumeric);
            };

            Func<ObjectInfo, string[]> getMembers = (obj) => {
                var mapClasses = mapMeta[obj.ID.ModuleID];
                if (!mapClasses.TryGetValue(obj.ClassNameFull, out ClassInfo? cls)) return [];
                return cls.SimpleMember.Where(isNumeric).Select(sm => sm.Name).ToArray();
            };

            return objects.Where(hasMembers).Select(o => new ObjInfo() {
                Type = o.ClassNameFull,
                ID = o.ID.ToEncodedString(),
                Name = o.Name,
                Members = getMembers(o)
            }).ToArray();
        }

        public static ObjInfo[] ReadObjectsWithVariables(Connection connection, ObjectInfos objects, Func<Variable, bool> isVariableMatch) {

            return objects.Where(o => o.Variables.Any(isVariableMatch))
                 .Select(o => new ObjInfo() {
                     Type = o.ClassNameFull,
                     ID = o.ID.ToEncodedString(),
                     Name = o.Name,
                     Variables = o.Variables.Where(isVariableMatch).Select(v => v.Name).ToArray()
                 }).ToArray();
        }

        private static bool IsNumericOrBoolOrString(Variable v) => v.IsNumeric || v.Type == DataType.Bool || v.Type == DataType.String || v.Type == DataType.Timeseries;

        private void CheckActivePage(string pageID) {
            if (activePage == null) {
                throw new Exception($"No active page!");
            }
            if (pageID != activePage.Page.ID) {
                throw new Exception($"Invalid page id: {pageID}");
            }
        }

        public Task<ReqResult> UiReq_RequestFromWidget(
            string pageID,
            string widgetID,
            string request,
            JObject parameter) {

            if (!Pages.ContainsKey(pageID)) {
                throw new Exception($"Unknown page id: {pageID}");
            }
            PageState page = Pages[pageID];
            return page.ForwardWidgetRequest(widgetID, request, parameter);
        }

        internal async Task SaveWidgetConfiguration(string pageID, Widget widget, object newWidgetConfig) {
            JObject objNewWidgetConfig = StdJson.ObjectToJObject(newWidgetConfig);
            widget.Config = objNewWidgetConfig;
            DataValue newViewConfig = DataValue.FromObject(configuration, indented: true);
            await Context.SaveViewConfiguration(newViewConfig);
            var msg = new {
                PageID = pageID,
                WidgetID = widget.ID,
                Config = widget.Config,
            };
            await Context.SendEventToUI("WidgetConfigChanged", msg);
        }

        internal Task SendWidgetEventToUI(string pageID, string widgetID, string eventName, object payload) {
            var msg = new EventMsg(pageID, widgetID, eventName, payload);
            return Context.SendEventToUI("WidgetEvent", msg);
        }

        private VariableRef GetPageLogRef(string pageID) {
            ObjectRef viewID = ID;
            string fullPageID = viewID.LocalObjectID + ".Page." + pageID;
            return VariableRef.Make(viewID.ModuleID, fullPageID, "ActionLog");
        }

        internal async Task LogPageAction(string pageID, string action) {
            var varRef = GetPageLogRef(pageID);
            var user = await Connection.GetLoginUser();
            var logAction = new LogAction() {
                Time = Timestamp.Now,
                UserID = user.ID,
                UserLogin = user.Login,
                UserName = user.Name,
                Action = action,
            };
            DataValue dv = DataValue.FromObject(logAction, indented: true);
            VTQ vtq = VTQ.Make(dv, Timestamp.Now, Quality.Good);
            await Connection.HistorianModify(varRef, ModifyMode.Insert, vtq);
        }

        internal VariableRef GetPageActionLogVariable(string pageID) {
            return GetPageLogRef(pageID);
        }

        internal async Task<LogAction[]> GetLoggedPageActions(string pageID, int limit) {
            var varRef = GetPageLogRef(pageID);
            var vttqs = await Connection.HistorianReadRaw(varRef, Timestamp.Empty, Timestamp.Max, limit, BoundingMethod.TakeLastN);
            var res = new List<LogAction>(vttqs.Count);
            foreach (var vttq in vttqs) {
                LogAction? ll = vttq.V.Object<LogAction>();
                if (ll == null) continue;
                res.Add(ll);
            }
            return res.ToArray();
        }

        public class EventMsg
        {
            public string PageID { get; set; }
            public string WidgetID { get; set; }
            public string EventName { get; set; }
            public object Content { get; set; }

            public EventMsg(string pageID, string widgetID, string eventName, object content) {
                PageID = pageID;
                WidgetID = widgetID;
                EventName = eventName;
                Content = content;
            }
        }

        public override Task OnVariableValueChanged(List<VariableValue> variables) {
            PageState? page = activePage;
            if (page == null) { return Task.FromResult(true); }
            return page.OnVariableValueChanged(variables);
        }

        public override Task OnConfigChanged(List<ObjectRef> changedObjects) {
            PageState? page = activePage;
            if (page == null) { return Task.FromResult(true); }
            return page.OnConfigChanged(changedObjects);
        }

        public override Task OnVariableHistoryChanged(List<HistoryChange> changes) {
            PageState? page = activePage;
            if (page == null) { return Task.FromResult(true); }
            return page.OnVariableHistoryChanged(changes);
        }

        public override Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
            PageState? page = activePage;
            if (page == null) { return Task.FromResult(true); }
            return page.OnAlarmOrEvents(alarmOrEvents);
        }

        internal Task<string> SaveWebAsset(string fileExtension, byte[] data) {
            return Context.SaveWebAsset(fileExtension, data);
        }

        internal VariableRef ResolveVariableRef(VariableRefUnresolved v) {
            string objID = v.Object.LocalObjectID;
            string newObjID = VariableReplacer.ReplaceVariables(objID, configVarValues);
            ObjectRef newObject = ObjectRef.Make(v.Object.ModuleID, newObjID);
            return VariableRef.Make(newObject, v.Name);
        }
    }

    internal static partial class VariableReplacer {

        internal static string ReplaceVariables(string input, Dictionary<string, string> variables) {

            if (string.IsNullOrEmpty(input) || variables == null) {
                return input;
            }

            return MyRegex().Replace(input, match => {
                var varId = match.Groups[1].Value;
                if (!variables.TryGetValue(varId, out var value)) {
                    return match.Value; // Keep original if varId not found
                }
                return value;
            });
        }

        // Pattern matches ${varID.key} where varID and key are non-empty strings not containing dots or closing braces
        [GeneratedRegex(@"\$\{([^\}]+)\}")]
        private static partial Regex MyRegex();
    }

    public class PageState
    {
        private readonly Dictionary<string, WidgetBase> widgetMap = new Dictionary<string, WidgetBase>();
        private readonly List<WidgetBase> listWidgets = new List<WidgetBase>();
        public readonly Page Page;
        private readonly ImmutableDictionary<string, Type> widgetTypes;
        private readonly View view;
        private readonly Connection connection;

        public PageState(Page page, Connection connection, ImmutableDictionary<string, Type> widgetTypes, View view) {

            this.widgetTypes = widgetTypes;
            this.view = view;
            this.connection = connection;
            this.Page = page;

            foreach (var widget in page.AllWidgets()) {
                WidgetInit(widget);
            }
        }

        private WidgetBase WidgetInit(Widget widget) {
            if (widgetMap.ContainsKey(widget.ID)) {
                throw new Exception($"Non unique widget id: {widget.ID}");
            }
            string strType = widget.Type;
            if (!widgetTypes.ContainsKey(strType)) {
                throw new Exception($"Unknown widget type '{strType}'");
            }
            Type type = widgetTypes[strType];
            object? widgetObj = Activator.CreateInstance(type);
            if (widgetObj == null) throw new Exception($"Failed to create widget instance of type {type}");
            WidgetBase widgetInst = (WidgetBase)widgetObj;
            widgetInst.OnInit(connection, new WidgetContextImpl(Page, widget, view), widget);
            widgetMap[widget.ID] = widgetInst;
            listWidgets.Add(widgetInst);
            return widgetInst;
        }

        public void ConfigInsertRow(int row, bool below) {
            var newRow = new Row() {
                Columns = new Column[] {
                    new Column() {
                        Width = ColumnWidth.Fill,
                    }
                }
            };
            var rows = Page.Rows.ToList();
            rows.Insert(below ? row + 1: row, newRow);
            Page.Rows = rows.ToArray();
        }

        public void ConfigMoveRow(int row, bool down) {
            var theRow = Page.Rows[row];
            if (down) {
                Page.Rows[row] = Page.Rows[row + 1];
                Page.Rows[row + 1] = theRow;
            }
            else {
                Page.Rows[row] = Page.Rows[row - 1];
                Page.Rows[row - 1] = theRow;
            }
        }

        public void ConfigRemoveRow(int row) {
            var theRow = Page.Rows[row];
            Page.Rows = Page.Rows.Where(r => r != theRow).ToArray();
        }

        public void ConfigInsertCol(int row, int col, bool right) {
            var newCol = new Column() {
                Width = ColumnWidth.Fill,
            };
            var cols = Page.Rows[row].Columns.ToList();
            cols.Insert(right ? col + 1 : col, newCol);
            Page.Rows[row].Columns = cols.ToArray();
        }

        public void ConfigSetColWidth(int row, int col, ColumnWidth width) {
            var theCol = Page.Rows[row].Columns[col];
            theCol.Width = width;
        }

        public void ConfigMoveCol(int row, int col, bool right) {
            var theCol = Page.Rows[row].Columns[col];
            if (right) {
                Page.Rows[row].Columns[col] = Page.Rows[row].Columns[col + 1];
                Page.Rows[row].Columns[col + 1] = theCol;
            }
            else {
                Page.Rows[row].Columns[col] = Page.Rows[row].Columns[col - 1];
                Page.Rows[row].Columns[col - 1] = theCol;
            }
        }

        public void ConfigRemoveCol(int row, int col) {
            var theCol = Page.Rows[row].Columns[col];
            Page.Rows[row].Columns = Page.Rows[row].Columns.Where(c => c != theCol).ToArray();
        }

        public void ConfigWidgetAdd(int row, int col, string type, string id) {
            var theCol = Page.Rows[row].Columns[col];
            var w = new Widget() {
                ID = id,
                Type = type,
            };
            var widget = WidgetInit(w);
            var widgets = theCol.Widgets.ToList();
            w.Height = widget.DefaultHeight;
            w.Width = widget.DefaultWidth;
            widgets.Add(w);
            theCol.Widgets = widgets.ToArray();
        }

        public async Task ConfigWidgetDelete(string widgetID) {
            if (!widgetMap.ContainsKey(widgetID)) {
                throw new Exception($"Unknown widget id: {widgetID}");
            }
            WidgetBase widget = widgetMap[widgetID];
            await widget.OnDeactivate();
            widgetMap.Remove(widgetID);
            listWidgets.Remove(widget);
            Column column = Page.ColumnFromWidget(widgetID);
            column.Widgets = column.Widgets.Where(w => w.ID != widgetID).ToArray();
        }

        public void ConfigWidgetMove(int row, int col, int widget, bool down) {
            var theWidget = Page.Rows[row].Columns[col].Widgets[widget];

            if (down) {
                Page.Rows[row].Columns[col].Widgets[widget] = Page.Rows[row].Columns[col].Widgets[widget + 1];
                Page.Rows[row].Columns[col].Widgets[widget + 1] = theWidget;
            }
            else {
                Page.Rows[row].Columns[col].Widgets[widget] = Page.Rows[row].Columns[col].Widgets[widget - 1];
                Page.Rows[row].Columns[col].Widgets[widget - 1] = theWidget;
            }
        }

        public void ConfigWidgetSetHeight(int row, int col, int widget, string height) {
            var theWidget = Page.Rows[row].Columns[col].Widgets[widget];
            theWidget.Height = height;
        }

        public void ConfigWidgetSetWidth(int row, int col, int widget, string width) {
            var theWidget = Page.Rows[row].Columns[col].Widgets[widget];
            theWidget.Width = width;
        }

        public void ConfigWidgetSetTitle(int row, int col, int widget, string title) {
            var theWidget = Page.Rows[row].Columns[col].Widgets[widget];
            theWidget.Title = title;
        }

        public Task<ReqResult> ForwardWidgetRequest(string widgetID, string request, JObject parameter) {
            if (!widgetMap.ContainsKey(widgetID)) {
                throw new Exception($"Unknown widget id: {widgetID}");
            }
            WidgetBase widget = widgetMap[widgetID];
            return widget.OnUiRequestAsync(request, parameter);
        }

        public async Task OnActivate() {
            await Task.WhenAll(listWidgets.Select(w => w.OnActivate()));
        }

        public async Task OnDeactivate() {
            await Task.WhenAll(listWidgets.Select(w => w.OnDeactivate()));
        }

        public async Task OnVariableValueChanged(List<VariableValue> variables) {
            await Task.WhenAll(listWidgets.Select(w => w.OnVariableValueChanged(variables)));
        }

        public async Task OnConfigChanged(List<ObjectRef> changedObjects) {
            await Task.WhenAll(listWidgets.Select(w => w.OnConfigChanged(changedObjects)));
        }

        public async Task OnVariableHistoryChanged(List<HistoryChange> changes) {
            await Task.WhenAll(listWidgets.Select(w => w.OnVariableHistoryChanged(changes)));
        }

        public async Task OnAlarmOrEvents(List<AlarmOrEvent> alarmOrEvents) {
            await Task.WhenAll(listWidgets.Select(w => w.OnAlarmOrEvents(alarmOrEvents)));
        }

        public class WidgetContextImpl : WidgetContext
        {
            private readonly View view;
            private readonly Widget widget;
            private readonly string pageID;

            public WidgetContextImpl(Page page, Widget widget, View view) {
                this.view = view;
                this.widget = widget;
                this.pageID = page.ID;
            }

            public Task SaveWidgetConfiguration(object newWidgetConfig) {
                return view.SaveWidgetConfiguration(pageID, widget, newWidgetConfig);
            }

            public Task SendEventToUI(string eventName, object payload) {
                return view.SendWidgetEventToUI(pageID, widget.ID, eventName, payload);
            }

            public Task LogPageAction(string action) {
                return view.LogPageAction(pageID, action);
            }

            public Task<LogAction[]> GetLoggedPageActions(int limit) {
                return view.GetLoggedPageActions(pageID, limit);
            }

            public VariableRef GetPageActionLogVariable() {
                return view.GetPageActionLogVariable(pageID);
            }

            public Task<string> SaveWebAsset(string fileExtension, byte[] data) {
                return view.SaveWebAsset(fileExtension, data);
            }

            public VariableRef ResolveVariableRef(VariableRefUnresolved v) {
                return view.ResolveVariableRef(v);
            }
        }
    }

    public static class PageExtension
    {
        public static IEnumerable<Widget> AllWidgets(this Page page) {
            foreach (var row in page.Rows) {
                foreach (var col in row.Columns) {
                    foreach (var widget in col.Widgets) {
                        yield return widget;
                    }
                }
            }
        }

        public static Column ColumnFromWidget(this Page page, string widgetID) {
            foreach (var row in page.Rows) {
                foreach (var col in row.Columns) {
                    if (col.Widgets.Any(w => w.ID == widgetID)) {
                        return col;
                    }
                }
            }
            throw new Exception($"Failed to find column of Widget '{widgetID}'");
        }
    }

    public record struct VariableRefUnresolved(ObjectRef Object, string Name);

}
