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

[IdentifyWidget(id: "TimeAggregatedTable")]
public class TimeAggregatedTable : WidgetBaseWithConfig<TimeAggregatedTableConfig>
{
    public override string DefaultHeight => "400px";
    public override string DefaultWidth => "100%";

    TimeAggregatedTableConfig configuration => Config;

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

    public async Task<ReqResult> UiReq_SaveConfig(TimeAggregatedTableConfig config) {
        configuration.TableConfig = config.TableConfig ?? new TimeAggregatedTableMainConfig();
        configuration.DataSeries = config.DataSeries ?? [];
        await Context.SaveWidgetConfiguration(configuration);
        return ReqResult.OK();
    }

    public async Task<ReqResult> UiReq_LoadData(TimeRange timeRange, Dictionary<string, string> configVars) {

        Context.SetConfigVariables(configVars);

        List<TimeBucket> buckets = CalculateTimeBuckets(
            0,
            configuration.TableConfig.StartTime,
            configuration.TableConfig.EndTime,
            configuration.TableConfig.TimeGranularity,
            configuration.TableConfig.WeekStart
        );

        var rows = new List<TableRow>();
        string[] seriesNames = configuration.DataSeries?.Select(ds => string.IsNullOrEmpty(ds.Name) ? ds.Variable.Name : ds.Name).ToArray() ?? [];

        if (configuration.DataSeries != null && configuration.DataSeries.Length > 0 && buckets.Count > 0) {

            for (int i = 0; i < buckets.Count; i++) {
                var bucket = buckets[i];
                var values = await GetValuesForBucket(bucket);

                bool canExpand = CanExpandGranularity(configuration.TableConfig.TimeGranularity);

                rows.Add(new TableRow {
                    Values = values,
                    Level = 0,
                    CanExpand = canExpand,
                    StartTime = bucket.Start,
                    EndTime = bucket.End,
                    Granularity = configuration.TableConfig.TimeGranularity
                });
            }
        }

        // Calculate total row (aggregation over entire period)
        double?[]? totalRow = null;
        if (configuration.TableConfig.ShowTotalRow && configuration.DataSeries != null && configuration.DataSeries.Length > 0) {
            totalRow = await CalculateTotalRow();
        }

        var response = new {
            Rows = rows,
            SeriesNames = seriesNames,
            TotalRow = totalRow
        };

        return ReqResult.OK(response);
    }

    public async Task<ReqResult> UiReq_LoadChildData(
        int level,
        string startTime,
        string endTime,
        Dictionary<string, string> configVars) {

        Context.SetConfigVariables(configVars);

        TimeGranularity current = GetGranularityFromLevel(level);
        TimeGranularity childGranularity = GetChildGranularity(current);

        List<TimeBucket> buckets = CalculateTimeBuckets(
            level + 1,
            Timestamp.FromISO8601(startTime),
            Timestamp.FromISO8601(endTime),
            childGranularity,
            configuration.TableConfig.WeekStart
        );

        var rows = new List<TableRow>();

        if (configuration.DataSeries != null && configuration.DataSeries.Length > 0 && buckets.Count > 0) {
            for (int i = 0; i < buckets.Count; i++) {
                var bucket = buckets[i];
                var values = await GetValuesForBucket(bucket);

                bool canExpand = CanExpandGranularity(childGranularity);

                rows.Add(new TableRow {
                    Values = values,
                    Level = level + 1,
                    CanExpand = canExpand,
                    StartTime = bucket.Start,
                    EndTime = bucket.End,
                    Granularity = childGranularity
                });
            }
        }

        var response = new {
            Rows = rows
        };

        return ReqResult.OK(response);
    }

    private TimeGranularity GetGranularityFromLevel(int level) {
        var startGranularity = configuration.TableConfig.TimeGranularity;
        return level switch {
            0 => startGranularity,
            1 => GetChildGranularity(startGranularity),
            2 => GetChildGranularity(GetChildGranularity(startGranularity)),
            _ => throw new Exception("Invalid level")
        };
    }

    private async Task<double?[]> GetValuesForBucket(TimeBucket bucket) {
        var values = new List<double?>();

        if (configuration.DataSeries == null) {
            return [];
        }

        foreach (var dataSeries in configuration.DataSeries) {
            if (dataSeries == null) {
                values.Add(null);
                continue;
            }

            VariableRefUnresolved unresolvedVar = dataSeries.Variable;
            if (string.IsNullOrEmpty(unresolvedVar.Object.ToEncodedString()) || string.IsNullOrEmpty(unresolvedVar.Name)) {
                values.Add(null);
                continue;
            }

            VariableRef variable;
            try {
                variable = Context.ResolveVariableRef(unresolvedVar);
            }
            catch {
                values.Add(null);
                continue;
            }

            Timestamp[] intervalBounds = [bucket.Start, bucket.End];

            Aggregation aggregation = dataSeries.Aggregation switch {
                TableAggregation.Average => Aggregation.Average,
                TableAggregation.Sum => Aggregation.Sum,
                TableAggregation.Count => Aggregation.Count,
                TableAggregation.Min => Aggregation.Min,
                TableAggregation.Max => Aggregation.Max,
                TableAggregation.First => Aggregation.First,
                TableAggregation.Last => Aggregation.Last,
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
                values.Add(null);
                continue;
            }

            double? value = aggregatedData.Count > 0 ? aggregatedData[0].V.AsDouble() : null;
            values.Add(value);
        }

        return values.ToArray();
    }

    private async Task<double?[]> CalculateTotalRow() {
        Timestamp startTs = configuration.TableConfig.StartTime;
        Timestamp endTs = configuration.TableConfig.EndTime ?? Timestamp.Now;

        var bucket = new TimeBucket {
            Start = startTs,
            End = endTs,
            Label = "Total"
        };

        return await GetValuesForBucket(bucket);
    }

    private static List<TimeBucket> CalculateTimeBuckets(int level, Timestamp startTs, Timestamp? endTs, TimeGranularity granularity, DayOfWeek weekStart) {
        Timestamp actualEndTs = endTs ?? Timestamp.Now;

        if (startTs.IsEmpty || actualEndTs.IsEmpty || actualEndTs <= startTs) {
            return [];
        }

        DateTime start = ToUtcStart(startTs.ToDateTime());
        DateTime end = ToUtcStart(actualEndTs.ToDateTime());

        DateTime current = AlignToGranularityStart(start, granularity, weekStart);
        var buckets = new List<TimeBucket>();

        while (current < end) {
            DateTime next = GetNextBucketStart(current, granularity, weekStart);
            if (next <= current) {
                break;
            }
            DateTime cappedEnd = next > end ? end : next;
            buckets.Add(new TimeBucket {
                Start = Timestamp.FromDateTime(current),
                End = Timestamp.FromDateTime(cappedEnd),
                Label = FormatLabel(level, current, granularity, weekStart)
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

    private static string FormatLabel(int level, DateTime bucketStart, TimeGranularity granularity, DayOfWeek weekStart) {
        var culture = CultureInfo.InvariantCulture;
        return granularity switch {
            TimeGranularity.Yearly => bucketStart.ToString("yyyy", culture),
            TimeGranularity.Quarterly => $"Q{((bucketStart.Month - 1) / 3) + 1} {bucketStart:yyyy}",
            TimeGranularity.Monthly => level == 0 ? bucketStart.ToString("MMMM yyyy", culture) : bucketStart.ToString("MMMM", culture),
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

    private static bool CanExpandGranularity(TimeGranularity granularity) {
        return granularity switch {
            TimeGranularity.Daily => false,
            _ => true
        };
    }

    private static TimeGranularity GetChildGranularity(TimeGranularity granularity) {
        return granularity switch {
            TimeGranularity.Yearly => TimeGranularity.Monthly,
            TimeGranularity.Quarterly => TimeGranularity.Monthly,
            TimeGranularity.Monthly => TimeGranularity.Daily,
            TimeGranularity.Weekly => TimeGranularity.Daily,
            _ => throw new Exception("No child granularity available")
        };
    }

    private class TimeBucket
    {
        public Timestamp Start { get; set; }
        public Timestamp End { get; set; }
        public string Label { get; set; } = "";
    }

    private class TableRow
    {
        public double?[] Values { get; set; } = [];
        public int Level { get; set; }
        public bool CanExpand { get; set; }
        public Timestamp StartTime { get; set; }
        public Timestamp EndTime { get; set; }
        public TimeGranularity Granularity { get; set; }
    }
}

public class TimeAggregatedTableConfig
{
    public TimeAggregatedTableMainConfig TableConfig { get; set; } = new();
    public TimeAggregatedTableDataSeries[] DataSeries { get; set; } = [];
}

public sealed class TimeAggregatedTableMainConfig
{
    public Timestamp StartTime { get; set; } = Timestamp.FromComponents(2025, 1, 1, 0, 0, 0);
    public Timestamp? EndTime { get; set; } = null;
    public TimeGranularity TimeGranularity { get; set; } = TimeGranularity.Yearly;
    public DayOfWeek WeekStart { get; set; } = DayOfWeek.Monday;
    public bool ShowTotalRow { get; set; } = true;
    public bool ShowTotalColumn { get; set; } = false;
    public TableAggregation TotalColumnAggregation { get; set; } = TableAggregation.Sum;
    public int FractionDigits { get; set; } = 2;

    public bool ShouldSerializeEndTime() => EndTime.HasValue;
}

public sealed class TimeAggregatedTableDataSeries
{
    public string Name { get; set; } = "";
    public VariableRefUnresolved Variable { get; set; }
    public TableAggregation Aggregation { get; set; } = TableAggregation.Average;
}

public enum TableAggregation
{
    Average,
    Sum,
    Min,
    Max,
    Count,
    First,
    Last,
}
