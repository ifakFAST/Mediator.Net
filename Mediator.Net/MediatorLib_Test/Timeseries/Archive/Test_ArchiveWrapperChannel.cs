using System;
using System.IO;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Timeseries;
using Ifak.Fast.Mediator.Timeseries.Archive;
using Ifak.Fast.Mediator.Timeseries.SQLite;
using Xunit;

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
                [start, end], Aggregation.First, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
            VTQ last = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Last, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
            VTQ count = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Count, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
            VTQ sum = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Sum, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
            VTQ average = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Average, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
            VTQ min = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Min, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
            VTQ max = Assert.Single(wrapper.ReadAggregatedIntervals(
                [start, end], Aggregation.Max, Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));

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
