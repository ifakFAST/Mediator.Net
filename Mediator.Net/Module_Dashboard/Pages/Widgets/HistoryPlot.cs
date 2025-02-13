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

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    [IdentifyWidget(id: "HistoryPlot")]
    public class HistoryPlot : WidgetBaseWithConfig<HistoryPlotConfig>
    {
        private VariableRefUnresolved[] variablesUnresolved = [];
        private VariableRef[] variables = [];

        private readonly Dictionary<VariableRef, DataType> VariablesType = [];

        private TimeRange LastTimeRange = new TimeRange();
        private bool IsLoaded = false;

        private long dataRevision = 0;

        public override string DefaultHeight => "300px";

        public override string DefaultWidth => "100%";

        HistoryPlotConfig configuration => Config;

        public override Task OnActivate() {
            VariablesUnresolved = configuration.Items.Select(it => it.Variable).ToArray();
            return Task.FromResult(true);
        }

        public VariableRefUnresolved[] VariablesUnresolved {
            get => variablesUnresolved;
            set {
                variablesUnresolved = value;
                ResolveVariables();
            }
        }

        VariableRef[] ResolvedVariablesFromCache => variables;

        VariableRef[] ResolveVariables() {
            VariableRef[] newVariables = variablesUnresolved.Select(v => Context.ResolveVariableRef(v)).ToArray();
            if (!Arrays.Equals(newVariables, variables)) {
                variables = newVariables;
                Task ignored = Connection.EnableVariableHistoryChangedEvents(newVariables);
            }
            return variables;
        }

        public Task<ReqResult> UiReq_GetItemsData() {
            ObjectRef[] usedObjects = configuration.Items.Select(it => it.Variable.Object).Distinct().ToArray();
            return Common.GetNumericVarItemsData(Connection, usedObjects);
        }

        private async Task<DataType> GetDataTypeOrThrow(VariableRef variable) {
            if (!VariablesType.TryGetValue(variable, out DataType variableType)) {
                ObjectInfo objInfo = await Connection.GetObjectByID(variable.Object); // throws if object does not exist
                Variable variableInfo = objInfo.Variables.First(v => v.Name == variable.Name); // throws if variable does not exist
                variableType = variableInfo.Type;
                VariablesType[variable] = variableType;
            }
            return variableType;
        }

        public async Task<ReqResult> UiReq_LoadData(TimeRange timeRange) {

            IsLoaded = false;

            LastTimeRange = timeRange;

            Timestamp tStart = timeRange.GetStart();
            Timestamp tEnd = timeRange.GetEnd();

            List<VTTQs> listHistories = await GetVariablesData(ResolveVariables(), tStart, tEnd, configuration.PlotConfig.MaxDataPoints);
           
            IsLoaded = true;

            var (windowLeft, windowRight) = GetTimeWindow(timeRange, listHistories);

            dataRevision += 1;

            var res = MemoryManager.GetMemoryStream("LoadHistory");
            try {
                using (var writer = new StreamWriter(res, Encoding.ASCII, 8 * 1024, leaveOpen: true)) {
                    writer.Write("{ \"WindowLeft\": " + windowLeft.JavaTicks);
                    writer.Write(", \"WindowRight\": " + windowRight.JavaTicks);
                    writer.Write(", \"DataRevision\": " + dataRevision);
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

        private static VTTQs AggregateValues(VTTQs values, AggregationSpec aggregation) {

            if (values == null || values.Count == 0) {
                return [];
            }

            var result = new VTTQs();

            long resMillis = aggregation.Resolution.TotalMilliseconds;

            Timestamp currentIntervalStart = Timestamp.FromJavaTicks((values[0].T.JavaTicks / resMillis) * resMillis);
            List<double> currentIntervalValues = [];

            foreach (var value in values) {

                double? vv = value.V.AsDouble();
                if (!vv.HasValue) {
                    continue;
                }

                Timestamp intervalStart = Timestamp.FromJavaTicks((value.T.JavaTicks / resMillis) * resMillis);
                if (intervalStart != currentIntervalStart) {
                    // Aggregate the current interval
                    result.Add(AggregateCurrentInterval(currentIntervalStart, currentIntervalValues, aggregation));
                    // Move to the next interval

                    while (!aggregation.SkipEmptyIntervals && currentIntervalStart + aggregation.Resolution < intervalStart) {
                        currentIntervalStart += aggregation.Resolution;
                        result.Add(AggregateCurrentInterval(currentIntervalStart, [], aggregation));
                    }

                    currentIntervalStart = intervalStart;
                    currentIntervalValues.Clear();
                }
                currentIntervalValues.Add(vv.Value);
            }

            // Aggregate the last interval
            if (currentIntervalValues.Count > 0) {
                result.Add(AggregateCurrentInterval(currentIntervalStart, currentIntervalValues, aggregation));
            }

            return result;
        }

        private static VTTQ AggregateCurrentInterval(Timestamp intervalStart, List<double> values, AggregationSpec aggregation) {

            if (values.Count == 0 && aggregation.Agg != Aggregation.Count) {
                return VTTQ.Make(DataValue.Empty, intervalStart, intervalStart, Quality.Good);
            }

            var aggregatedValue = aggregation.Agg switch {
                Aggregation.Average => values.Average(),
                Aggregation.Min => values.Min(),
                Aggregation.Max => values.Max(),
                Aggregation.First => values.First(),
                Aggregation.Last => values.Last(),
                Aggregation.Count => values.Count,
                _ => throw new ArgumentOutOfRangeException(nameof(aggregation), "Invalid aggregation method."),
            };
            return VTTQ.Make(DataValue.FromDouble(aggregatedValue), intervalStart, intervalStart, Quality.Good);
        }

        public enum Aggregation {
            Average,
            Min,
            Max,
            First,
            Last,
            Count
        }

        record AggregationSpec(
            Aggregation Agg,
            Duration Resolution,
            bool SkipEmptyIntervals
        );

        private async Task<List<VTTQs>> GetVariablesData(IEnumerable<VariableRef> variables, Timestamp tStartRange, Timestamp tEndRange, int? maxDataPoints = null, Timestamp? tStartSub = null, AggregationSpec? agg = null) {

            VTTQs TransformData(VTTQs data) {
                if (agg == null) return data;
                return AggregateValues(data, agg);
            }

            Timestamp tStartEffective = tStartSub ?? tStartRange;

            var listHistories = new List<VTTQs>();

            QualityFilter filter = configuration.PlotConfig.FilterByQuality;

            foreach (var variable in variables) {

                try {

                    DataType variableType = await GetDataTypeOrThrow(variable);

                    if (variableType == DataType.Timeseries) {

                        VTTQs data = await Connection.HistorianReadRaw(variable, tStartRange, tEndRange, 1, BoundingMethod.TakeLastN, filter);

                        TimeseriesEntry[] entries = Array.Empty<TimeseriesEntry>();

                        if (data.Count > 0) {
                            entries = data[0].V.Object<TimeseriesEntry[]>() ?? Array.Empty<TimeseriesEntry>();
                        }

                        VTTQs timeseries = new VTTQs(entries.Length);
                        foreach (var entry in entries) {
                            Timestamp t = entry.Time;
                            if (t >= tStartEffective && t <= tEndRange && entry.Value.AsDouble().HasValue) {
                                timeseries.Add(VTTQ.Make(entry.Value, t, t, Quality.Good));
                            }
                        }
                        listHistories.Add(timeseries);
                    }
                    else {

                        if (maxDataPoints.HasValue) {
                            VTTQs data = await Connection.HistorianReadRaw(variable, tStartEffective, tEndRange, maxDataPoints.Value, BoundingMethod.CompressToN, filter);
                            listHistories.Add(data);
                        }
                        else { // Get all data in range (no compression)

                            const int ChunckSize = 5000;
                            VTTQs data = await Connection.HistorianReadRaw(variable, tStartRange, tEndRange, ChunckSize, BoundingMethod.TakeFirstN, filter);

                            if (data.Count < ChunckSize) {
                                listHistories.Add(TransformData(data));
                            }
                            else {
                                var buffer = new VTTQs(data);
                                do {
                                    Timestamp t = data[data.Count - 1].T.AddMillis(1);
                                    data = await Connection.HistorianReadRaw(variable, t, tEndRange, ChunckSize, BoundingMethod.TakeFirstN, filter);
                                    buffer.AddRange(data);
                                }
                                while (data.Count == ChunckSize);

                                listHistories.Add(TransformData(buffer));
                            }
                        }
                    }
                }
                catch (Exception) {
                    listHistories.Add(new VTTQs());
                }
            }

            return listHistories;
        }

        public async Task<ReqResult> UiReq_SaveItems(ItemConfig[] items) {

            VariableRefUnresolved[] newVariables = items.Select(it => it.Variable).ToArray();
            bool reloadData = !Arrays.Equals(newVariables, VariablesUnresolved);

            VariablesUnresolved = newVariables;

            configuration.Items = items;

            await Context.SaveWidgetConfiguration(configuration);

            return ReqResult.OK(new {
                ReloadData = reloadData
            });
        }

        public async Task<ReqResult> UiReq_SavePlot(PlotConfig plot) {

            bool reloadData =
                configuration.PlotConfig.MaxDataPoints != plot.MaxDataPoints ||
                configuration.PlotConfig.FilterByQuality != plot.FilterByQuality ||
                configuration.PlotConfig.LeftAxisLimitY != plot.LeftAxisLimitY ||
                configuration.PlotConfig.RightAxisLimitY != plot.RightAxisLimitY;

            configuration.PlotConfig = plot;

            await Context.SaveWidgetConfiguration(configuration);

            return ReqResult.OK(new {
                ReloadData = reloadData
            });
        }

        public enum FileType
        {
            CSV,
            Spreadsheet
        }

        public record AggregationSpecUI(
            Aggregation Agg,
            int ResolutionCount,
            TimeUnit ResolutionUnit,
            bool SkipEmptyIntervals
        );

        public async Task<ReqResult> UiReq_DownloadFile(
            TimeRange timeRange,
            VariableRefUnresolved[] variables,
            string[] variableNames,
            FileType fileType,
            bool simbaFormat,
            AggregationSpecUI? aggregation) {

            Timestamp tStart = timeRange.GetStart();
            Timestamp tEnd = timeRange.GetEnd();

            if (tStart >= tEnd) {
                throw new Exception("Invalid time range: start >= end");
            }

            AggregationSpec? spec = null;

            if (aggregation != null) {
                AggregationSpecUI aggUI = aggregation;
                if (aggUI.ResolutionCount <= 0) {
                    throw new Exception("Resolution must be greater than zero.");
                }
                Duration resolution = TimeRange.DurationFromTimeUnit(aggUI.ResolutionCount, aggUI.ResolutionUnit);
                spec = new AggregationSpec(aggUI.Agg, resolution, aggUI.SkipEmptyIntervals);
            }

            VariableRef[] resolvedVariables = variables.Select(v => Context.ResolveVariableRef(v)).ToArray();
            var listHistories = await GetVariablesData(resolvedVariables, tStart, tEnd, agg: spec);

            var columns = new List<string> {
                "Time"
            };
            columns.AddRange(variableNames);

            var res = MemoryManager.GetMemoryStream("DownloadFile");
            try {
                string contentType;
                switch (fileType) {

                    case FileType.CSV:

                        contentType = "text/csv";
                        using (var writer = new StreamWriter(res, Encoding.UTF8, 8 * 1024, leaveOpen: true)) {
                            WriteUnifiedData(new CsvDataRecordArrayWriter(writer, columns, configuration.DataExport.CSV), listHistories);
                        }
                        break;

                    case FileType.Spreadsheet:

                        contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                        using (var excel = new ExcelPackage(res)) {

                            if (simbaFormat) {
                                WriteExcelDataSIMBA(excel, columns, listHistories, configuration.DataExport.Spreadsheet);
                            }
                            else {
                                ExcelWorksheet sheet = excel.Workbook.Worksheets.Add("Data Export");
                                WriteUnifiedData(new ExcelDataRecordArrayWriter(sheet, columns, configuration.DataExport.Spreadsheet), listHistories);
                            }
                            excel.Save();
                        }
                        break;

                    default: throw new Exception($"Unknown file type: {fileType}");
                }

                res.Seek(0, SeekOrigin.Begin);
                return new ReqResult(200, res, contentType);
            }
            catch (Exception) {
                res.Dispose();
                throw;
            }
        }

        public override async Task OnVariableHistoryChanged(List<HistoryChange> changes) {

            var setOfChangedVariables = changes.Select(ch => ch.Variable).ToHashSet();

            VariableRef[] variables = ResolvedVariablesFromCache;

            if (IsLoaded && variables.Any(v => setOfChangedVariables.Contains(v))) {

                VariableRef[] changedTabVariables = variables.Where(v => setOfChangedVariables.Contains(v)).ToArray();

                Timestamp tMinChanged = Timestamp.Max;

                foreach (var v in changedTabVariables) {
                    Timestamp t = changes.First(ch => ch.Variable == v).ChangeStart;
                    if (t < tMinChanged) {
                        tMinChanged = t;
                    }
                }

                Timestamp tStartRange = LastTimeRange.GetStart();
                Timestamp tEndRange = LastTimeRange.GetEnd();
                Timestamp tStartEffective = Timestamp.MaxOf(tMinChanged, tStartRange);

                if (tEndRange < tStartEffective) {
                    return;
                }

                List<VTTQs> listHistories = await GetVariablesData(variables, tStartRange, tEndRange, 5000, tStartEffective);

                var (windowLeft, windowRight) = GetTimeWindow(LastTimeRange, listHistories);

                var sb = new StringBuilder();
                using (var writer = new StringWriter(sb)) {
                    WriteUnifiedData(new JsonDataRecordArrayWriter(writer), listHistories);
                }

                var evt = new {
                    WindowLeft = windowLeft.JavaTicks,
                    WindowRight = windowRight.JavaTicks,
                    DataRevision = dataRevision,
                    Data = sb.ToString()
                };

                await Context.SendEventToUI("DataAppend", evt);
            }
        }

        private static (Timestamp left, Timestamp right) GetTimeWindow(TimeRange range, List<VTTQs> data) {

            switch (range.Type) {
                case TimeType.Last:

                    Timestamp? latest = data.Any(x => x.Count > 0) ? data.Where(x => x.Count > 0).Max(vtqs => vtqs.Last().T) : (Timestamp?)null;
                    var now = Timestamp.Now.TruncateMilliseconds().AddSeconds(1);
                    var right = latest.HasValue ? Timestamp.MaxOf(now, latest.Value) : now;
                    var left = range.GetStart();
                    return (left, right);

                case TimeType.Range:

                    return (range.GetStart(), range.GetEnd());

                default:
                    throw new Exception("Unknown range type: " + range.Type);
            }
        }

        private static void WriteUnifiedData(DataRecordArrayWriter writer, List<VTTQs> variables) {

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

            public override void WriteValueDouble(double dbl) {
                if (double.IsNaN(dbl) || double.IsInfinity(dbl)) {
                    writer.Write("null");
                }
                else {
                    writer.Write(dbl.ToString("R", CultureInfo.InvariantCulture));
                }
            }
        }

        class CsvDataRecordArrayWriter : DataRecordArrayWriter
        {
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

            public override void WriteValueEmpty() { }

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

        private static void WriteExcelDataSIMBA(
            ExcelPackage excel, 
            IList<string> columns, 
            List<VTTQs> variables, 
            SpreadsheetDataExport format) {

            const double MillisecondsPerDay = 24 * 60 * 60 * 1000.0;

            string[] tags = columns.Skip(1).ToArray();

            Timestamp tFirst = Timestamp.Max;
            Timestamp tLast = Timestamp.Empty;            

            foreach (VTTQs vttqs in variables) {
                if (vttqs.Count > 0) {
                    Timestamp t1 = vttqs.First().T;
                    if (t1 < tFirst) {
                        tFirst = t1;
                    }
                    Timestamp tn = vttqs.Last().T;
                    if (tn > tLast) {
                        tLast = tn;
                    }
                }
            }

            long tBase = tFirst.JavaTicks;

            for (int i = 0; i < tags.Length; i++) {

                string tagName = tags[i];
                VTTQs vttqs = variables[i];

                ExcelWorksheet sheet = excel.Workbook.Worksheets.Add(tagName);

                sheet.Cells[1, 1].Value = "Time (d)";
                sheet.Cells[1, 1].Style.Font.Bold = true;
                sheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                sheet.Column(1).Width = 12;

                sheet.Cells[1, 2].Value = "Value";
                sheet.Cells[1, 2].Style.Font.Bold = true;
                sheet.Cells[1, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                sheet.Column(2).Width = 12;

                for (int j = 0; j < vttqs.Count; j++) {
                    VTTQ vtq = vttqs[j];
                    double? vOpt = vtq.V.AsDouble();
                    if (vOpt.HasValue) {
                        double d = (vtq.T.JavaTicks - tBase) / MillisecondsPerDay;
                        double v = vOpt.Value;
                        sheet.Cells[2 + j, 1].Value = d;
                        sheet.Cells[2 + j, 2].Value = v;
                    }
                }
            }

            if (tFirst != tLast) {

                ExcelWorksheet sheetTime = excel.Workbook.Worksheets.Add("Time");

                sheetTime.Cells[1, 1].Value = "Time (d)";
                sheetTime.Cells[1, 1].Style.Font.Bold = true;
                sheetTime.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                sheetTime.Column(1).Width = 12;

                sheetTime.Cells[1, 2].Value = "Time Str";
                sheetTime.Cells[1, 2].Style.Font.Bold = true;
                sheetTime.Cells[1, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                sheetTime.Column(2).Width = 20;

                sheetTime.Cells[2 + 0, 1].Value = 0;
                sheetTime.Cells[2 + 0, 2].Value = tFirst.ToDateTime().ToLocalTime();
                sheetTime.Cells[2 + 0, 2].Style.Numberformat.Format = format.TimestampFormat;

                sheetTime.Cells[2 + 1, 1].Value = (tLast.JavaTicks - tBase) / MillisecondsPerDay;
                sheetTime.Cells[2 + 1, 2].Value = tLast.ToDateTime().ToLocalTime();
                sheetTime.Cells[2 + 1, 2].Style.Numberformat.Format = format.TimestampFormat;
            }
        }
    }

    public class HistoryPlotConfig
    {
        public PlotConfig PlotConfig { get; set; } = new PlotConfig();
        public ItemConfig[] Items { get; set; } = new ItemConfig[0];
        public DataExport DataExport { get; set; } = new DataExport();
    }

    public class PlotConfig
    {
        public int MaxDataPoints { get; set; } = 8000;

        public QualityFilter FilterByQuality { get; set; } = QualityFilter.ExcludeBad;

        public string LeftAxisName { get; set; } = "";
        public bool LeftAxisStartFromZero { get; set; } = true;

        public string RightAxisName { get; set; } = "";
        public bool RightAxisStartFromZero { get; set; } = true;
        public double? LeftAxisLimitY { get; set; } = null;

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
        public VariableRefUnresolved Variable { get; set; }

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

}
