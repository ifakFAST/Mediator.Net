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
    public void ArchiveChannelInsertRejectsDuplicateTimestampsBeforeWriting() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        var channel = new ArchiveChannel(ChannelRef.Make("Object", "Value"), storage);
        Timestamp firstDay = Timestamp.FromISO8601("2025-01-01T12:00:00Z");
        Timestamp secondDay = Timestamp.FromISO8601("2025-01-02T12:00:00Z");

        Assert.Throws<ArgumentException>(() => channel.Insert([
            VTQ.Make(1, firstDay, Quality.Good),
            VTQ.Make(2, secondDay, Quality.Good),
            VTQ.Make(3, secondDay, Quality.Good),
        ]));

        Assert.Equal(0, channel.CountAll());
    }

    [Fact]
    public void ArchiveChannelUpdateRejectsDuplicateTimestampsBeforeWriting() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        var channel = new ArchiveChannel(ChannelRef.Make("Object", "Value"), storage);
        Timestamp firstDay = Timestamp.FromISO8601("2025-01-01T12:00:00Z");
        Timestamp secondDay = Timestamp.FromISO8601("2025-01-02T12:00:00Z");
        channel.Insert([
            VTQ.Make(1, firstDay, Quality.Good),
            VTQ.Make(2, secondDay, Quality.Good),
        ]);

        Assert.Throws<ArgumentException>(() => channel.Update([
            VTQ.Make(10, firstDay, Quality.Good),
            VTQ.Make(20, secondDay, Quality.Good),
            VTQ.Make(30, secondDay, Quality.Good),
        ]));

        var values = channel.ReadData(
            firstDay,
            secondDay,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone);
        Assert.Collection(
            values,
            value => Assert.Equal(1, value.V.GetInt()),
            value => Assert.Equal(2, value.V.GetInt()));
    }

    [Fact]
    public void ArchiveChannelUpsertsUseLastValueForDuplicateTimestamps() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        var channel = new ArchiveChannel(ChannelRef.Make("Object", "Value"), storage);
        Timestamp timestamp = Timestamp.FromISO8601("2025-01-01T12:00:00Z");

        channel.Upsert([
            VTQ.Make(1, timestamp, Quality.Good),
            VTQ.Make(2, timestamp, Quality.Good),
        ]);

        VTTQ value = Assert.Single(channel.ReadData(
            timestamp,
            timestamp,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
        Assert.Equal(2, value.V.GetInt());

        channel.UpsertVTTQs([
            VTTQ.Make(DataValue.FromInt(3), timestamp, timestamp, Quality.Good),
            VTTQ.Make(DataValue.FromInt(4), timestamp, timestamp, Quality.Good),
        ]);

        value = Assert.Single(channel.ReadData(
            timestamp,
            timestamp,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
        Assert.Equal(4, value.V.GetInt());
    }

    [Fact]
    public void ArchiveChannelReplaceAllWritesPreparedReplacementDays() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        var channel = new ArchiveChannel(ChannelRef.Make("Object", "Value"), storage);
        Timestamp existingTimestamp = Timestamp.FromISO8601("2025-01-01T12:00:00Z");
        Timestamp firstReplacementTimestamp = Timestamp.FromISO8601("2025-01-02T12:00:00Z");
        Timestamp secondReplacementTimestamp = Timestamp.FromISO8601("2025-01-03T12:00:00Z");
        channel.Insert([VTQ.Make(1, existingTimestamp, Quality.Good)]);

        channel.ReplaceAll([
            VTQ.Make(2, firstReplacementTimestamp, Quality.Good),
            VTQ.Make(3, secondReplacementTimestamp, Quality.Good),
        ]);

        var values = channel.ReadData(
            Timestamp.Empty,
            Timestamp.Max,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone);
        Assert.Collection(
            values,
            value => {
                Assert.Equal(firstReplacementTimestamp, value.T);
                Assert.Equal(2, value.V.GetInt());
            },
            value => {
                Assert.Equal(secondReplacementTimestamp, value.T);
                Assert.Equal(3, value.V.GetInt());
            });
    }

    [Fact]
    public void ArchiveChannelReplaceAllPreservesExistingDataWhenReplacementHasDuplicateTimestamps() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        var channel = new ArchiveChannel(ChannelRef.Make("Object", "Value"), storage);
        Timestamp existingTimestamp = Timestamp.FromISO8601("2025-01-01T12:00:00Z");
        Timestamp replacementTimestamp = Timestamp.FromISO8601("2025-01-02T12:00:00Z");
        channel.Insert([VTQ.Make(1, existingTimestamp, Quality.Good)]);

        Assert.Throws<ArgumentException>(() => channel.ReplaceAll([
            VTQ.Make(2, replacementTimestamp, Quality.Good),
            VTQ.Make(3, replacementTimestamp, Quality.Good),
        ]));

        VTTQ value = Assert.Single(channel.ReadData(
            Timestamp.Empty,
            Timestamp.Max,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
        Assert.Equal(existingTimestamp, value.T);
        Assert.Equal(1, value.V.GetInt());
    }

    [Fact]
    public void ArchiveChannelReplaceAllPreservesExistingDataWhenReplacementTimestampIsUnsupported() {
        using var folder = new TemporaryFolder();
        using var storage = new SQLiteStorage(folder.Path, readOnly: false);
        var channel = new ArchiveChannel(ChannelRef.Make("Object", "Value"), storage);
        Timestamp existingTimestamp = Timestamp.FromISO8601("2025-01-01T12:00:00Z");
        Timestamp unsupportedTimestamp = Timestamp.FromJavaTicks(
            ((long)StorageBase.MaxDayNumber + 1L) * 86_400_000L);
        channel.Insert([VTQ.Make(1, existingTimestamp, Quality.Good)]);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            channel.ReplaceAll([VTQ.Make(2, unsupportedTimestamp, Quality.Good)]));

        VTTQ value = Assert.Single(channel.ReadData(
            Timestamp.Empty,
            Timestamp.Max,
            maxValues: 10,
            Ifak.Fast.Mediator.Timeseries.BoundingMethod.TakeFirstN,
            Ifak.Fast.Mediator.Timeseries.QualityFilter.ExcludeNone));
        Assert.Equal(existingTimestamp, value.T);
        Assert.Equal(1, value.V.GetInt());
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
