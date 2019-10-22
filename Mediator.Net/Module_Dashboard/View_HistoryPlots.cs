// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Dashboard
{
    [Identify(id: "HistoryPlots", bundle: "Generic", path: "history.html")]
    public class View_HistoryPlots : ViewBase
    {
        private ViewConfig configuration = new ViewConfig();

        private List<TabState> tabStates = new List<TabState>();

        class TabState
        {
            public string TabName = "";
            public bool IsLoaded = false;
            public VariableRef[] Variables = new VariableRef[0];
            public TimeRange LastTimeRange = new TimeRange();
        }

        public override Task OnActivate() {

            if (Config.NonEmpty) {
                configuration = Config.Object<ViewConfig>();
            }

            tabStates = GetInitialTabStates(configuration);

            return Task.FromResult(true);
        }

        private static List<TabState> GetInitialTabStates(ViewConfig configuration) {
            return configuration.Tabs.Select(t => new TabState() {
                TabName = t.Name,
                Variables = t.Items.Select(it => it.Variable).ToArray()
            }).ToList();
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            switch (command) {

                case "Init": {

                        var para = parameters.Object<InitParams>();
                        var (windowLeft, windowRight) = GetTimeWindow(para.TimeRange, new List<VTTQ[]>());

                        var res = new LoadHistoryResult();
                        res.Tabs = configuration.Tabs.Select(t => new TabRes() {
                            Name = t.Name,
                            MaxDataPoints = t.PlotConfig.MaxDataPoints,
                            Variables = t.Items.Select(it => it.Variable).ToArray(),
                            Options = MakeGraphOptions(t),
                            Configuration = t
                        }).ToArray();

                        res.WindowLeft = windowLeft.JavaTicks;
                        res.WindowRight = windowRight.JavaTicks;

                        tabStates = GetInitialTabStates(configuration);

                        ObjectRef[] objects = configuration.Tabs.SelectMany(t => t.Items.Select(it => it.Variable.Object)).Distinct().ToArray();
                        ObjectInfo[] infos;
                        try {
                            infos = await Connection.GetObjectsByID(objects);
                        }
                        catch (Exception) {
                            infos = new ObjectInfo[objects.Length];
                            for (int i = 0; i < objects.Length; ++i) {
                                ObjectRef obj = objects[i];
                                try {
                                    infos[i] = await Connection.GetObjectByID(obj);
                                }
                                catch (Exception) {
                                    infos[i] = new ObjectInfo(obj, "???", "???");
                                }
                            }
                        }

                        foreach (ObjectInfo info in infos) {
                            var numericVariables = info.Variables.Where(IsNumeric).Select(v => v.Name).ToArray();
                            res.ObjectMap[info.ID.ToEncodedString()] = new ObjInfo() {
                                Name = info.Name,
                                Variables = numericVariables
                            };
                        }

                        var mods = await Connection.GetModules();
                        res.Modules = mods.Select(m => new ModuleInfo() {
                            ID = m.ID,
                            Name = m.Name
                        }).ToArray();

                        await EnableEvents(configuration);

                        return ReqResult.OK(res);
                    }

                case "LoadTabData": {

                        var para = parameters.Object<LoadHistoryParams>();

                        TabState tabState = tabStates.First(ts => ts.TabName == para.TabName);
                        tabState.LastTimeRange = para.TimeRange;

                        Timestamp tStart = para.TimeRange.GetStart();
                        Timestamp tEnd = para.TimeRange.GetEnd();

                        var listHistories = new List<VTTQ[]>();

                        foreach (var variable in para.Variables) {
                            try {
                                VTTQ[] data = await Connection.HistorianReadRaw(variable, tStart, tEnd, para.MaxDataPoints, BoundingMethod.CompressToN);
                                listHistories.Add(data);
                            }
                            catch (Exception) {
                                listHistories.Add(new VTTQ[0]);
                            }
                        }

                        tabState.IsLoaded = true;

                        var (windowLeft, windowRight) = GetTimeWindow(para.TimeRange, listHistories);

                        var res = MemoryManager.GetMemoryStream("LoadHistory");
                        try {
                            using (var writer = new StreamWriter(res, Encoding.ASCII, 8 * 1024, leaveOpen: true)) {
                                writer.Write("{ \"WindowLeft\": " + windowLeft.JavaTicks);
                                writer.Write(", \"WindowRight\": " + windowRight.JavaTicks);
                                writer.Write(", \"Data\": ");
                                WriteUnifiedData(writer, listHistories);
                                writer.Write('}');
                            }
                            res.Seek(0, SeekOrigin.Begin);
                        }
                        catch (Exception) {
                            res.Dispose();
                            throw;
                        }
                        return new ReqResult(200, res);
                    }

                case "SaveItems": {

                        var para = parameters.Object<SaveItemsParams>();

                        TabState tabState = tabStates.First(ts => ts.TabName == para.TabName);

                        VariableRef[] newVariables = para.Items.Select(it => it.Variable).ToArray();
                        bool reloadData = !Arrays.Equals(newVariables, tabState.Variables);

                        tabState.Variables = newVariables;

                        TabConfig tabConfig = configuration.Tabs.FirstOrDefault(t => t.Name == para.TabName);
                        tabConfig.Items = para.Items;

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        if (reloadData) {
                            await EnableEvents(configuration);
                        }

                        return ReqResult.OK(new {
                            Options = MakeGraphOptions(tabConfig),
                            Variables = newVariables,
                            Configuration = tabConfig,
                            ReloadData = reloadData
                        });
                    }

                case "SavePlot": {

                        var para = parameters.Object<SavePlotParams>();

                        TabConfig tabConfig = configuration.Tabs.First(t => t.Name == para.TabName);

                        bool reloadData = tabConfig.PlotConfig.MaxDataPoints != para.Plot.MaxDataPoints;

                        tabConfig.PlotConfig = para.Plot;

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        return ReqResult.OK(new {
                            Options = MakeGraphOptions(tabConfig),
                            Configuration = tabConfig,
                            ReloadData = reloadData
                        });
                    }

                case "DeleteTab": {

                        var para = parameters.Object<DeleteTabParams>();

                        configuration.Tabs = configuration.Tabs.Where(t => t.Name != para.TabName).ToArray();

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        return ReqResult.OK("");
                    }

                case "RenameTab": {

                        var para = parameters.Object<RenameTabParams>();

                        if (para.NewName != para.TabName && configuration.Tabs.Any(t => t.Name == para.NewName)) throw new Exception("Tab name already exists!");

                        TabConfig tabConfig = configuration.Tabs.First(t => t.Name == para.TabName);
                        tabConfig.Name = para.NewName;

                        TabState tabState = tabStates.First(ts => ts.TabName == para.TabName);
                        tabState.TabName = para.NewName;

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        return ReqResult.OK(new {
                            Configuration = tabConfig
                        });
                    }

                case "AddTab": {

                        var para = parameters.Object<AddTabParams>();
                        var (windowLeft, windowRight) = GetTimeWindow(para.TimeRange, new List<VTTQ[]>());

                        if (configuration.Tabs.Any(t => t.Name == para.NewName)) throw new Exception("Tab name already exists!");

                        TabConfig tabConfig = new TabConfig();
                        tabConfig.Name = para.NewName;

                        var tabs = configuration.Tabs.ToList();
                        tabs.Add(tabConfig);
                        configuration.Tabs = tabs.ToArray();

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        var tabState = new TabState() {
                            TabName = para.NewName,
                            LastTimeRange = para.TimeRange
                        };
                        tabStates.Add(tabState);

                        return ReqResult.OK(new {
                            NewTab = new TabRes() {
                                Name = tabConfig.Name,
                                MaxDataPoints = tabConfig.PlotConfig.MaxDataPoints,
                                Options = MakeGraphOptions(tabConfig),
                                Configuration = tabConfig
                            },
                            WindowLeft = windowLeft.JavaTicks,
                            WindowRight = windowRight.JavaTicks
                        });
                    }

                case "MoveLeft": {

                        var para = parameters.Object<MoveTabParams>();

                        int i = configuration.Tabs.ToList().FindIndex(t => t.Name == para.TabName);
                        if (i <= 0) throw new Exception("Can't move left");

                        var tmp = configuration.Tabs[i];
                        configuration.Tabs[i] = configuration.Tabs[i - 1];
                        configuration.Tabs[i - 1] = tmp;

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        return ReqResult.OK("");
                    }

                case "MoveRight": {

                        var para = parameters.Object<MoveTabParams>();

                        int i = configuration.Tabs.ToList().FindIndex(t => t.Name == para.TabName);
                        if (i >= configuration.Tabs.Length - 1) throw new Exception("Can't move right");

                        var tmp = configuration.Tabs[i];
                        configuration.Tabs[i] = configuration.Tabs[i + 1];
                        configuration.Tabs[i + 1] = tmp;

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        return ReqResult.OK("");
                    }

                case "ReadModuleObjects": {

                        var pars = parameters.Object<ReadModuleObjectsParams>();

                        ObjectInfo[] objects;

                        try {
                            objects = await Connection.GetAllObjects(pars.ModuleID);
                        }
                        catch (Exception) {
                            objects = new ObjectInfo[0];
                        }

                        return ReqResult.OK(new {
                            Items = objects.Where(o => o.Variables.Any(IsNumeric)).Select(o => new Obj() {
                                Type = o.ClassName,
                                ID = o.ID.ToEncodedString(),
                                Name = o.Name,
                                Variables = o.Variables.Where(IsNumeric).Select(v => v.Name).ToArray()
                            })
                        });
                    }

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        private (Timestamp left, Timestamp right) GetTimeWindow(TimeRange range, List<VTTQ[]> data) {

            switch (range.Type) {
                case TimeType.Last:

                    Timestamp? latest = data.Any(x => x.Length > 0) ? data.Where(x => x.Length > 0).Max(vtqs => vtqs.Last().T) : (Timestamp?)null;
                    var now = Timestamp.Now.TruncateMilliseconds().AddSeconds(1);
                    var right = latest.HasValue ? Timestamp.MaxOf(now, latest.Value) : now;
                    var left = now - TimeRange.DurationFromTimeRange(range);
                    return (left, right);

                case TimeType.Range:

                    return (range.GetStart(), range.GetEnd());

                default:
                    throw new Exception("Unknown range type: " + range.Type);
            }
        }

        private static DataValue ConfigToIndentedDataValue(ViewConfig configuration) {
            string json = StdJson.ObjectToString(configuration, indented: true);
            string c = "\r\n" + Indent(json, 8) + "\r\n      ";
            return DataValue.FromJSON(c);
        }

        private static string Indent(string value, int size) {
            var indent = new string(' ', size);
            var strArray = value.Split("\r\n");
            var sb = new StringBuilder();
            for (int i = 0; i < strArray.Length; ++i) {
                string s = strArray[i];
                sb.Append(indent);
                sb.Append(s);
                if (i < strArray.Length - 1) {
                    sb.Append("\r\n");
                }
            }
            return sb.ToString();
        }

        public override async Task OnVariableHistoryChanged(HistoryChange[] changes) {
            await Foo(changes);
            await Task.Delay(1000);
        }

        private async Task Foo(HistoryChange[] changes) {

            var setOfChangedVariables = changes.Select(ch => ch.Variable).ToHashSet();

            foreach (TabState tabState in tabStates) {

                if (tabState.IsLoaded && tabState.Variables.Any(v => setOfChangedVariables.Contains(v))) {

                    VariableRef[] changedTabVariables = tabState.Variables.Where(v => setOfChangedVariables.Contains(v)).ToArray();

                    Timestamp tMinChanged = Timestamp.Max;

                    foreach (var v in changedTabVariables) {
                        Timestamp t = changes.First(ch => ch.Variable == v).ChangeStart;
                        if (t < tMinChanged) {
                            tMinChanged = t;
                        }
                    }

                    Timestamp tStart = tMinChanged;
                    Timestamp tEnd = tabState.LastTimeRange.GetEnd();

                    var listHistories = new List<VTTQ[]>();

                    foreach (var variable in tabState.Variables) {
                        VTTQ[] data = await Connection.HistorianReadRaw(variable, tStart, tEnd, 10000, BoundingMethod.TakeFirstN);
                        listHistories.Add(data);
                    }

                    var (windowLeft, windowRight) = GetTimeWindow(tabState.LastTimeRange, listHistories);

                    var sb = new StringBuilder();
                    using (var writer = new StringWriter(sb)) {
                        WriteUnifiedData(writer, listHistories);
                    }

                    var evt = new TabDataEvent() {
                        TabName = tabState.TabName,
                        WindowLeft = windowLeft.JavaTicks,
                        WindowRight = windowRight.JavaTicks,
                        Data = sb.ToString()
                    };

                    await Context.SendEventToUI("TabDataAppend", evt);
                }
            }
        }

        private async Task EnableEvents(ViewConfig configuration) {
            var variables = configuration.Tabs.SelectMany(t => t.Items.Select(it => it.Variable)).Distinct().ToArray();
            await Connection.DisableChangeEvents();
            await Connection.EnableVariableHistoryChangedEvents(variables);
        }

        private static bool IsNumeric(Variable v) => v.IsNumeric;

        private void WriteUnifiedData(TextWriter writer, List<VTTQ[]> variables) {

            writer.Write('[');

            HistReader[] vars = variables.Select(v => new HistReader(v)).ToArray();

            bool hasNext = vars.Any(v => v.HasValue);

            while (hasNext) {

                writer.Write('[');

                Timestamp time = Timestamp.Max;

                foreach (var reader in vars) {
                    Timestamp? t = reader.Time;
                    if (t.HasValue && t.Value < time) {
                        time = t.Value;
                    }
                }

                writer.Write(time.JavaTicks);

                foreach (var reader in vars) {
                    writer.Write(',');
                    Timestamp? t = reader.Time;
                    if (t.HasValue && t.Value == time) {
                        DataValue v = reader.Value;
                        writer.Write(v.JSON);
                        reader.MoveNext();
                    }
                    else {
                        writer.Write("null");
                    }
                }
                writer.Write(']');
                hasNext = vars.Any(v => v.HasValue);
                if (hasNext) {
                    writer.WriteLine(',');
                }
            }

            writer.Write(']');
            writer.Flush();
        }

        class HistReader
        {
            private readonly VTTQ[] data;
            private int idx = 0;

            public HistReader(VTTQ[] data) {
                this.data = data;
            }

            public Timestamp? Time => (idx < data.Length) ? data[idx].T : (Timestamp?)null;

            public DataValue Value => data[idx].V;

            public void MoveNext() {
                idx += 1;
            }

            public bool HasValue => idx < data.Length;
        }


        ////////////////////////////////////////////////////////////////////////////////////////////////////////

        public class ViewConfig
        {
            public TabConfig[] Tabs { get; set; } = new TabConfig[0];
        }

        public class TabConfig
        {
            public string Name { get; set; } = "";
            public PlotConfig PlotConfig { get; set; } = new PlotConfig();
            public ItemConfig[] Items { get; set; } = new ItemConfig[0];
        }

        public class PlotConfig
        {
            public int MaxDataPoints { get; set; } = 12000;

            public string LeftAxisName { get; set; } = "";
            public bool LeftAxisStartFromZero { get; set; } = true;

            public string RightAxisName { get; set; } = "";
            public bool RightAxisStartFromZero { get; set; } = true;
        }

        public class ItemConfig
        {
            public string Name { get; set; } = "";
            public string Color { get; set; } = "";
            public double Size { get; set; } = 3.0;
            public SeriesType SeriesType { get; set; } = SeriesType.Scatter;
            public Axis Axis { get; set; } = Axis.Left;
            public bool Checked { get; set; } = true;
            public VariableRef Variable { get; set; }

            public string GetLabel() => Name + ((Axis == Axis.Right) ? " [R]" : "");
        }

        public enum SeriesType
        {
            Line,
            Scatter
        }

        public enum Axis
        {
            Left,
            Right
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public class InitParams
        {
            public TimeRange TimeRange { get; set; } = new TimeRange();
        }

        public class LoadHistoryParams
        {
            public string TabName { get; set; } = "";
            public TimeRange TimeRange { get; set; } = new TimeRange();
            public VariableRef[] Variables { get; set; } = new VariableRef[0];
            public int MaxDataPoints { get; set; } = 12000;
        }

        public class SaveItemsParams
        {
            public string TabName { get; set; } = "";
            public ItemConfig[] Items { get; set; } = new ItemConfig[0];
        }

        public class SavePlotParams
        {
            public string TabName { get; set; } = "";
            public PlotConfig Plot { get; set; } = new PlotConfig();
        }

        public class DeleteTabParams
        {
            public string TabName { get; set; } = "";
        }

        public class MoveTabParams
        {
            public string TabName { get; set; } = "";
        }

        public class RenameTabParams
        {
            public string TabName { get; set; } = "";
            public string NewName { get; set; } = "";
        }

        public class AddTabParams
        {
            public TimeRange TimeRange { get; set; } = new TimeRange();
            public string NewName { get; set; } = "";
        }

        public class ReadModuleObjectsParams
        {
            public string ModuleID { get; set; } = "";
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        private static DyGraphOptions MakeGraphOptions(TabConfig tab) {
            return new DyGraphOptions() {
                labels = new List<string>() { "Date" }.Concat(tab.Items.Select(it => it.GetLabel())).ToArray(),
                series = MakeSeriesOptions(tab.Items),
                axes = MakeAxisOptions(tab.PlotConfig),
                drawAxesAtZero = true,
                includeZero = true,
                connectSeparatedPoints = true,
                ylabel = tab.PlotConfig.LeftAxisName,
                y2label = tab.PlotConfig.RightAxisName,
                visibility = tab.Items.Select(v => v.Checked).ToArray()
            };
        }

        private static Dictionary<string, AxisInfo> MakeAxisOptions(PlotConfig plot) {
            var res = new Dictionary<string, AxisInfo>();
            res["y"] = new AxisInfo() {
                independentTicks = true,
                drawGrid = true,
                includeZero = plot.LeftAxisStartFromZero,
                gridLinePattern = null
            };
            res["y2"] = new AxisInfo() {
                independentTicks = true,
                drawGrid = true,
                includeZero = plot.RightAxisStartFromZero,
                gridLinePattern = new int[] { 2, 2 }
            };
            return res;
        }

        private static Dictionary<string, SeriesInfo> MakeSeriesOptions(ItemConfig[] items) {
            var res = new Dictionary<string, SeriesInfo>();
            foreach (var it in items) {
                bool scatter = it.SeriesType == SeriesType.Scatter;
                res[it.GetLabel()] = new SeriesInfo() {
                    axis = it.Axis == Axis.Left ? "y" : "y2",
                    drawPoints = scatter,
                    strokeWidth = scatter ? 0.0 : it.Size,
                    pointSize = it.Size,
                    color = it.Color
                };
            }
            return res;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public class LoadHistoryResult
        {
            public TabRes[] Tabs { get; set; } = new TabRes[0];
            public long WindowLeft { get; set; }
            public long WindowRight { get; set; }
            public Dictionary<string, ObjInfo> ObjectMap = new Dictionary<string, ObjInfo>();
            public ModuleInfo[] Modules = new ModuleInfo[0];
        }

        public class ModuleInfo
        {
            public string ID { get; set; }
            public string Name { get; set; }
        }

        public class ObjInfo
        {
            public string Name { get; set; } = "";
            public string[] Variables { get; set; } = new string[0];
        }

        public class Obj
        {
            public string Type { get; set; } = "";
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
            public string[] Variables { get; set; } = new string[0];
        }

        public class TabRes
        {
            public string Name { get; set; } = "";
            public int MaxDataPoints { get; set; } = 10;
            public VariableRef[] Variables { get; set; } = new VariableRef[0];
            public DyGraphOptions Options { get; set; } = new DyGraphOptions();
            public string[] HistoryData { get; set; } = new string[0];

            public TabConfig Configuration { get; set; }
        }

        public class DyGraphOptions
        {
            public string[] labels { get; set; } = new string[0];
            public Dictionary<string, SeriesInfo> series { get; set; } = new Dictionary<string, SeriesInfo>();
            public string legend { get; set; } = "always";
            public bool drawAxesAtZero { get; set; } = true;
            public bool includeZero { get; set; } = true;
            public bool connectSeparatedPoints { get; set; } = true;

            public Dictionary<string, AxisInfo> axes { get; set; } = new Dictionary<string, AxisInfo>();
            public string ylabel { get; set; } = "";
            public string y2label { get; set; } = "";

            public string legendFormatter { get; set; } = "";

            public bool[] visibility { get; set; } = new bool[0];
        }

        public class AxisInfo
        {
            public bool independentTicks { get; set; } = true;
            public bool drawGrid { get; set; } = true;
            public bool includeZero { get; set; } = false;

            public int[] gridLinePattern { get; set; } = null;
        }

        public class SeriesInfo
        {
            public string axis { get; set; } = "y";
            public string color { get; set; } = "#0000FF";
            public double strokeWidth { get; set; } = 1.0;
            public bool drawPoints { get; set; } = true;
            public double pointSize { get; set; } = 3.0;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////

        public class TabDataEvent
        {
            public string TabName { get; set; } = "";
            public long WindowLeft { get; set; }
            public long WindowRight { get; set; }
            public string Data { get; set; } = "";
        }

    }
}
