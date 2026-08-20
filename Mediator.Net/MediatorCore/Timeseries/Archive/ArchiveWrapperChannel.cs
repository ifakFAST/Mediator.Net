// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using NLog;

namespace Ifak.Fast.Mediator.Timeseries.Archive;

public sealed class ArchiveWrapperChannel : Channel
{
    private static readonly Logger Logger = LogManager.GetLogger("ArchiveWrapper");

    //private bool limitWriting = true;

    private readonly ChannelRef channelRef;
    private readonly Channel chRecent;
    private readonly ArchiveChannel chArchive;
    private readonly int archiveOlderThanDays;

    public override ChannelRef Ref => channelRef;

    public Channel RecentChannel => chRecent;
    public ArchiveChannel ArchiveChannel => chArchive;

    public ArchiveWrapperChannel(Channel chRecent, ArchiveChannel chArchive, int archiveOlderThanDays) {
        this.channelRef = chRecent.Ref;
        this.chRecent = chRecent;
        this.chArchive = chArchive;
        this.archiveOlderThanDays = archiveOlderThanDays;
    }

    public override Timestamp? GetOldestTimestamp() {
        return chArchive.GetOldestTimestamp() ?? chRecent.GetOldestTimestamp();
    }

    public override long CountAll() {

        // Promote2DB();

        return chRecent.CountAll() + chArchive.CountAll();
    }

    public override long CountData(Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter) {

        // Promote2DB();

        Timestamp bound = GetBoundForRead();

        if (bound <= startInclusive) {
            return chRecent.CountData(startInclusive, endInclusive, filter);
        }
        else if (endInclusive < bound) {
            return chArchive.CountData(startInclusive, endInclusive, filter);
        }

        long count;

        count = chRecent.CountData(bound, endInclusive, filter);
        count += chArchive.CountData(startInclusive, bound.AddMillis(-1), filter);

        return count;
    }

    public override long DeleteAll() {
        // buffer.Clear();
        long count;
        count = chRecent.DeleteAll();
        count += chArchive.DeleteAll();
        return count;
    }

    public override long DeleteData(Timestamp startInclusive, Timestamp endInclusive) {
        // Promote2DB();
        long count;
        count = chRecent.DeleteData(startInclusive, endInclusive);
        count += chArchive.DeleteData(startInclusive, endInclusive);
        return count;
    }

    public override long DeleteData(Timestamp[] timestamps) {
        // Promote2DB();
        long count;
        count = chRecent.DeleteData(timestamps);
        count += chArchive.DeleteData(timestamps);
        return count;
    }

    public override void ReplaceAll(VTQ[] data) {
        DeleteAll();
        Insert(data);
    }

    public override VTTQ? GetLatest() {
        // Promote2DB();
        return chRecent.GetLatest() ?? chArchive.GetLatest();
    }

    public override VTTQ? GetLatestTimestampDB(Timestamp startInclusive, Timestamp endInclusive) {
        // Promote2DB();
        VTTQ? latestRecent = chRecent.GetLatestTimestampDB(startInclusive, endInclusive);
        VTTQ? latestArchive = chArchive.GetLatestTimestampDB(startInclusive, endInclusive);
        if (latestRecent.HasValue && latestArchive.HasValue) return latestRecent.Value.T_DB > latestArchive.Value.T_DB ? latestRecent : latestArchive;
        if (latestRecent.HasValue) return latestRecent;
        return latestArchive;
    }

    // private readonly List<VTTQ> buffer = new List<VTTQ>();

    public override Func<PrepareContext, string?> PrepareAppend(VTQ data, bool allowOutOfOrder) {

        //if (limitWriting) {

        //    return (ctx) => {

        //        VTTQ? lastItem = buffer.Count > 0 ? buffer.Last() : chSqli.GetLatest();

        //        if (lastItem.HasValue && data.T <= lastItem.Value.T) {
        //            return chSqli.table + ": Timestamp is smaller or equal than last dataset timestamp in channel DB!\n\tLastItem in Database: " + lastItem.Value.ToString() + "\n\t  The Item to Append: " + data.ToString();
        //        }

        //        buffer.Add(VTTQ.Make(data.V, data.T, ctx.TimeDB, data.Q));
        //        return null;
        //    };
        //}
        //else {
            return chRecent.PrepareAppend(data, allowOutOfOrder);
        //}
    }

    //internal void Promote2DB(DbTransaction? transaction = null) {
    //if (buffer.Count == 0) return;
    //if (transaction == null) {
    //    chSqli.UpsertVTTQs(buffer);
    //}
    //else {
    //    chSqli.UpsertVTTQs(transaction, buffer);
    //}
    //buffer.Clear();
    //}

    private Timestamp NormativeBound => Timestamp.Now.TruncateMinutes() - Duration.FromDays(archiveOlderThanDays);

    private Timestamp GetBoundForWrite() {
        Timestamp? recentOldest = chRecent.GetOldestTimestamp();
        if (recentOldest.HasValue) {
            return recentOldest.Value;
        }
        VTTQ? vttqLatest = chArchive.GetLatest();
        if (vttqLatest.HasValue) {
            return Timestamp.MaxOf(NormativeBound, vttqLatest.Value.T.AddMillis(1));
        }
        return NormativeBound;
    }

    private Timestamp GetBoundForRead() {
        Timestamp? recentOldest = chRecent.GetOldestTimestamp();
        if (recentOldest.HasValue) {
            return recentOldest.Value;
        }
        VTTQ? vttqLatest = chArchive.GetLatest();
        if (vttqLatest.HasValue) {
            return vttqLatest.Value.T.AddMillis(1);
        }
        return NormativeBound;
    }

    private void DoInsertUpdate(VTQ[] data, Action<VTQ[]> opRecent, Action<VTQ[]> opArchive) {

        Timestamp bound = GetBoundForWrite();

        //if (data.All(vtq => vtq.T >= bound)) {
        //    opRecent(data);
        //    return;
        //}

        //if (data.All(vtq => vtq.T < bound)) {
        //    opArchive(data);
        //    return;
        //}

        var listArchive = new List<VTQ>(data.Length);
        var listRecent = new List<VTQ>(data.Length);

        for (int i = 0; i < data.Length; i++) {
            VTQ vtq = data[i];
            if (vtq.T < bound) {
                listArchive.Add(vtq);
            }
            else {
                listRecent.Add(vtq);
            }
        }

        if (listRecent.Count > 0) {
            opRecent(listRecent.ToArray());
        }

        if (listArchive.Count > 0) {
            opArchive(listArchive.ToArray());
        }
    }

    public override void Insert(VTQ[] data) {
        DoInsertUpdate(data, chRecent.Insert, chArchive.Insert);
    }

    public override void Update(VTQ[] data) {
        DoInsertUpdate(data, chRecent.Update, chArchive.Update);
    }

    public override void Upsert(VTQ[] data) {
        DoInsertUpdate(data, chRecent.Upsert, chArchive.Upsert);
    }

    public override List<VTTQ> ReadData(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {

        if (maxValues <= 0) {
            return [];
        }

        // Promote2DB();

        Timestamp? boundOpt = chRecent.GetOldestTimestamp();
        if (!boundOpt.HasValue) {
            VTTQ? latestArchive = chArchive.GetLatest();
            if (!latestArchive.HasValue) {
                return [];
            }
            else {
                Timestamp tLast = latestArchive.Value.T;
                endInclusive = Timestamp.MinOf(endInclusive, tLast);
                boundOpt = tLast.AddMillis(1);
            }
        }

        Timestamp bound = boundOpt.Value;

        if (bound <= startInclusive) {
            return chRecent.ReadData(startInclusive, endInclusive, maxValues, bounding, filter);
        }
        else if (endInclusive < bound) {
            return chArchive.ReadData(startInclusive, endInclusive, maxValues, bounding, filter);
        }

        switch (bounding) {

            case BoundingMethod.TakeFirstN: {
                    var data = chArchive.ReadData(startInclusive, bound.AddMillis(-1), maxValues, bounding, filter);
                    if (data.Count >= maxValues) { return data; }
                    var dataRecent = chRecent.ReadData(bound, endInclusive, maxValues - data.Count, bounding, filter);
                    data.AddRange(dataRecent);
                    return data;
                }

            case BoundingMethod.TakeLastN: {
                    var dataRecent = chRecent.ReadData(bound, endInclusive, maxValues, bounding, filter);
                    if (dataRecent.Count >= maxValues) { return dataRecent; }
                    var data = chArchive.ReadData(startInclusive, bound.AddMillis(-1), maxValues - dataRecent.Count, bounding, filter);
                    data.AddRange(dataRecent);
                    return data;
                }

            default:
                throw new NotImplementedException($"Unknown bounding method: {bounding}");
        }
    }

    public override List<VTQ> ReadAggregatedIntervals(Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter) {

        if (intervalBounds == null || intervalBounds.Length < 2) {
            return [];
        }

        // Promote2DB();

        Timestamp bound = GetBoundForRead();
        int numIntervals = intervalBounds.Length - 1;

        // Optimization: all intervals entirely in one channel
        if (bound <= intervalBounds[0]) {
            return chRecent.ReadAggregatedIntervals(intervalBounds, aggregation, filter);
        }
        if (intervalBounds[^1] <= bound) {
            return chArchive.ReadAggregatedIntervals(intervalBounds, aggregation, filter);
        }

        // Mixed: process each interval individually
        var result = new List<VTQ>(numIntervals);
        for (int i = 0; i < numIntervals; i++) {
            Timestamp start = intervalBounds[i];
            Timestamp end = intervalBounds[i + 1];

            VTQ vtq;
            if (bound <= start) {
                // Interval entirely in recent
                List<VTQ> singleResult = chRecent.ReadAggregatedIntervals([start, end], aggregation, filter);
                vtq = singleResult.Count > 0 ? singleResult[0] : VTQ.Make(DataValue.Empty, start, Quality.Good);
            }
            else if (end <= bound) {
                // Interval entirely in archive
                List<VTQ> singleResult = chArchive.ReadAggregatedIntervals([start, end], aggregation, filter);
                vtq = singleResult.Count > 0 ? singleResult[0] : VTQ.Make(DataValue.Empty, start, Quality.Good);
            }
            else {
                // Interval spans both channels - combine native partial aggregations
                vtq = ComputeSpanningAggregation(start, end, bound, aggregation, filter);
            }
            result.Add(vtq);
        }
        return result;
    }

    private VTQ ComputeSpanningAggregation(Timestamp start, Timestamp end, Timestamp bound, Aggregation aggregation, QualityFilter filter) {

        Aggregation partialAggregation = aggregation == Aggregation.Average ? Aggregation.Sum : aggregation;
        DataValue archiveValue = GetAggregateValue(chArchive.ReadAggregatedIntervals([start, bound], partialAggregation, filter));
        DataValue recentValue = GetAggregateValue(chRecent.ReadAggregatedIntervals([bound, end], partialAggregation, filter));

        if (aggregation == Aggregation.First || aggregation == Aggregation.Last) {
            DataValue value = aggregation == Aggregation.First
                ? (archiveValue.NonEmpty ? archiveValue : recentValue)
                : (recentValue.NonEmpty  ? recentValue  : archiveValue);

            return VTQ.Make(value, start, Quality.Good);
        }

        double archiveCount = 0;
        double recentCount = 0;
        if (aggregation == Aggregation.Average) {
            archiveCount = GetAggregateValue(
                chArchive.ReadAggregatedIntervals([start, bound], Aggregation.Count, filter)).AsDoubleNoNaN() ?? 0;
            recentCount = GetAggregateValue(
                chRecent.ReadAggregatedIntervals([bound, end], Aggregation.Count, filter)).AsDoubleNoNaN() ?? 0;
        }

        DataValue combined = CombineNumericAggregations(
            aggregation,
            archiveValue.AsDoubleNoNaN(),
            recentValue.AsDoubleNoNaN(),
            archiveCount,
            recentCount);

        return VTQ.Make(combined, start, Quality.Good);
    }

    private static DataValue GetAggregateValue(List<VTQ> result) {
        return result.Count > 0 ? result[0].V : DataValue.Empty;
    }

    private static DataValue CombineNumericAggregations(
        Aggregation aggregation,
        double? archiveValue,
        double? recentValue,
        double archiveCount,
        double recentCount) {

        if (aggregation == Aggregation.Count) {
            return DataValue.FromDouble((archiveValue ?? 0) + (recentValue ?? 0));
        }

        if (!archiveValue.HasValue && !recentValue.HasValue) {
            return DataValue.Empty;
        }

        double value = aggregation switch {
            Aggregation.Min => archiveValue.HasValue && recentValue.HasValue
                ? Math.Min(archiveValue.Value, recentValue.Value)
                : archiveValue ?? recentValue!.Value,
            Aggregation.Max => archiveValue.HasValue && recentValue.HasValue
                ? Math.Max(archiveValue.Value, recentValue.Value)
                : archiveValue ?? recentValue!.Value,
            Aggregation.Sum => (archiveValue ?? 0) + (recentValue ?? 0),
            Aggregation.Average => ((archiveValue ?? 0) + (recentValue ?? 0)) / (archiveCount + recentCount),
            _ => throw new Exception($"Unknown aggregation method: {aggregation}"),
        };
        return DataValue.FromDouble(value);
    }
}
