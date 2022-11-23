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
using System.Globalization;
using OfficeOpenXml;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;

namespace Ifak.Fast.Mediator.Dashboard
{
    [Identify(id: "HistoryPlots", bundle: "Generic", path: "history.html", configType: typeof(ViewConfig), icon: "mdi-chart-line-variant")]
    public class View_HistoryPlots : ViewBase
    {
        private ViewConfig configuration = new ViewConfig();
        private ModuleInfo[] modules = new ModuleInfo[0];
        private List<TabState> tabStates = new List<TabState>();

        class TabState
        {
            public string TabName = "";
            public bool IsLoaded = false;
            public VariableRef[] Variables = new VariableRef[0];
            public TimeRange LastTimeRange = new TimeRange();
        }

        public override async Task OnActivate() {

            if (Config.NonEmpty) {
                configuration = Config.Object<ViewConfig>() ?? new ViewConfig();
            }

            tabStates = GetInitialTabStates(configuration);

            string[] exclude = configuration.ExcludeModules;
            var mods = await Connection.GetModules();
            modules = mods
                .Where(m => !exclude.Contains(m.ID))
                .Select(m => new ModuleInfo() {
                    ID = m.ID,
                    Name = m.Name
                }).ToArray();
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

                        var para = parameters.Object<InitParams>() ?? throw new Exception("InitParams is null");
                        var (windowLeft, windowRight) = GetTimeWindow(para.TimeRange, new List<VTTQs>());

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
                        ObjectInfos infos;
                        try {
                            infos = await Connection.GetObjectsByID(objects);
                        }
                        catch (Exception) {
                            infos = new ObjectInfos(objects.Length);
                            for (int i = 0; i < objects.Length; ++i) {
                                ObjectRef obj = objects[i];
                                try {
                                    infos.Add(await Connection.GetObjectByID(obj));
                                }
                                catch (Exception) {
                                    infos.Add(new ObjectInfo(obj, "???", "???"));
                                }
                            }
                        }

                        foreach (ObjectInfo info in infos) {
                            var numericVariables = info.Variables.Where(IsNumericOrBool).Select(v => v.Name).ToArray();
                            res.ObjectMap[info.ID.ToEncodedString()] = new ObjInfo() {
                                Name = info.Name,
                                Variables = numericVariables
                            };
                        }

                        res.Modules = modules;

                        await EnableEvents(configuration);

                        return ReqResult.OK(res);
                    }

                case "LoadTabData": {

                        var para = parameters.Object<LoadHistoryParams>() ?? throw new Exception("LoadHistoryParams is null");

                        TabState tabState = tabStates.FirstOrDefault(ts => ts.TabName == para.TabName);
                        if (tabState == null) {
                            throw new Exception($"Failed to load history data: tab '{para.TabName}' not found");
                        }

                        tabState.LastTimeRange = para.TimeRange;

                        Timestamp tStart = para.TimeRange.GetStart();
                        Timestamp tEnd = para.TimeRange.GetEnd();

                        TabConfig tabConfig = configuration.Tabs.First(t => t.Name == para.TabName);
                        QualityFilter filter = tabConfig.PlotConfig.FilterByQuality;

                        var listHistories = new List<VTTQs>();

                        foreach (var variable in para.Variables) {
                            try {
                                VTTQs data = await Connection.HistorianReadRaw(variable, tStart, tEnd, para.MaxDataPoints, BoundingMethod.CompressToN, filter);
                                listHistories.Add(data);
                            }
                            catch (Exception) {
                                listHistories.Add(new VTTQs());
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
                                WriteUnifiedData(new JsonDataRecordArrayWriter(writer), listHistories);
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

                case "DownloadFile": {

                        var para = parameters.Object<DownloadDataFileParams>() ?? throw new Exception("DownloadDataFileParams is null");
                        TabConfig tabConfig = configuration.Tabs.First(t => t.Name == para.TabName);
                        QualityFilter filter = tabConfig.PlotConfig.FilterByQuality;

                        Timestamp tStart = para.TimeRange.GetStart();
                        Timestamp tEnd = para.TimeRange.GetEnd();

                        var listHistories = new List<VTTQs>();

                        foreach (var variable in para.Variables) {
                            try {

                                const int ChunckSize = 5000;
                                VTTQs data = await Connection.HistorianReadRaw(variable, tStart, tEnd, ChunckSize, BoundingMethod.TakeFirstN, filter);

                                if (data.Count < ChunckSize) {
                                    listHistories.Add(data);
                                }
                                else {
                                    var buffer = new VTTQs(data);
                                    do {
                                        Timestamp t = data[data.Count - 1].T.AddMillis(1);
                                        data = await Connection.HistorianReadRaw(variable, t, tEnd, ChunckSize, BoundingMethod.TakeFirstN, filter);
                                        buffer.AddRange(data);
                                    }
                                    while (data.Count == ChunckSize);

                                    listHistories.Add(buffer);
                                }
                            }
                            catch (Exception) {
                                listHistories.Add(new VTTQs());
                            }
                        }

                        var columns = new List<string>();
                        columns.Add("Time");
                        columns.AddRange(para.VariableNames);

                        var res = MemoryManager.GetMemoryStream("DownloadFile");
                        try {

                            string contentType;
                            switch (para.FileType) {

                                case FileType.CSV:

                                    contentType = "text/csv";
                                    using (var writer = new StreamWriter(res, Encoding.UTF8, 8 * 1024, leaveOpen: true)) {
                                        WriteUnifiedData(new CsvDataRecordArrayWriter(writer, columns, configuration.DataExport.CSV), listHistories);
                                    }
                                    break;

                                case FileType.Spreadsheet:

                                    contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                                    using (var excel = new ExcelPackage(res)) {
                                        ExcelWorksheet sheet = excel.Workbook.Worksheets.Add("Data Export");
                                        WriteUnifiedData(new ExcelDataRecordArrayWriter(sheet, columns, configuration.DataExport.Spreadsheet), listHistories);
                                        excel.Save();
                                    }
                                    break;

                                default: throw new Exception($"Unknown file type: {para.FileType}");
                            }

                            res.Seek(0, SeekOrigin.Begin);
                            return new ReqResult(200, res, contentType);
                        }
                        catch (Exception) {
                            res.Dispose();
                            throw;
                        }
                    }

                case "SaveItems": {

                        var para = parameters.Object<SaveItemsParams>() ?? throw new Exception("SaveItemsParams is null");

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

                        var para = parameters.Object<SavePlotParams>() ?? throw new Exception("SavePlotParams is null");

                        TabConfig tabConfig = configuration.Tabs.First(t => t.Name == para.TabName);

                        bool reloadData =
                            tabConfig.PlotConfig.MaxDataPoints != para.Plot.MaxDataPoints ||
                            tabConfig.PlotConfig.FilterByQuality != para.Plot.FilterByQuality ||
                            tabConfig.PlotConfig.LeftAxisLimitY != para.Plot.LeftAxisLimitY ||
                            tabConfig.PlotConfig.RightAxisLimitY != para.Plot.RightAxisLimitY;

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

                        var para = parameters.Object<DeleteTabParams>() ?? throw new Exception("DeleteTabParams is null");

                        configuration.Tabs = configuration.Tabs.Where(t => t.Name != para.TabName).ToArray();

                        DataValue newConfig = ConfigToIndentedDataValue(configuration);

                        await Context.SaveViewConfiguration(newConfig);

                        return ReqResult.OK("");
                    }

                case "RenameTab": {

                        var para = parameters.Object<RenameTabParams>() ?? throw new Exception("RenameTabParams is null");

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

                        var para = parameters.Object<AddTabParams>() ?? throw new Exception("AddTabParams is null");
                        var (windowLeft, windowRight) = GetTimeWindow(para.TimeRange, new List<VTTQs>());

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

                        var para = parameters.Object<MoveTabParams>() ?? throw new Exception("MoveTabParams is null");

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

                        var para = parameters.Object<MoveTabParams>() ?? throw new Exception("MoveTabParams is null");

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

                        var pars = parameters.Object<ReadModuleObjectsParams>() ?? throw new Exception("ReadModuleObjectsParams is null");

                        ObjectInfos objects;

                        try {
                            objects = await Connection.GetAllObjects(pars.ModuleID);
                        }
                        catch (Exception) {
                            objects = new ObjectInfos();
                        }

                        return ReqResult.OK(new {
                            Items = objects.Where(o => o.Variables.Any(IsNumericOrBool)).Select(o => new Obj() {
                                Type = o.ClassName,
                                ID = o.ID.ToEncodedString(),
                                Name = o.Name,
                                Variables = o.Variables.Where(IsNumericOrBool).Select(v => v.Name).ToArray()
                            }).ToArray()
                        });
                    }

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        private (Timestamp left, Timestamp right) GetTimeWindow(TimeRange range, List<VTTQs> data) {

            switch (range.Type) {
                case TimeType.Last:

                    Timestamp? latest = data.Any(x => x.Count > 0) ? data.Where(x => x.Count > 0).Max(vtqs => vtqs.Last().T) : (Timestamp?)null;
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

        public override async Task OnVariableHistoryChanged(List<HistoryChange> changes) {

            var setOfChangedVariables = changes.Select(ch => ch.Variable).ToHashSet();
            var tabs = tabStates.ToArray();

            foreach (TabState tabState in tabs) {

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

                    var listHistories = new List<VTTQs>();

                    foreach (var variable in tabState.Variables) {
                        VTTQs data = await Connection.HistorianReadRaw(variable, tStart, tEnd, 10000, BoundingMethod.TakeFirstN);
                        listHistories.Add(data);
                    }

                    var (windowLeft, windowRight) = GetTimeWindow(tabState.LastTimeRange, listHistories);

                    var sb = new StringBuilder();
                    using (var writer = new StringWriter(sb)) {
                        WriteUnifiedData(new JsonDataRecordArrayWriter(writer), listHistories);
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

        private static bool IsNumericOrBool(Variable v) => v.IsNumeric || v.Type == DataType.Bool;

        private void WriteUnifiedData(DataRecordArrayWriter writer, List<VTTQs> variables) {

            HistReader[] vars = variables.Select(v => new HistReader(v)).ToArray();

            writer.WriteArrayStart();

            bool hasNext = vars.Any(v => v.HasValue);

            while (hasNext) {

                writer.WriteRecordStart();

                Timestamp time = Timestamp.Max;

                foreach (var reader in vars) {
                    Timestamp? t = reader.Time;
                    if (t.HasValue && t.Value < time) {
                        time = t.Value;
                    }
                }

                writer.WriteValueTimestamp(time);

                foreach (var reader in vars) {
                    writer.WriteColumSeparator();
                    Timestamp? t = reader.Time;
                    if (t.HasValue && t.Value == time) {
                        DataValue v = reader.Value;
                        if (IsSimpleDouble(v.JSON)) {
                            writer.WriteValueJsonDouble(v.JSON);
                        }
                        else {
                            double? value = v.AsDouble();
                            if (value.HasValue)
                                writer.WriteValueDouble(value.Value);
                            else
                                writer.WriteValueEmpty();
                        }
                        reader.MoveNext();
                    }
                    else {
                        writer.WriteValueEmpty();
                    }
                }
                hasNext = vars.Any(v => v.HasValue);
                writer.WriteRecordEnd(hasMoreRecords: hasNext);
            }

            writer.WriteArrayEnd();
        }

        private static bool IsSimpleDouble(string str) {
            if (str.Length == 0) return false;
            char firstChar = str[0];
            return char.IsDigit(firstChar) || firstChar == '-';
        }

        class HistReader
        {
            private readonly VTTQs data;
            private int idx = 0;

            public HistReader(VTTQs data) {
                this.data = data;
            }

            public Timestamp? Time => (idx < data.Count) ? data[idx].T : (Timestamp?)null;

            public DataValue Value => data[idx].V;

            public void MoveNext() {
                idx += 1;
            }

            public bool HasValue => idx < data.Count;
        }

        abstract class DataRecordArrayWriter
        {
            public abstract void WriteArrayStart();
            public abstract void WriteArrayEnd();
            public abstract void WriteRecordStart();
            public abstract void WriteRecordEnd(bool hasMoreRecords);
            public abstract void WriteValueTimestamp(Timestamp t);
            public abstract void WriteValueText(string txt);
            public abstract void WriteValueJsonDouble(string dbl);
            public abstract void WriteValueDouble(double dbl);
            public abstract void WriteValueEmpty();
            public abstract void WriteColumSeparator();
        }

        class JsonDataRecordArrayWriter : DataRecordArrayWriter
        {
            private readonly TextWriter writer;

            public JsonDataRecordArrayWriter(TextWriter writer) {
                this.writer = writer;
            }

            public override void WriteArrayStart() => writer.Write('[');

            public override void WriteArrayEnd() {
                writer.Write(']');
                writer.Flush();
            }

            public override void WriteRecordStart() => writer.Write('[');

            public override void WriteRecordEnd(bool hasMoreRecords) {
                writer.Write(']');
                if (hasMoreRecords) {
                    writer.WriteLine(',');
                }
            }

            public override void WriteValueEmpty() => writer.Write("null");

            public override void WriteValueText(string txt) => writer.Write(txt);

            public override void WriteValueTimestamp(Timestamp t) => writer.Write(t.JavaTicks);

            public override void WriteColumSeparator() => writer.Write(",");

            public override void WriteValueJsonDouble(string dbl) => writer.Write(dbl);

            public override void WriteValueDouble(double dbl) => writer.Write(dbl.ToString("R", CultureInfo.InvariantCulture));
        }

        class CsvDataRecordArrayWriter : DataRecordArrayWriter {
            private readonly TextWriter writer;
            private readonly string[] columns;
            private readonly CsvDataExport format;

            public CsvDataRecordArrayWriter(TextWriter writer, IList<string> columns, CsvDataExport format) {
                this.writer = writer;
                this.columns = columns.ToArray();
                this.format = format;
            }

            public override void WriteArrayStart() => writer.WriteLine(string.Join(format.ColumnSeparator, columns));

            public override void WriteArrayEnd() {
                writer.Flush();
            }

            public override void WriteRecordStart() { }

            public override void WriteRecordEnd(bool hasMoreRecords) {
                writer.WriteLine();
            }

            public override void WriteValueEmpty() {}

            public override void WriteValueText(string txt) => writer.Write(txt);

            public override void WriteValueTimestamp(Timestamp t) {
                DateTime dt = t.ToDateTime().ToLocalTime();
                string s = dt.ToString(format.TimestampFormat, CultureInfo.InvariantCulture);
                writer.Write(s);
            }

            public override void WriteColumSeparator() => writer.Write(format.ColumnSeparator);

            public override void WriteValueJsonDouble(string dbl) => writer.Write(dbl);

            public override void WriteValueDouble(double dbl) => writer.Write(dbl.ToString("R", CultureInfo.InvariantCulture));
        }

        class ExcelDataRecordArrayWriter : DataRecordArrayWriter
        {
            private readonly ExcelWorksheet sheet;
            private readonly string[] columns;
            private readonly SpreadsheetDataExport format;
            private int row = 0;
            private int col = 0;

            public ExcelDataRecordArrayWriter(ExcelWorksheet sheet, IList<string> columns, SpreadsheetDataExport format) {
                this.sheet = sheet;
                this.columns = columns.ToArray();
                this.format = format;
            }

            public override void WriteArrayStart() {
                for (int n = 0; n < columns.Length; n++) {
                    sheet.Cells[1, n + 1].Value = columns[n];
                    sheet.Cells[1, n + 1].Style.Font.Bold = true;
                    sheet.Cells[1, n + 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                }
                row = 2;
                col = 1;
            }

            public override void WriteArrayEnd() {
                sheet.Cells.AutoFitColumns();
            }

            public override void WriteRecordStart() {
                col = 1;
            }

            public override void WriteRecordEnd(bool hasMoreRecords) {
                row += 1;
            }

            public override void WriteValueEmpty() { }

            public override void WriteValueText(string txt) {
                sheet.Cells[row, col].Value = txt;
            }

            public override void WriteValueTimestamp(Timestamp t) {
                sheet.Cells[row, col].Value = t.ToDateTime().ToLocalTime();
                sheet.Cells[row, col].Style.Numberformat.Format = format.TimestampFormat;
            }

            public override void WriteColumSeparator() {
                col += 1;
            }

            public override void WriteValueJsonDouble(string dbl) {
                WriteValueDouble(DataValue.FromJSON(dbl).GetDouble());
            }

            public override void WriteValueDouble(double dbl) {
                sheet.Cells[row, col].Value = dbl;
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////

        public class ViewConfig
        {
            public TabConfig[] Tabs { get; set; } = new TabConfig[0];
            public DataExport DataExport { get; set; } = new DataExport();
            public string[] ExcludeModules { get; set; } = new string[0];
        }

        public class DataExport
        {
            public CsvDataExport CSV { get; set; } = new CsvDataExport();
            public SpreadsheetDataExport Spreadsheet { get; set; } = new SpreadsheetDataExport();
        }

        public class CsvDataExport
        {
            public string TimestampFormat { get; set; } = "yyyy'-'MM'-'dd' 'HH':'mm':'ss";
            public string ColumnSeparator { get; set; } = ",";
        }

        public class SpreadsheetDataExport
        {
            public string TimestampFormat { get; set; } = "yyyy/mm/dd hh:mm:ss;@";
        }

        public class TabConfig
        {
            public string Name { get; set; } = "";
            public PlotConfig PlotConfig { get; set; } = new PlotConfig();
            public ItemConfig[] Items { get; set; } = new ItemConfig[0];
        }

        public class PlotConfig
        {
            public int MaxDataPoints { get; set; } = 8000;

            public QualityFilter FilterByQuality { get; set; } = QualityFilter.ExcludeBad;

            public string LeftAxisName { get; set; } = "";
            public bool LeftAxisStartFromZero { get; set; } = true;
            public double? LeftAxisLimitY { get; set; } = null;

            public string RightAxisName { get; set; } = "";
            public bool RightAxisStartFromZero { get; set; } = true;
            public double? RightAxisLimitY { get; set; } = null;

            public bool ShouldSerializeLeftAxisLimitY() => LeftAxisLimitY.HasValue;
            public bool ShouldSerializeRightAxisLimitY() => RightAxisLimitY.HasValue;
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

        public class DownloadDataFileParams
        {
            public string TabName { get; set; } = "";
            public TimeRange TimeRange { get; set; } = new TimeRange();
            public VariableRef[] Variables { get; set; } = new VariableRef[0];
            public string[] VariableNames { get; set; } = new string[0];
            public FileType FileType { get; set; }
        }

        public enum FileType
        {
            CSV,
            Spreadsheet
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
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
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

            public TabConfig Configuration { get; set; } = new TabConfig();
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

            public int[]? gridLinePattern { get; set; } = null;
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
