// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Ifak.Fast.Mediator.Dashboard
{
    public class TimeRange
    {
        public TimeType Type { get; set; } = TimeType.Last;
        public int LastCount { get; set; } = 7;
        public TimeUnit LastUnit { get; set; } = TimeUnit.Days;
        public string RangeStart { get; set; } = "";
        public string RangeEnd { get; set; } = "";

        public Timestamp GetStart() {
            switch (Type) {
                case TimeType.Last:
                    return Timestamp.Now - DurationFromTimeRange(this);
                case TimeType.Range:
                    return Timestamp.FromISO8601(RangeStart);
                default:
                    throw new Exception("Unknown range type: " + Type);
            }
        }

        public Timestamp GetEnd() {
            switch (Type) {
                case TimeType.Last:
                    return Timestamp.Max;
                case TimeType.Range:
                    return Timestamp.FromISO8601(RangeEnd);
                default:
                    throw new Exception("Unknown range type: " + Type);
            }
        }

        public static Duration DurationFromTimeRange(TimeRange range) {
            return DurationFromTimeUnit(range.LastCount, range.LastUnit);
        }

        public static Duration DurationFromTimeUnit(int count, TimeUnit unit) {
            return unit switch {
                TimeUnit.Minutes => Duration.FromMinutes(count),
                TimeUnit.Hours => Duration.FromHours(count),
                TimeUnit.Days => Duration.FromDays(count),
                TimeUnit.Weeks => Duration.FromDays(7 * count),
                TimeUnit.Months => Duration.FromDays(30 * count),
                TimeUnit.Years => Duration.FromDays(365 * count),
                _ => throw new Exception("Unknown time unit: " + unit),
            };
        }

        public static TimeRange Parse(string str) {
            var parts = str.Split(' ').Select(s => s.Trim()).ToArray();
            if (parts.Length != 3) throw new Exception("Invalid TimeRange: " + str);
            var range = new TimeRange();
            range.Type = (TimeType)Enum.Parse(typeof(TimeType), parts[0], ignoreCase: true);

            static string AsLocalDateTimeString(string date) {
                bool isUTC = date.EndsWith("Z", StringComparison.OrdinalIgnoreCase);
                if (isUTC) {
                    string s = Timestamp.FromISO8601(date).ToDateTime().ToLocalTime().ToString("yyyy-MM-ddTHH:mm:ss");
                    if (s.EndsWith(":00")) {
                        s = s.Substring(0, s.Length - 3);
                    }
                    return s;
                }
                else {
                    return LocalDateTime.FromISO8601(date).ToString();
                }
            }

            if (range.Type == TimeType.Range) {
                range.RangeStart = AsLocalDateTimeString(parts[1]);
                range.RangeEnd = AsLocalDateTimeString(parts[2]);
                return range;
            }
            else {
                range.LastCount = int.Parse(parts[1]);
                range.LastUnit = parts[2].ToLowerInvariant() switch {
                    "min" => TimeUnit.Minutes,
                    "minutes" => TimeUnit.Minutes,
                    "h" => TimeUnit.Hours,
                    "hours" => TimeUnit.Hours,
                    "d" => TimeUnit.Days,
                    "days" => TimeUnit.Days,
                    "weeks" => TimeUnit.Weeks,
                    "months" => TimeUnit.Months,
                    "y" => TimeUnit.Years,
                    "years" => TimeUnit.Years,
                    _ => throw new Exception("Invalid TimeRange: " + str)
                };
                return range;
            }
        }

        public override string ToString() {
            if (Type == TimeType.Last) {
                string unitString = LastUnit switch {
                    TimeUnit.Minutes => LastCount == 1 ? "minute" : "minutes",
                    TimeUnit.Hours => LastCount == 1 ? "hour" : "hours", 
                    TimeUnit.Days => LastCount == 1 ? "day" : "days",
                    TimeUnit.Weeks => "weeks",
                    TimeUnit.Months => "months",
                    TimeUnit.Years => LastCount == 1 ? "year" : "years",
                    _ => LastUnit.ToString().ToLowerInvariant()
                };
                return $"Last {LastCount} {unitString}";
            }
            else if (Type == TimeType.Range) {
                return $"Range {RangeStart} {RangeEnd}";
            }
            else {
                return Type.ToString();
            }
        }
    }

    public enum TimeType
    {
        Last,
        Range
    }

    public enum TimeUnit
    {
        Minutes,
        Hours,
        Days,
        Weeks,
        Months,
        Years
    }
}
