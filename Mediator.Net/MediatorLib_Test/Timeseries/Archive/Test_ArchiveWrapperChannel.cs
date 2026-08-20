#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Timeseries;
using Ifak.Fast.Mediator.Timeseries.Archive;
using Ifak.Fast.Mediator.Timeseries.SQLite;
using Xunit;
using TSBoundingMethod = Ifak.Fast.Mediator.Timeseries.BoundingMethod;
using TSQualityFilter = Ifak.Fast.Mediator.Timeseries.QualityFilter;

namespace MediatorLib_Test.Timeseries.Archive;

public sealed class Test_ArchiveWrapperChannel
{
    [Fact]
    public void SpanningAggregationsCombineNativeNumericAggregations() {
        using var folder = new TemporaryFolder();
        string archivePath = Path.Combine(folder.Path, "archive");
        Directory.CreateDirectory(archivePath);
        using var archiveStorage = new SQLiteStorage(archivePath, readOnly: false);
        var recentDb = new SQLiteTimeseriesDB();
        recentDb.Open(new TimeSeriesDB.OpenParams(
            Name: "Recent",
            ConnectionString: $"Data Source={Path.Combine(folder.Path, "recent.sqlite")}",
            ReadWriteMode: TimeSeriesDB.Mode.ReadWrite));

        try {
            ChannelRef channelRef = ChannelRef.Make("Object", "Value");
            Channel recent = recentDb.CreateChannel(new ChannelInfo("Object", "Value", DataType.Float64));
            var archive = new ArchiveChannel(channelRef, archiveStorage);
            var wrapper = new ArchiveWrapperChannel(recent, archive, archiveOlderThanDays: 30);

            Timestamp start = Timestamp.FromISO8601("2025-01-01T00:00:00Z");
            Timestamp bound = start.AddMillis(3);
            Timestamp end = start.AddMillis(5);

            archive.Insert([
                VTQ.Make("not numeric", start, Quality.Good),
                VTQ.Make(10, start.AddMillis(1), Quality.Good),
                VTQ.Make(20, start.AddMillis(2), Quality.Good),
            ]);
            recent.Insert([
                VTQ.Make(100, bound, Quality.Good),
                VTQ.Make("not numeric", start.AddMillis(4), Quality.Good),
            ]);

            VTQ first = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.First, TSQualityFilter.ExcludeNone));
            VTQ last = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Last, TSQualityFilter.ExcludeNone));
            VTQ count = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Count, TSQualityFilter.ExcludeNone));
            VTQ sum = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Sum, TSQualityFilter.ExcludeNone));
            VTQ average = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Average, TSQualityFilter.ExcludeNone));
            VTQ min = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Min, TSQualityFilter.ExcludeNone));
            VTQ max = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Max, TSQualityFilter.ExcludeNone));

            Assert.Equal(10, first.V.GetInt());
            Assert.Equal(100, last.V.GetInt());
            Assert.Equal(3, count.V.GetDouble());
            Assert.Equal(130, sum.V.GetDouble());
            Assert.Equal(130.0 / 3.0, average.V.GetDouble(), precision: 12);
            Assert.Equal(10, min.V.GetDouble());
            Assert.Equal(100, max.V.GetDouble());
            Assert.Equal(start, first.T);
            Assert.Equal(start, last.T);
        }
        finally {
            recentDb.Close();
        }
    }

    [Fact]
    public void MixedIntervalsAreBatchedPerChannel() {
        using var folder = new TemporaryFolder();
        string archivePath = Path.Combine(folder.Path, "archive");
        Directory.CreateDirectory(archivePath);
        using var archiveStorage = new CountingStorage(new SQLiteStorage(archivePath, readOnly: false));
        var recentDb = new SQLiteTimeseriesDB();
        recentDb.Open(new TimeSeriesDB.OpenParams(
            Name: "Recent",
            ConnectionString: $"Data Source={Path.Combine(folder.Path, "recent.sqlite")}",
            ReadWriteMode: TimeSeriesDB.Mode.ReadWrite));

        try {
            ChannelRef channelRef = ChannelRef.Make("Object", "Value");
            Channel innerRecent = recentDb.CreateChannel(new ChannelInfo("Object", "Value", DataType.Float64));
            var recent = new CountingChannel(innerRecent);
            var archive = new ArchiveChannel(channelRef, archiveStorage);
            var wrapper = new ArchiveWrapperChannel(recent, archive, archiveOlderThanDays: 30);

            Timestamp start = Timestamp.FromISO8601("2025-01-01T00:00:00Z");
            archive.Insert([
                VTQ.Make(1, start.AddMillis(1), Quality.Good),
                VTQ.Make(2, start.AddMillis(11), Quality.Good),
                VTQ.Make(3, start.AddMillis(21), Quality.Good),
                VTQ.Make(4, start.AddMillis(31), Quality.Good),
            ]);
            recent.Insert([
                VTQ.Make(5, start.AddMillis(40), Quality.Good),
                VTQ.Make(6, start.AddMillis(51), Quality.Good),
                VTQ.Make(7, start.AddMillis(61), Quality.Good),
                VTQ.Make(8, start.AddMillis(71), Quality.Good),
            ]);

            archiveStorage.ResetReadCount();
            recent.ResetAggregationReadCount();

            Timestamp[] bounds = [
                start,
                start.AddMillis(10),
                start.AddMillis(20),
                start.AddMillis(30),
                start.AddMillis(50),
                start.AddMillis(60),
                start.AddMillis(70),
                start.AddMillis(80),
            ];
            List<VTQ> result = wrapper.ReadAggregatedIntervals(
                bounds, Aggregation.Sum, TSQualityFilter.ExcludeNone);

            double[] expected = [1, 2, 3, 9, 6, 7, 8];
            AssertValues(bounds, expected, result);
            Assert.Equal(2, archiveStorage.ReadCount);
            Assert.Equal(2, recent.AggregationReadCount);

            archiveStorage.ResetReadCount();
            recent.ResetAggregationReadCount();

            Timestamp[] exactBoundaryBounds = [
                start,
                start.AddMillis(10),
                start.AddMillis(20),
                start.AddMillis(30),
                start.AddMillis(40),
                start.AddMillis(50),
                start.AddMillis(60),
                start.AddMillis(70),
                start.AddMillis(80),
            ];
            result = wrapper.ReadAggregatedIntervals(
                exactBoundaryBounds, Aggregation.Sum, TSQualityFilter.ExcludeNone);

            AssertValues(exactBoundaryBounds, [1, 2, 3, 4, 5, 6, 7, 8], result);
            Assert.Equal(1, archiveStorage.ReadCount);
            Assert.Equal(1, recent.AggregationReadCount);
        }
        finally {
            recentDb.Close();
        }
    }

    private static void AssertValues(Timestamp[] bounds, double[] expected, List<VTQ> result) {
        Assert.Equal(expected.Length, result.Count);
        for (int i = 0; i < expected.Length; i++) {
            Assert.Equal(expected[i], result[i].V.GetDouble());
            Assert.Equal(bounds[i], result[i].T);
        }
    }

    private sealed class CountingChannel(Channel inner) : Channel
    {
        public int AggregationReadCount { get; private set; }

        public override ChannelRef Ref => inner.Ref;

        public void ResetAggregationReadCount() {
            AggregationReadCount = 0;
        }

        public override void Update(VTQ[] data) => inner.Update(data);
        public override void Insert(VTQ[] data) => inner.Insert(data);
        public override void Upsert(VTQ[] data) => inner.Upsert(data);
        public override void ReplaceAll(VTQ[] data) => inner.ReplaceAll(data);
        public override Func<PrepareContext, string?> PrepareAppend(VTQ data, bool allowOutOfOrder) =>
            inner.PrepareAppend(data, allowOutOfOrder);
        public override Timestamp? GetOldestTimestamp() => inner.GetOldestTimestamp();
        public override VTTQ? GetLatest() => inner.GetLatest();
        public override VTTQ? GetLatestTimestampDB(Timestamp startInclusive, Timestamp endInclusive) =>
            inner.GetLatestTimestampDB(startInclusive, endInclusive);
        public override List<VTTQ> ReadData(
            Timestamp startInclusive,
            Timestamp endInclusive,
            int maxValues,
            TSBoundingMethod bounding,
            TSQualityFilter filter) =>
            inner.ReadData(startInclusive, endInclusive, maxValues, bounding, filter);
        public override List<VTQ> ReadAggregatedIntervals(
            Timestamp[] intervalBounds,
            Aggregation aggregation,
            TSQualityFilter filter) {
            AggregationReadCount++;
            return inner.ReadAggregatedIntervals(intervalBounds, aggregation, filter);
        }
        public override long DeleteData(Timestamp startInclusive, Timestamp endInclusive) =>
            inner.DeleteData(startInclusive, endInclusive);
        public override long DeleteData(Timestamp[] timestamps) => inner.DeleteData(timestamps);
        public override long DeleteAll() => inner.DeleteAll();
        public override long CountAll() => inner.CountAll();
        public override long CountData(Timestamp startInclusive, Timestamp endInclusive, TSQualityFilter filter) =>
            inner.CountData(startInclusive, endInclusive, filter);
    }

    private sealed class CountingStorage(StorageBase inner) : StorageBase
    {
        public int ReadCount { get; private set; }

        public void ResetReadCount() {
            ReadCount = 0;
        }

        public override (int dayStart, int dayEnd)? GetStoredDayNumberRange(ChannelRef channel) =>
            inner.GetStoredDayNumberRange(channel);
        public override void WriteDayData(ChannelRef channel, int dayNumber, byte[] data) =>
            inner.WriteDayData(channel, dayNumber, data);
        public override Stream? ReadDayData(ChannelRef channel, int dayNumber) {
            ReadCount++;
            return inner.ReadDayData(channel, dayNumber);
        }
        public override void DeleteDayData(
            ChannelRef channel,
            int startDayNumberInclusive,
            int endDayNumberInclusive) =>
            inner.DeleteDayData(channel, startDayNumberInclusive, endDayNumberInclusive);
        public override bool CanCompact() => inner.CanCompact();
        public override void Compact() => inner.Compact();
        public override void Dispose() => inner.Dispose();
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder() {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MediatorNet-ArchiveWrapper-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() {
            Directory.Delete(Path, recursive: true);
        }
    }
}
