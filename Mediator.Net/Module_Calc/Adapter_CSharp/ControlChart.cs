// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp;

namespace StdLib.ControlCharts;

public record WarnLevel(
    double SigmaLowerZone1Level = 1.0,
    double SigmaUpperZone1Level = 1.0,
    double SigmaLowerWarnLevel  = 2.0,
    double SigmaUpperWarnLevel  = 2.0,
    double SigmaLowerAlarmLevel = 3.0,
    double SigmaUpperAlarmLevel = 3.0
);

public record EvalResult(
    int Status, // -1 = insufficient data, 0 = in control, 1 = warning, 2 = alarm
    int Rule,   // -1 = insufficient data, 0 = no rule violated, 1 = rule 1 violated, 2 = rule 2 violated, etc.
    string Msg  // e.g. "In Control", "Warning: Rule 1 Violated", "Alarm: Rule 2 Violated", etc.
);

public delegate EvalResult EvalRules(
    double[] values,
    ChartType chartType,
    ControlLimits controlLimits
);

public record ChartOutput(
    double? SampleY, // the sample value to plot on the control chart (same scale as CL, UB_A, LB_A, etc.); null when no new data point is available
    string? SampleT, // timestamp of the sample value, null when SampleY is null
    int Status,      // -1 = insufficient data, 0 = in control, 1 = warning, 2 = alarm
    int Rule,        // -1 = insufficient data, 0 = no rule violated, 1 = rule 1 violated, 2 = rule 2 violated, etc.
    string Msg,      // e.g. "In Control", "Warning: Rule 1 Violated", "Alarm: Rule 2 Violated", etc.
    double? CL,      // Center Line
    double? UB_A,    // Upper Bound for Alarm
    double? LB_A,    // Lower Bound for Alarm
    double? UB_W,    // Upper Bound for Warning
    double? LB_W,    // Lower Bound for Warning
    double? UB_Z,    // Upper Bound for Zone 1
    double? LB_Z     // Lower Bound for Zone 1
)
{ 
    public bool IsWarningOrAlarm() => Status > 0;
    public bool IsWarning() => Status == 1;
    public bool IsAlarm() => Status == 2;
    public bool IsInControl() => Status == 0;
    public bool IsInsufficientData() => Status == -1;
}



public class ControlChart( string       name,
                           VariableRef  variable,              // DataItemRef("Measurement")
                           ChartType    chartType,             // TwoSided, UpperTail, LowerTail
                           Interval[]   trainingData,          // e.g. [ Interval("2024-01-01 00:00:00", "2024-01-31 23:59:59") ]
                           string?      resolution = null,     // e.g. "1 min", null means use raw data
                           WarnLevel?   warnLevel = null,      // null means use default values (2 sigma for warn, 3 sigma for alarm)
                           EvalRules?   evaluationRules = null // null means use Western Electric rules
    ) {

    readonly Input x = new("X", unit: "", variable);

    record TrainingParameter(
        VariableRef Variable,
        Interval[]  TrainingIntervals,
        Duration?   Resolution
    );

    record TrainingResult(
        double Mean,
        double StdDev
    );

    readonly TrainingParameter currentTrainingParameter = new(
        variable, 
        trainingData == null || trainingData.Length == 0 ? throw new System.ArgumentException("Training data cannot be null or empty.", nameof(trainingData)) : trainingData, 
        resolution == null ? null : Duration.Parse(resolution));
    
    readonly StateClass<TrainingParameter> trainingParameter = new("TrainingParameter", null);
    readonly StateClass<TrainingResult>    trainingResult    = new("TrainingResult",    null);
    readonly StateTimestamp                lastSampleTime    = new("LastSampleTime",    null);

    readonly OutputClass<ChartOutput> output = new("Output");

    readonly Alarm alarm = new("Alarm", prefixMessageWithName: false);

    readonly WarnLevel effectiveWarnLevel = warnLevel ?? new WarnLevel();
    readonly EvalRules effectiveEvaluationRules = evaluationRules ?? Rules.WesternElectric;

    public ChartOutput? Step(Timestamp t) {

        if (ShouldSkipStep(t)) {
            return null;
        }

        TrainingResult? trainResult = trainingResult.Value;

        if (trainResult == null || TrainingParametersDiffer(trainingParameter.Value, currentTrainingParameter)) { // parameters have changed or no training data yet
            trainingResult.Value = null;
            trainingParameter.Value = currentTrainingParameter;
            trainResult = CalculateTrainingResults(x, currentTrainingParameter);
            trainingResult.Value = trainResult;
        }

        ChartOutput res;
        if (trainResult != null) {
            List<VTQ> vtqs = ReadLast8Values(t);
            double[] values = GetLastConsecutiveNumericValues(vtqs);
            ControlLimits controlLimits = CalculateControlLimits(trainResult, effectiveWarnLevel);
            EvalResult result = values.Length == 0 ? new(-1, -1, "Insufficient data") : effectiveEvaluationRules(values, chartType, controlLimits);

            double? sampleValue = null;
            if (values.Length > 0) {
                Timestamp latestT = vtqs.Last().T;
                Timestamp? prevT = lastSampleTime.ValueOrNull;
                if (!prevT.HasValue || prevT.Value != latestT) {
                    sampleValue = values[0];
                    lastSampleTime.Value = latestT;
                }
            }

            res = new ChartOutput(
                SampleY: sampleValue,
                SampleT: sampleValue.HasValue ? lastSampleTime.Value.ToString() : null,
                Status: result.Status, 
                Rule:   result.Rule, 
                Msg:    result.Msg, 
                CL:     controlLimits.CenterLine,
                UB_A:   controlLimits.UpperAlarm,
                LB_A:   controlLimits.LowerAlarm,
                UB_W:   controlLimits.UpperWarn,
                LB_W:   controlLimits.LowerWarn,
                UB_Z:   controlLimits.UpperZ1Limit,
                LB_Z:   controlLimits.LowerZ1Limit);
        }
        else {
            res = new ChartOutput(
                SampleY: null,
                SampleT: null,
                Status: -1,
                Rule:   -1,
                Msg:    "Insufficient training data",
                CL:     null,
                UB_A:   null,
                LB_A:   null,
                UB_W:   null,
                LB_W:   null,
                UB_Z:   null,
                LB_Z:   null);
        }

        output.Value = res;

        if (res.IsAlarm()) {
            alarm.Set(Level.Alarm, $"{name}: {res.Msg}");
        }
        else if (res.IsWarning()) {
            alarm.Set(Level.Warn, $"{name}: {res.Msg}");
        }
        else if (res.IsInsufficientData()) {
            alarm.Set(Level.Warn, $"{name}: {res.Msg}");
        }
        else if (res.IsInControl()) {
            alarm.Clear();
        }

        return res;
    }

    private bool ShouldSkipStep(Timestamp t) {

        Duration? resolution = currentTrainingParameter.Resolution;
        if (resolution == null) {
            return false;
        }

        Duration interval = resolution.Value;
        if (interval.TotalMilliseconds <= 0) {
            throw new System.ArgumentException("ControlChart resolution must be greater than zero.");
        }

        return t != t.Truncate(interval);
    }

    private static ControlLimits CalculateControlLimits(TrainingResult limits, WarnLevel warnLevel) {

        return new ControlLimits(
            limits.Mean,
            limits.Mean + warnLevel.SigmaUpperAlarmLevel * limits.StdDev,
            limits.Mean - warnLevel.SigmaLowerAlarmLevel * limits.StdDev,
            limits.Mean + warnLevel.SigmaUpperWarnLevel  * limits.StdDev,
            limits.Mean - warnLevel.SigmaLowerWarnLevel  * limits.StdDev,
            limits.Mean + warnLevel.SigmaUpperZone1Level * limits.StdDev,
            limits.Mean - warnLevel.SigmaLowerZone1Level * limits.StdDev
        );
    }

    private List<VTQ> ReadLast8Values(Timestamp t) {

        if (currentTrainingParameter.Resolution == null) {
            // get last 8 values of variable with raw data resolution
            return x.HistorianReadRaw(startInclusive: Timestamp.Empty,
                                      endInclusive: t,
                                      maxValues: 8,
                                      bounding: BoundingMethod.TakeLastN,
                                      filter: QualityFilter.ExcludeNone);
        }
        else { // get last 8 subsampled values of variable with dataResolution

            Duration interval = currentTrainingParameter.Resolution.Value;
            return ReadSubsampled(x, Timestamp.Empty, t, interval, maxIntervals: 8);
        }
    }

    private static double[] GetLastConsecutiveNumericValues(List<VTQ> vtqs) {
        List<double> values = [];
        for (int i = vtqs.Count - 1; i >= 0; i--) {
            VTQ vtq = vtqs[i];
            double? value = vtq.V.AsDoubleNoNaN();
            if (value.HasValue && vtq.NotBad) {
                values.Add(value.Value);
            }
            else {
                break; // stop at first value that is bad, missing or non-numeric, ensuring that we only consider consecutive values up to the current time
            }
        }
        return values.ToArray();
    }

    private static TrainingResult? CalculateTrainingResults(Input x, TrainingParameter parameter) {

        RunningTrainingStats stats = new();

        foreach (Interval trainingInterval in parameter.TrainingIntervals) {

            Timestamp start = trainingInterval.Start;
            Timestamp end   = trainingInterval.End;

            if (parameter.Resolution == null) {
                AddRawTrainingValues(x, start, end, stats);
            }
            else {
                Duration resolution = parameter.Resolution.Value;
                if (resolution.TotalMilliseconds <= 0) {
                    throw new System.ArgumentException("TrainingParameter.Resolution must be greater than zero.");
                }
                foreach (VTQ vtq in ReadSubsampled(x, start, end, resolution)) {
                    AddTrainingValue(vtq, stats);
                }
            }
        }

        return stats.ToTrainingResult();
    }

    private static void AddRawTrainingValues(Input x, Timestamp startInclusive, Timestamp endInclusive, RunningTrainingStats stats) {
        
        const int TrainingReadChunkSize = 5000;

        Timestamp nextStart = startInclusive;

        while (nextStart <= endInclusive) {

            List<VTQ> chunk = x.HistorianReadRaw(
                                    startInclusive: nextStart,
                                    endInclusive: endInclusive,
                                    maxValues: TrainingReadChunkSize,
                                    bounding: BoundingMethod.TakeFirstN,
                                    filter: QualityFilter.ExcludeBad);

            if (chunk.Count == 0) {
                break;
            }

            foreach (VTQ vtq in chunk) {
                AddTrainingValue(vtq, stats);
            }

            if (chunk.Count < TrainingReadChunkSize) {
                break;
            }

            nextStart = chunk[^1].T.AddMillis(1);
        }
    }

    private static void AddTrainingValue(VTQ vtq, RunningTrainingStats stats) {
        double? value = vtq.V.AsDoubleNoNaN();
        if (value.HasValue) {
            stats.Add(value.Value);
        }
    }

    private static List<VTQ> ReadSubsampled(Input x, Timestamp startInclusive, Timestamp endInclusive, Duration interval, int? maxIntervals = null) {

        Timestamp? latestEligible = FindLatestEligibleTimestamp(x, startInclusive, endInclusive);
        if (!latestEligible.HasValue) {
            return [];
        }

        // Snap the newest eligible point to the fixed bucket that contains it.
        Timestamp latestBucketEndExclusive = latestEligible.Value.Truncate(interval).AddDuration(interval);

        if (latestBucketEndExclusive <= startInclusive) {
            return [];
        }

        long intervalCount = (latestBucketEndExclusive - startInclusive).TotalMilliseconds / interval.TotalMilliseconds;
        if (maxIntervals.HasValue) {
            intervalCount = Math.Min(intervalCount, maxIntervals.Value);
        }
        if (intervalCount <= 0) {
            return [];
        }

        Timestamp[] intervalBounds = BuildAnchoredIntervalBounds(latestBucketEndExclusive, interval, checked((int)intervalCount));

        // Clamp last interval bound to endInclusive + 1ms:
        Timestamp lastBound = intervalBounds[^1];
        Timestamp requestedEndExclusive = endInclusive.AddMillis(1);
        if (lastBound > requestedEndExclusive) {
            intervalBounds[^1] = requestedEndExclusive;
        }

        return x.HistorianReadAggregatedIntervals(intervalBounds, Aggregation.First, rawFilter: QualityFilter.ExcludeBad);
    }

    private static Timestamp? FindLatestEligibleTimestamp(Input x, Timestamp startInclusive, Timestamp endInclusive) {

        const int ReadChunkSize = 512;
        Timestamp currentEnd = endInclusive;

        while (currentEnd >= startInclusive) {

            // Match the aggregation filter first, then skip trailing non-numeric values.
            List<VTQ> chunk = x.HistorianReadRaw(startInclusive: startInclusive,
                                                 endInclusive: currentEnd,
                                                 maxValues: ReadChunkSize,
                                                 bounding: BoundingMethod.TakeLastN,
                                                 filter: QualityFilter.ExcludeBad);

            if (chunk.Count == 0) {
                return null;
            }

            for (int i = chunk.Count - 1; i >= 0; --i) {
                VTQ vtq = chunk[i];
                if (vtq.V.AsDoubleNoNaN().HasValue) {
                    return vtq.T;
                }
            }

            Timestamp earliestInChunk = chunk[0].T;
            if (earliestInChunk <= startInclusive) {
                return null;
            }

            currentEnd = earliestInChunk.AddMillis(-1);
        }

        return null;
    }

    private static Timestamp[] BuildAnchoredIntervalBounds(Timestamp endExclusive, Duration interval, int intervalCount) {

        Timestamp[] intervalBounds = new Timestamp[intervalCount + 1];
        for (int i = 0; i <= intervalCount; ++i) {
            intervalBounds[i] = endExclusive - Duration.FromMilliseconds((long)(intervalCount - i) * interval.TotalMilliseconds);
        }
        return intervalBounds;
    }

    private static bool TrainingParametersDiffer(TrainingParameter? left, TrainingParameter right) =>
                left is null
            ||  left.Variable != right.Variable
            ||  left.Resolution != right.Resolution
            || !left.TrainingIntervals.SequenceEqual(right.TrainingIntervals);

    sealed class RunningTrainingStats {

        public long Count { get; private set; }
        public double Mean { get; private set; }
        double SumSquaredDiff { get; set; }

        public void Add(double value) {
            Count++;
            double delta = value - Mean;
            Mean += delta / Count;
            double delta2 = value - Mean;
            SumSquaredDiff += delta * delta2;
        }

        public TrainingResult? ToTrainingResult() {
            if (Count < 2) {
                return null;
            }
            double variance = SumSquaredDiff / (Count - 1);
            return new TrainingResult(Mean, Math.Sqrt(variance));
        }
    }
}

public record Interval {

    public Timestamp Start { get; } // e.g. "2024-01-01 00:00:00"
    public Timestamp End   { get; } // e.g. "2024-01-31 23:59:59"

    public Interval(Timestamp start, Timestamp end) {
        if (end < start) {
            throw new System.ArgumentException($"Invalid interval: end '{end}' is before start '{start}'.");
        }
        Start = start;
        End = end;
    }

    public Interval(string start, string end) {

        if (!Timestamp.TryParse(start, out Timestamp startTs)) {
            throw new System.ArgumentException($"Invalid interval start timestamp: '{start}'.", nameof(start));
        }
        if (!Timestamp.TryParse(end, out Timestamp endTs)) {
            throw new System.ArgumentException($"Invalid interval end timestamp: '{end}'.", nameof(end));
        }
        if (endTs < startTs) {
            throw new System.ArgumentException($"Invalid interval: end '{end}' is before start '{start}'.");
        }

        Start = startTs;
        End = endTs;
    }
}

public enum ChartType
{
    UpperTail,
    LowerTail,
    TwoSided
}

public record ControlLimits(
    double CenterLine,
    double UpperAlarm,
    double LowerAlarm,
    double UpperWarn,
    double LowerWarn,
    double UpperZ1Limit,
    double LowerZ1Limit
);

public static class Rules {

    public static EvalResult WesternElectric(double[] values, ChartType chartType, ControlLimits controlLimits) {

        if (values.Length == 0) {
            throw new System.Exception("No values provided for evaluation.");
        }

        double currentValue = values[0];

        bool checkUpper = chartType != ChartType.LowerTail;
        bool checkLower = chartType != ChartType.UpperTail;

        double centerLine = controlLimits.CenterLine;
        double upperAlarm = controlLimits.UpperAlarm;
        double lowerAlarm = controlLimits.LowerAlarm;
        double upperWarn = controlLimits.UpperWarn;
        double lowerWarn = controlLimits.LowerWarn;
        double upperZ1Limit = controlLimits.UpperZ1Limit;
        double lowerZ1Limit = controlLimits.LowerZ1Limit;

        // Rule 1: one point outside 3-sigma control limits -> alarm
        bool rule1Upper = checkUpper && currentValue > upperAlarm;
        bool rule1Lower = checkLower && currentValue < lowerAlarm;
        if (rule1Upper || rule1Lower) {
            return new EvalResult(2, 1, "Alarm: Rule 1 Violated");
        }

        // Rule 2: two of three recent points beyond 2-sigma on same side -> warning
        if (values.Length >= 3) {
            int upperCount = 0;
            int lowerCount = 0;
            for (int i = 0; i < 3; ++i) {
                double v = values[i];
                if (checkUpper && v > upperWarn) {
                    upperCount++;
                }
                if (checkLower && v < lowerWarn) {
                    lowerCount++;
                }
            }
            if (upperCount >= 2 || lowerCount >= 2) {
                return new EvalResult(1, 2, "Warning: Rule 2 Violated");
            }
        }

        // Rule 3: four of five recent points beyond 1-sigma on same side -> warning
        if (values.Length >= 5) {

            if (checkUpper || checkLower) {
                int upperCount = 0;
                int lowerCount = 0;
                for (int i = 0; i < 5; ++i) {
                    double v = values[i];
                    if (checkUpper && v > upperZ1Limit) {
                        upperCount++;
                    }
                    if (checkLower && v < lowerZ1Limit) {
                        lowerCount++;
                    }
                }

                if (upperCount >= 4 || lowerCount >= 4) {
                    return new EvalResult(1, 3, "Warning: Rule 3 Violated");
                }
            }
        }

        // Rule 4: eight recent points on same side of center line -> warning
        if (values.Length >= 8) {

            bool allUpper = checkUpper;
            bool allLower = checkLower;

            for (int i = 0; i < 8; ++i) {
                double v = values[i];
                if (allUpper && v <= centerLine) {
                    allUpper = false;
                }
                if (allLower && v >= centerLine) {
                    allLower = false;
                }
                if (!allUpper && !allLower) {
                    break;
                }
            }

            if (allUpper || allLower) {
                return new EvalResult(1, 4, "Warning: Rule 4 Violated");
            }
        }

        return new EvalResult(0, 0, "In Control");
    }
}
