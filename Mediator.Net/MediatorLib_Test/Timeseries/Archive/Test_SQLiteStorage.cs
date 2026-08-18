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

    [Fact]
    public void CachedRangeTracksSuccessfulWrites() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        Assert.Null(storage.GetStoredDayNumberRange(channel));

        storage.WriteDayData(channel, 10, [1]);
        AssertRange(storage, channel, 10, 10);

        storage.WriteDayData(channel, 12, [2]);
        AssertRange(storage, channel, 10, 12);

        storage.WriteDayData(channel, 8, [3]);
        AssertRange(storage, channel, 8, 12);

        storage.WriteDayData(channel, 10, [4]);
        AssertRange(storage, channel, 8, 12);
    }

    [Fact]
    public void WriteDoesNotSeedAnUnknownExistingRange() {
        using var folder = new TemporaryFolder();
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        using (var initialStorage = new SQLiteStorage(folder.Path, readOnly: false)) {
            initialStorage.WriteDayData(channel, 5, [1]);
            initialStorage.WriteDayData(channel, 9, [2]);
        }

        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        storage.WriteDayData(channel, 7, [3]);

        AssertRange(storage, channel, 5, 9);
    }

    [Fact]
    public void CachedRangeIsRecomputedAfterDeletes() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        storage.WriteDayData(channel, 0, [1]);
        storage.WriteDayData(channel, 1, [2]);
        storage.WriteDayData(channel, 2, [3]);
        storage.WriteDayData(channel, 90, [4]);
        storage.WriteDayData(channel, 91, [5]);
        AssertRange(storage, channel, 0, 91);

        storage.DeleteDayData(channel, 1, 1);
        AssertRange(storage, channel, 0, 91);

        storage.DeleteDayData(channel, 0, 0);
        AssertRange(storage, channel, 2, 91);

        storage.DeleteDayData(channel, 2, 90);
        AssertRange(storage, channel, 91, 91);

        storage.DeleteDayData(channel, 91, 91);
        Assert.Null(storage.GetStoredDayNumberRange(channel));
    }

    [Fact]
    public void ReadOnlyInstanceSeesWriterRangeChanges() {
        using var folder = new TemporaryFolder();
        using var writer = new SQLiteStorage(folder.Path, readOnly: false);
        ChannelRef channel = ChannelRef.Make("Object", "Value");

        writer.WriteDayData(channel, 10, [1]);

        using var reader = new SQLiteStorage(folder.Path, readOnly: true);
        AssertRange(reader, channel, 10, 10);

        writer.WriteDayData(channel, 20, [2]);
        AssertRange(reader, channel, 10, 20);

        writer.DeleteDayData(channel, 10, 20);
        Assert.Null(reader.GetStoredDayNumberRange(channel));
    }

    [Fact]
    public void CachedRangesAreIsolatedByChannelAndFolder() {
        using var firstFolder = new TemporaryFolder();
        using var secondFolder = new TemporaryFolder();
        using var firstStorage = new SQLiteStorage(firstFolder.Path, readOnly: false);
        using var secondStorage = new SQLiteStorage(secondFolder.Path, readOnly: false);
        ChannelRef firstChannel = ChannelRef.Make("First", "Value");
        ChannelRef secondChannel = ChannelRef.Make("Second", "Value");

        firstStorage.WriteDayData(firstChannel, 1, [1]);
        firstStorage.WriteDayData(secondChannel, 50, [2]);
        secondStorage.WriteDayData(firstChannel, 100, [3]);
        AssertRange(firstStorage, firstChannel, 1, 1);
        AssertRange(firstStorage, secondChannel, 50, 50);
        AssertRange(secondStorage, firstChannel, 100, 100);

        firstStorage.WriteDayData(firstChannel, 2, [4]);
        firstStorage.DeleteDayData(secondChannel, 50, 50);

        AssertRange(firstStorage, firstChannel, 1, 2);
        Assert.Null(firstStorage.GetStoredDayNumberRange(secondChannel));
        AssertRange(secondStorage, firstChannel, 100, 100);
    }

    private static void AssertRange(SQLiteStorage storage, ChannelRef channel, int expectedStart, int expectedEnd) {
        (int dayStart, int dayEnd)? range = storage.GetStoredDayNumberRange(channel);
        Assert.NotNull(range);
        Assert.Equal(expectedStart, range.Value.dayStart);
        Assert.Equal(expectedEnd, range.Value.dayEnd);
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
