using System;
using System.IO;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Timeseries;
using Ifak.Fast.Mediator.Timeseries.Archive;
using Xunit;

namespace MediatorLib_Test.Timeseries.Archive;

public sealed class Test_SQLiteStorage
{
    [Fact]
    public void NegativeDaysRoundTripAndProduceSignedRange() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        storage.WriteDayData(channel, -2, [1, 2]);
        storage.WriteDayData(channel, -1, [3, 4]);

        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        Assert.NotNull(range);
        Assert.Equal(-2, range.Value.dayStart);
        Assert.Equal(-1, range.Value.dayEnd);

        using Stream stream = Assert.IsAssignableFrom<Stream>(storage.ReadDayData(channel, -1));
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        Assert.Equal(new byte[] { 3, 4 }, copy.ToArray());

        storage.DeleteDayData(channel, -2, -1);
        Assert.Null(storage.GetStoredDayNumberRange(channel));
    }

    [Fact]
    public void ArchiveChannelGroupsPreEpochTimestampIntoNegativeDay() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channelRef = ChannelRef.Make("Object", "Value");
        var channel = new ArchiveChannel(channelRef, storage);
        Timestamp timestamp = Timestamp.FromISO8601("1969-12-31T23:59:59.999Z");

        channel.Insert([VTQ.Make(42, timestamp, Quality.Good)]);

        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channelRef);
        Assert.NotNull(range);
        Assert.Equal(-1, range.Value.dayStart);
        Assert.Equal(-1, range.Value.dayEnd);

        var values = channel.ReadData(
            timestamp,
            timestamp,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone);

        Assert.Single(values);
        Assert.Equal(timestamp, values[0].T);
        Assert.Equal(42, values[0].V.GetInt());
    }

    [Fact]
    public void SupportedDayNumberBoundariesRoundTrip() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        storage.WriteDayData(channel, StorageBase.MinDayNumber, [1]);
        storage.WriteDayData(channel, StorageBase.MaxDayNumber, [2]);

        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        Assert.NotNull(range);
        Assert.Equal(StorageBase.MinDayNumber, range.Value.dayStart);
        Assert.Equal(StorageBase.MaxDayNumber, range.Value.dayEnd);
    }

    [Fact]
    public void DayNumbersOutsideSupportedRangeAreRejected() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            storage.WriteDayData(channel, StorageBase.MinDayNumber - 1, [1]));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            storage.ReadDayData(channel, StorageBase.MaxDayNumber + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            storage.DeleteDayData(channel, StorageBase.MinDayNumber - 1, 0));
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder() {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MediatorNet-SQLiteStorage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() {
            Directory.Delete(Path, recursive: true);
        }
    }
}
