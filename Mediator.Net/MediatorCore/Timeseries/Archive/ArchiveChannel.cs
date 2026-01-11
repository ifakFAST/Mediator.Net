// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Ifak.Fast.Mediator.BinSeri;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Timeseries.Archive;

public sealed class ArchiveChannel(ChannelRef channel, StorageBase storage) : Channel
{
    public override ChannelRef Ref => channel;

    public override Timestamp? GetOldestTimestamp() {
        foreach (int t in EnumDays()) {
            List<VTTQ> allData = ReadDay(t);
            if (allData.Count > 0) {
                return allData.First().T;
            }
        }
        return null;
    }

    public override long CountAll() {
        long total = 0;
        foreach (int t in EnumDays()) {
            List<VTTQ> data = ReadDay(t);
            total += data.Count;
        }
        return total;
    }

    public override long CountData(Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter) {
        var (dayStart, dayEnd) = BoundedDayNumbersFromTimestamps(startInclusive, endInclusive);
        var filterHelper = QualityFilterHelper.Make(filter);
        long total = 0;
        for (int t = dayStart; t <= dayEnd; t++) {
            List<VTTQ> data = ReadDay(t);
            total += data.Count(x => x.T >= startInclusive && x.T <= endInclusive && filterHelper.Include(x.Q));
        }
        return total;
    }

    public override long DeleteAll() {
        long total = CountAll();
        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        if (range != null) {
            storage.DeleteDayData(channel, range.Value.dayStart, range.Value.dayEnd);
        }
        return total;
    }

    public override long DeleteData(Timestamp startInclusive, Timestamp endInclusive) {
        var (dayStart, dayEnd) = BoundedDayNumbersFromTimestamps(startInclusive, endInclusive);
        long totalDeleted = 0;
        for (int t = dayStart; t <= dayEnd; t++) {
            bool middle = t != dayStart && t != dayEnd;
            if (middle) {
                List<VTTQ> data = ReadDay(t);
                totalDeleted += data.Count;
                DeleteDay(t);
            }
            else {
                List<VTTQ> allData = ReadDay(t);
                List<VTTQ> cleanedData = allData.Where(x => x.T < startInclusive || x.T > endInclusive).ToList();
                if (cleanedData.Count < allData.Count) {
                    totalDeleted += (allData.Count - cleanedData.Count);
                    if (cleanedData.Count > 0)
                        WriteDay(t, cleanedData);
                    else
                        DeleteDay(t);
                }
            }
        }

        return totalDeleted;
    }

    public override long DeleteData(Timestamp[] timestamps) {
        HashSet<Timestamp> delete = timestamps.ToHashSet();
        int[] days = timestamps.Select(GetDayNumber).ToHashSet().Order().ToArray();

        long totalDeleted = 0;

        foreach (int t in days) {
            List<VTTQ> allData = ReadDay(t);
            List<VTTQ> cleanedData = allData.Where(x => !delete.Contains(x.T)).ToList();
            if (cleanedData.Count < allData.Count) {
                totalDeleted += (allData.Count - cleanedData.Count);
                if (cleanedData.Count > 0)
                    WriteDay(t, cleanedData);
                else
                    DeleteDay(t);
            }
        }

        return totalDeleted;
    }

    public override VTTQ? GetLatest() {
        foreach (int t in EnumDaysReverse()) {
            List<VTTQ> allData = ReadDay(t);
            if (allData.Count > 0) {
                return allData.Last();
            }
        }
        return null;
    }

    public override VTTQ? GetLatestTimestampDB(Timestamp startInclusive, Timestamp endInclusive) {
        var (dayStart, dayEnd) = BoundedDayNumbersFromTimestamps(startInclusive, endInclusive);
        VTTQ? latest = null;
        for (int t = dayStart; t <= dayEnd; t++) {
            List<VTTQ> allData = ReadDay(t);
            for (int i = 0; i < allData.Count; ++i) {
                VTTQ x = allData[i];
                if (x.T >= startInclusive && x.T <= endInclusive) {
                    if (latest == null || x.T_DB > latest.Value.T_DB) {
                        latest = x;
                    }
                }
            }
        }
        return latest;
    }

    public override void Insert(VTQ[] data) {
        InsertBody(data, (all, newData, timeDB) => JoinInsert(all, newData, timeDB, allowUpdate: false), "Insert");
    }

    public override void Update(VTQ[] data) {
        InsertBody(data, JoinUpdate, "Update");
    }

    public override void Upsert(VTQ[] data) {
        InsertBody(data, (all, newData, timeDB) => JoinInsert(all, newData, timeDB, allowUpdate: true), "Upsert");
    }

    public void UpsertVTTQs(List<VTTQ> data) {
        InsertBodyVTTQ(data, UpsertVTTQ, "UpsertVTTQs");
    }

    public override Func<PrepareContext, string?> PrepareAppend(VTQ data, bool allowOutOfOrder) {

        return (PrepareContext ctx) => {

            //var sw = Stopwatch.StartNew();

            VTTQ? lastItem = GetLatest();

            //sw.Stop();

            //Console.WriteLine($"GetLatest: {sw.ElapsedMilliseconds} ms");

            if (lastItem.HasValue && data.T <= lastItem.Value.T) {
                return $"{channel}: Timestamp is smaller or equal than last dataset timestamp in channel DB!\n\tLastItem in Database: " + lastItem.Value.ToString() + "\n\t  The Item to Append: " + data.ToString();
            }

            try {
                //var sw = Stopwatch.StartNew();
                Insert([data]);
                //Console.WriteLine($"Insert: {sw.ElapsedMilliseconds} ms");
                return null;
            }
            catch (Exception exp) {
                return $"{channel}: {exp.Message}";
            }

        };
    }

    public override List<VTTQ> ReadData(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {
        if (maxValues <= 0) return [];
        return bounding switch {
            BoundingMethod.TakeFirstN => ReadDataFirstN(startInclusive, endInclusive, maxValues, filter),
            BoundingMethod.TakeLastN => ReadDataLastN(startInclusive, endInclusive, maxValues, filter),
            _ => throw new Exception($"Unknown bounding method: {bounding}"),
        };
    }

    public override List<VTQ> ReadAggregatedIntervals(Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter) {

        if (intervalBounds == null || intervalBounds.Length < 2) {
            return [];
        }

        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        int dayMin = range?.dayStart ?? GetDayNumber(Timestamp.Now);
        int dayMax = range?.dayEnd ?? GetDayNumber(Timestamp.Now);

        int numIntervals = intervalBounds.Length - 1;
        var result = new List<VTQ>(numIntervals);
        var values = new List<double>(6000);
        var filterHelper = QualityFilterHelper.Make(filter);

        bool isFirst = aggregation == Aggregation.First;
        bool isLast = aggregation == Aggregation.Last;

        // Optimization: cache locally to prevent re-reading/decompressing the same day many times
        int currentDay = int.MinValue;
        List<VTTQ> currentDayData = null!;

        for (int interval = 0; interval < numIntervals; interval++) {

            Timestamp startInclusive = intervalBounds[interval];
            Timestamp endInclusive = intervalBounds[interval + 1] - Duration.FromMilliseconds(1);

            var (dayStart, dayEnd) = BoundedDayNumbers(dayMin, dayMax, startInclusive, endInclusive);

            values.Clear();
            DataValue firstValue = DataValue.Empty;
            DataValue lastValue = DataValue.Empty;
            bool foundFirstObj = false;

            for (int t = dayStart; t <= dayEnd; t++) {

                // Optimization: For 'First', if we already found a value, we can skip remaining days
                if (isFirst && foundFirstObj) break;

                if (t != currentDay) {
                    currentDayData = ReadDay(t);
                    currentDay = t;
                }
                List<VTTQ> allData = currentDayData;
                int count = allData.Count;
                if (count == 0) continue;

                // Optimization: Boundary checks
                if (allData[0].T > endInclusive) continue;
                if (allData[count - 1].T < startInclusive) continue;

                // Optimization: Binary search for start index
                int startIndex = 0;
                if (allData[0].T < startInclusive) {
                    int low = 1;
                    int high = count - 1;
                    while (low <= high) {
                        int mid = low + (high - low) / 2;
                        if (allData[mid].T < startInclusive)
                            low = mid + 1;
                        else
                            high = mid - 1;
                    }
                    startIndex = low;
                }

                for (int i = startIndex; i < count; ++i) {
                    VTTQ x = allData[i];
                    
                    // Optimization: Stop immediately if we passed the interval end
                    if (x.T > endInclusive) break;

                    if (filterHelper.Include(x.Q)) {
                        double? v = x.V.AsDoubleNoNaN();
                        if (v.HasValue) {
                            if (isFirst) {
                                firstValue = x.V;
                                foundFirstObj = true;
                                break; // Found the 'First', stop processing this day/interval
                            }
                            else if (isLast) {
                                lastValue = x.V;
                            }
                            else {
                                values.Add(v.Value);
                            }
                        }
                    }
                }
            }

            VTQ vtq;
            if (isFirst) {
                vtq = VTQ.Make(firstValue, startInclusive, Quality.Good);
            }
            else if (isLast) {
                vtq = VTQ.Make(lastValue, startInclusive, Quality.Good);
            }
            else {
                vtq = ComputeAggregation(aggregation, values, startInclusive);
            }
            result.Add(vtq);
        }

        return result;
    }

    public override void ReplaceAll(VTQ[] data) {
        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        if (range != null) {
            storage.DeleteDayData(channel, range.Value.dayStart, range.Value.dayEnd);
        }
        Insert(data);
    }

    ////////////////////////////////////////////////

    private static VTQ ComputeAggregation(Aggregation aggregation, List<double> values, Timestamp t) {
        if (values.Count == 0) {
            return aggregation == Aggregation.Count ? VTQ.Make(0, t, Quality.Good) : VTQ.Make(DataValue.Empty, t, Quality.Good);
        }
        double v = aggregation switch {
            Aggregation.Min => values.Min(),
            Aggregation.Max => values.Max(),
            Aggregation.Sum => values.Sum(),
            Aggregation.Average => values.Average(),
            Aggregation.Count => values.Count,
            _ => throw new Exception($"Unknown aggregation method: {aggregation}"),
        };
        return VTQ.Make(v, t, Quality.Good);
    }

    private void InsertBody(VTQ[] data, Func<IReadOnlyList<VTTQ>, IReadOnlyList<VTQ>, Timestamp, List<VTTQ>> joinData, string context) {

        if (data.Length == 0) return;

        Timestamp timeDB = Timestamp.Now;

        var groups = data
            .GroupBy(vtq => GetDayNumber(vtq.T))
            .OrderBy(g => g.Key);

        foreach (var group in groups) {
            int t = group.Key;
            List<VTTQ> allData = ReadDay(t);
            List<VTQ> periodData = group
                .Order()
                .ToList();
            WriteDay(t, joinData(allData, periodData, timeDB));
        }
    }

    private void InsertBodyVTTQ(List<VTTQ> data, Func<List<VTTQ>, List<VTTQ>, List<VTTQ>> joinData, string context) {

        if (data.Count == 0) return;

        var groups = data
            .GroupBy(vtq => GetDayNumber(vtq.T))
            .OrderBy(g => g.Key);

        foreach (var group in groups) {
            int t = group.Key;
            List<VTTQ> allData = ReadDay(t);
            List<VTTQ> periodData = group
                .Order()
                .ToList();
            WriteDay(t, joinData(allData, periodData));
        }
    }

    private static List<VTTQ> UpsertVTTQ(List<VTTQ> allData, List<VTTQ> newData) {

        List<VTTQ> result = new(allData.Count + newData.Count);
        int i = 0;
        int j = 0;

        while (i < allData.Count && j < newData.Count) {
            Timestamp a = allData[i].T;
            Timestamp b = newData[j].T;

            if (a < b) {
                result.Add(allData[i]);
                i++;
            }
            else if (a > b) {
                VTTQ vttq = newData[j];
                result.Add(vttq);
                j++;
            }
            else {
                VTTQ vttq = newData[j];
                result.Add(vttq);
                j++;
                i++;
            }
        }

        while (i < allData.Count) {
            result.Add(allData[i]);
            i++;
        }

        while (j < newData.Count) {
            result.Add(newData[j]);
            j++;
        }

        return result;
    }

    private static List<VTTQ> JoinInsert(IReadOnlyList<VTTQ> allData, IReadOnlyList<VTQ> newData, Timestamp timeDB, bool allowUpdate) {

        List<VTTQ> result = new(allData.Count + newData.Count);
        int i = 0;
        int j = 0;

        while (i < allData.Count && j < newData.Count) {
            Timestamp a = allData[i].T;
            Timestamp b = newData[j].T;

            if (a < b) {
                result.Add(allData[i]);
                i++;
            }
            else if (a > b) {
                VTQ vtq = newData[j];
                result.Add(VTTQ.Make(vtq.V, vtq.T, timeDB, vtq.Q));
                j++;
            }
            else {

                if (!allowUpdate)
                    throw new Exception($"Timestamp {a} already in timeseries");

                VTQ vtq = newData[j];
                result.Add(VTTQ.Make(vtq.V, vtq.T, timeDB, vtq.Q));
                j++;
                i++;
            }
        }

        while (i < allData.Count) {
            result.Add(allData[i]);
            i++;
        }

        while (j < newData.Count) {
            VTQ vtq = newData[j];
            result.Add(VTTQ.Make(vtq.V, vtq.T, timeDB, vtq.Q));
            j++;
        }

        return result;
    }

    private static List<VTTQ> JoinUpdate(IReadOnlyList<VTTQ> allDataOrig, IReadOnlyList<VTQ> updateData, Timestamp timeDB) {

        int i = 0;
        int j = 0;

        List<VTTQ> allData = allDataOrig.ToList();

        while (i < allData.Count && j < updateData.Count) {

            Timestamp a = allData[i].T;
            Timestamp b = updateData[j].T;

            if (a == b) {
                VTQ vtq = updateData[j];
                allData[i] = VTTQ.Make(vtq.V, vtq.T, timeDB, vtq.Q);
                j++;
            }
            i++;
        }

        if (j < updateData.Count) {
            VTQ vtq = updateData[j];
            throw new Exception($"Update of missing timestamp '{vtq.T}' would fail.");
        }

        return allData;
    }

    private List<VTTQ> ReadDataFirstN(Timestamp startInclusive, Timestamp endInclusive, int maxValues, QualityFilter filter) {

        var (dayStart, dayEnd) = BoundedDayNumbersFromTimestamps(startInclusive, endInclusive);

        var res = new List<VTTQ>(Math.Min(maxValues, 12000));
        var filterHelper = QualityFilterHelper.Make(filter);

        for (int t = dayStart; t <= dayEnd; t++) {

            List<VTTQ> allData = ReadDay(t);
            for (int i = 0; i < allData.Count; ++i) {
                VTTQ x = allData[i];
                if (x.T >= startInclusive && x.T <= endInclusive && filterHelper.Include(x.Q)) {
                    res.Add(x);
                    if (res.Count >= maxValues) {
                        return res;
                    }
                }
            }
        }

        return res;
    }

    private List<VTTQ> ReadDataLastN(Timestamp startInclusive, Timestamp endInclusive, int maxValues, QualityFilter filter) {

        var (dayStart, dayEnd) = BoundedDayNumbersFromTimestamps(startInclusive, endInclusive);

        var res = new List<VTTQ>(maxValues);
        var filterHelper = QualityFilterHelper.Make(filter);

        for (int t = dayEnd; t >= dayStart; --t) {

            List<VTTQ> allData = ReadDay(t);
            for (int i = allData.Count - 1; i >= 0; --i) {
                VTTQ x = allData[i];
                if (x.T >= startInclusive && x.T <= endInclusive && filterHelper.Include(x.Q)) {
                    res.Add(x);
                    if (res.Count >= maxValues) {
                        res.Reverse();
                        return res;
                    }
                }
            }
        }

        res.Reverse();
        return res;
    }

    private List<VTTQ> ReadDay(int dayNumber) {
        using Stream? stream = storage.ReadDayData(channel, dayNumber);
        return stream != null ? DecompressVTTQ(stream) : [];
    }

    private void WriteDay(int dayNumber, List<VTTQ> data) {
        byte[] compressedData = CompressVTTQ(data);
        storage.WriteDayData(channel, dayNumber, compressedData);
    }

    private void DeleteDay(int dayNumber) {
        storage.DeleteDayData(channel, dayNumber, dayNumber);
    }

    private (int start, int end) BoundedDayNumbersFromTimestamps(Timestamp startInclusive, Timestamp endInclusive) {

        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);

        if (range == null) {
            int today = GetDayNumber(Timestamp.Now);
            return (today, today);
        }

        int dayMin = range.Value.dayStart;
        int dayMax = range.Value.dayEnd;

        return BoundedDayNumbers(dayMin, dayMax, startInclusive, endInclusive);
    }

    private static (int start, int end) BoundedDayNumbers(int dayMin, int dayMax, Timestamp startInclusive, Timestamp endInclusive) {

        int dayNumStart = GetDayNumber(startInclusive);
        int dayNumEnd = GetDayNumber(endInclusive);

        if (dayNumStart < dayMin) {
            dayNumStart = dayMin;
        }

        if (dayNumEnd > dayMax) {
            dayNumEnd = dayMax;
        }

        return (dayNumStart, dayNumEnd);
    }

    public IEnumerable<int> EnumDays() {
        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        if (range == null) {
            yield break;
        }
        int dayEnd = range.Value.dayEnd;
        for (int day = range.Value.dayStart; day <= dayEnd; day++) {
            yield return day;
        }
    }

    public IEnumerable<int> EnumDaysReverse() {
        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        if (range == null) {
            yield break;
        }
        int dayStart = range.Value.dayStart;
        for (int day = range.Value.dayEnd; day >= dayStart; day--) {
            yield return day;
        }
    }

    /// <summary>
    /// Converts a UTC day start timestamp to a day number (0 = Jan 1, 1970, 1 = Jan 2, 1970, etc.).
    /// </summary>
    /// <param name="dayStartUtc">The UTC day start timestamp.</param>
    /// <returns>Day number (days since Unix epoch).</returns>
    private static int GetDayNumber(Timestamp dayStartUtc) {
        return (int)(dayStartUtc.JavaTicks / MillisecondsPerDay);
    }

    /// <summary>
    /// Converts a day number back to a UTC day start timestamp.
    /// </summary>
    /// <param name="dayNumber">The day number (days since Unix epoch).</param>
    /// <returns>UTC day start timestamp.</returns>
    private static Timestamp DayNumberToTimestamp(int dayNumber) {
        return Timestamp.FromJavaTicks((long)dayNumber * MillisecondsPerDay);
    }

    private const long MillisecondsPerDay = 86400000L;

    private static List<VTTQ> DecompressVTTQ(Stream compressedStream) {
        ArgumentNullException.ThrowIfNull(compressedStream);
        using var mem = MemoryManager.GetMemoryStream("ArchiveStorage_Decompress");
        using (var decompressStream = new GZipStream(compressedStream, CompressionMode.Decompress, leaveOpen: false)) {
            decompressStream.CopyTo(mem);
        }
        // at this point compressedStream is closed/disposed
        mem.Position = 0;
        return VTQ_Serializer.DeserializeAsVTTQ(mem);
    }

    private static byte[] CompressVTTQ(List<VTTQ> data) {

        ArgumentNullException.ThrowIfNull(data);

        if (data.Count == 0) {
            return [];
        }

        // Step 1: Serialize VTTQ list to binary format using VTTQ_Serializer
        using var stream = MemoryManager.GetMemoryStream("ArchiveStorage_Serialize");
        VTQ_Serializer.SerializeVTTQ(stream, data, Common.CurrentBinaryVersion);
        stream.Position = 0;

        // Step 2: Compress binary data with GZip
        using var mem = MemoryManager.GetMemoryStream("ArchiveStorage_Compress");
        using (var compressStream = new GZipStream(mem, CompressionLevel.Optimal, leaveOpen: true)) {
            stream.CopyTo(compressStream);
        }
        mem.Position = 0;
        return mem.ToArray();
    }
}
