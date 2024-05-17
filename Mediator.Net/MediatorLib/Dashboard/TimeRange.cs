// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
                    return Timestamp.FromISO8601(RangeEnd) + Duration.FromDays(1);
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
