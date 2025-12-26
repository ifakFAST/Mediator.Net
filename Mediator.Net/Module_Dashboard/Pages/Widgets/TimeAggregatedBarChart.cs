// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets;

[IdentifyWidget(id: "TimeAggregatedBarChart")]
public class TimeAggregatedBarChart : WidgetBaseWithConfig<TimeAggregatedBarChartConfig>
{
    public override string DefaultHeight => "300px";
    public override string DefaultWidth => "100%";

    TimeAggregatedBarChartConfig configuration => Config;

    public override Task OnActivate() {

        return Task.FromResult(true);
    }

    public Task<ReqResult> UiReq_GetItemsData() {
        ObjectRef[] usedObjects = configuration.DataSeries?
            .Select(ds => ds.Variable.Object)
            .Where(obj => !string.IsNullOrEmpty(obj.ToEncodedString()))
            .Distinct()
            .ToArray() ?? [];
        return Common.GetNumericVarItemsData(Connection, usedObjects);
    }

    public async Task<ReqResult> UiReq_SaveConfig(TimeAggregatedBarChartConfig config) {
        configuration.ChartConfig = config.ChartConfig ?? new TimeAggregatedBarChartMainConfig();
        configuration.DataSeries = config.DataSeries ?? [];
        await Context.SaveWidgetConfiguration(configuration);
        return ReqResult.OK();
    }

    public async Task<ReqResult> UiReq_LoadData(TimeRange timeRange, Dictionary<string, string> configVars) {

        Context.SetConfigVariables(configVars);

        List<TimeBucket> buckets = CalculateTimeBuckets();

        var seriesData = new List<SeriesData>();

        if (configuration.DataSeries != null && configuration.DataSeries.Length > 0 && buckets.Count > 0) {

            Task<SeriesData?> GetData(TimeAggregatedBarChartDataSeries dataSeries) {
                return GetSeriesDataAsync(dataSeries, buckets);
            }
            SeriesData?[] results = await Common.TransformAsync(configuration.DataSeries, GetData);
            seriesData.AddRange(results.Where(r => r != null)!);
        }

        var response = new {
            BucketStartTimes = buckets.Select(b => b.Start).ToArray(),
            Granularity = configuration.ChartConfig.TimeGranularity,
            WeekStart = configuration.ChartConfig.WeekStart,
            Series = seriesData
        };

        return ReqResult.OK(response);
    }

    private async Task<SeriesData?> GetSeriesDataAsync(TimeAggregatedBarChartDataSeries? dataSeries, List<TimeBucket> buckets) {
        if (dataSeries == null) {
            return null;
        }

        VariableRefUnresolved unresolvedVar = dataSeries.Variable;
        if (string.IsNullOrEmpty(unresolvedVar.Object.ToEncodedString()) || string.IsNullOrEmpty(unresolvedVar.Name)) {
            return null;
        }

        VariableRef variable;
        try {
            variable = Context.ResolveVariableRef(unresolvedVar);
        }
        catch {
            return null;
        }

        // Build interval bounds array from buckets
        Timestamp[] intervalBounds = new Timestamp[buckets.Count + 1];
        for (int i = 0; i < buckets.Count; i++) {
            intervalBounds[i] = buckets[i].Start;
        }
        intervalBounds[buckets.Count] = buckets[buckets.Count - 1].End;

        // Convert BarAggregation enum to Aggregation enum
        Aggregation aggregation = dataSeries.Aggregation switch {
            BarAggregation.Average => Aggregation.Average,
            BarAggregation.Sum => Aggregation.Sum,
            BarAggregation.Count => Aggregation.Count,
            BarAggregation.Min => Aggregation.Min,
            BarAggregation.Max => Aggregation.Max,
            BarAggregation.First => Aggregation.First,
            BarAggregation.Last => Aggregation.Last,
            _ => throw new Exception("Unsupported aggregation type"),
        };

        VTQs aggregatedData;
        try {
            aggregatedData = await Connection.HistorianReadAggregatedIntervals(
                variable,
                intervalBounds,
                aggregation,
                QualityFilter.ExcludeBad
            );
        }
        catch {
            return null;
        }

        // Extract values from VTQs
        double?[] aggregatedValues = aggregatedData
            .Select(vtq => vtq.V.AsDouble())
            .ToArray();

        return new SeriesData {
            Name = string.IsNullOrEmpty(dataSeries.Name) ? variable.Name : dataSeries.Name,
            Color = string.IsNullOrEmpty(dataSeries.Color) ? "#888888" : dataSeries.Color,
            Values = aggregatedValues
        };
    }

    private List<TimeBucket> CalculateTimeBuckets() {

        Timestamp startTs = configuration.ChartConfig.StartTime;
        Timestamp endTs = configuration.ChartConfig.EndTime ?? Timestamp.Now;

        if (startTs.IsEmpty || endTs.IsEmpty || endTs <= startTs) {
            return [];
        }

        var granularity = configuration.ChartConfig.TimeGranularity;
        DateTime start = ToUtcStart(startTs.ToDateTime());
        DateTime end = ToUtcStart(endTs.ToDateTime());

        DateTime current = AlignToGranularityStart(start, granularity, configuration.ChartConfig.WeekStart);
        var buckets = new List<TimeBucket>();

        while (current < end) {
            DateTime next = GetNextBucketStart(current, granularity, configuration.ChartConfig.WeekStart);
            if (next <= current) {
                break;
            }
            DateTime cappedEnd = next > end ? end : next;
            buckets.Add(new TimeBucket {
                Start = Timestamp.FromDateTime(current),
                End = Timestamp.FromDateTime(cappedEnd),
                Label = FormatLabel(current, granularity, configuration.ChartConfig.WeekStart)
            });
            current = next;
        }

        return buckets;
    }

    private static DateTime AlignToGranularityStart(DateTime start, TimeGranularity granularity, DayOfWeek weekStart) {
        return granularity switch {
            TimeGranularity.Yearly => new DateTime(start.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TimeGranularity.Quarterly => GetQuarterStart(start),
            TimeGranularity.Monthly => new DateTime(start.Year, start.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            TimeGranularity.Weekly => AlignToWeekStart(start, weekStart),
            TimeGranularity.Daily => new DateTime(start.Year, start.Month, start.Day, 0, 0, 0, DateTimeKind.Utc),
            _ => start,
        };
    }

    private static DateTime GetNextBucketStart(DateTime start, TimeGranularity granularity, DayOfWeek weekStart) {
        return granularity switch {
            TimeGranularity.Yearly => start.AddYears(1),
            TimeGranularity.Quarterly => start.AddMonths(3),
            TimeGranularity.Monthly => start.AddMonths(1),
            TimeGranularity.Weekly => start.AddDays(7),
            TimeGranularity.Daily => start.AddDays(1),
            _ => start,
        };
    }

    private static DateTime GetQuarterStart(DateTime dateTime) {
        int quarter = (dateTime.Month - 1) / 3;
        int month = quarter * 3 + 1;
        return new DateTime(dateTime.Year, month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime AlignToWeekStart(DateTime dateTime, DayOfWeek weekStart) {
        int diff = ((7 + (dateTime.DayOfWeek - weekStart)) % 7);
        DateTime aligned = dateTime.AddDays(-diff);
        return new DateTime(aligned.Year, aligned.Month, aligned.Day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime ToUtcStart(DateTime dt) {
        if (dt.Kind != DateTimeKind.Utc) {
            dt = dt.ToUniversalTime();
        }
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Millisecond, DateTimeKind.Utc);
    }

    private static string FormatLabel(DateTime bucketStart, TimeGranularity granularity, DayOfWeek weekStart) {
        var culture = CultureInfo.InvariantCulture;
        return granularity switch {
            TimeGranularity.Yearly => bucketStart.ToString("yyyy", culture),
            TimeGranularity.Quarterly => $"Q{((bucketStart.Month - 1) / 3) + 1} {bucketStart:yyyy}",
            TimeGranularity.Monthly => bucketStart.ToString("MMMM yyyy", culture),
            TimeGranularity.Weekly => FormatWeeklyLabel(bucketStart, weekStart, culture),
            TimeGranularity.Daily => bucketStart.ToString("yyyy-MM-dd", culture),
            _ => bucketStart.ToString(culture)
        };
    }

    private static string FormatWeeklyLabel(DateTime bucketStart, DayOfWeek weekStart, CultureInfo culture) {
        Calendar calendar = culture.Calendar;
        int week = calendar.GetWeekOfYear(bucketStart, CalendarWeekRule.FirstFourDayWeek, weekStart);
        return $"Week {week:D2} {bucketStart:yyyy}";
    }

    private class TimeBucket
    {
        public Timestamp Start { get; set; }
        public Timestamp End { get; set; }
        public string Label { get; set; } = "";
    }

    private class SeriesData
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public double?[] Values { get; set; } = [];
    }
}

public class TimeAggregatedBarChartConfig
{
    public TimeAggregatedBarChartMainConfig ChartConfig { get; set; } = new();
    public TimeAggregatedBarChartDataSeries[] DataSeries { get; set; } = [];
}

public sealed class TimeAggregatedBarChartMainConfig
{
    public Timestamp StartTime { get; set; } = Timestamp.FromComponents(2025, 1, 1, 0, 0, 0);
    public Timestamp? EndTime { get; set; } = null;
    public TimeGranularity TimeGranularity { get; set; } = TimeGranularity.Monthly;
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday; // relevant if TimeMode == Weekly
    public bool ShowSumOverBars { get; set; } = true;
    public int SumFractionDigits { get; set; } = 1;

    public bool ShouldSerializeEndTime() => EndTime.HasValue;
}

public enum TimeGranularity
{
    Yearly,
    Monthly,
    Quarterly,
    Weekly,
    Daily,
}

public sealed class TimeAggregatedBarChartDataSeries
{
    public string Name { get; set; } = "";
    public string Color { get; set; } = "";
    public VariableRefUnresolved Variable { get; set; }
    public BarAggregation Aggregation { get; set; } = BarAggregation.Average;
}

public enum BarAggregation
{
    Average, // average of all points in the time granularity interval
    Sum,     // sum of all points in the time granularity interval
    Min,     // minimum of all points in the time granularity interval
    Max,      // maximum of all points in the time granularity interval
    Count,   // count of all points in the time granularity interval    
    First,   // first value in the time granularity interval
    Last,     // last value in the time granularity interval
}
