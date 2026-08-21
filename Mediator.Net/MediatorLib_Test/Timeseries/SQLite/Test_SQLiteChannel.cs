#nullable enable

using System;
using System.IO;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Timeseries;
using Ifak.Fast.Mediator.Timeseries.SQLite;
using Xunit;

namespace MediatorLib_Test.Timeseries.SQLite;

public sealed class Test_SQLiteChannel
{
    [Fact]
    public void TruncateSwapsInEmptyTableAndChannelRemainsUsable() {
        using var folder = new TemporaryFolder();
        var db = new SQLiteTimeseriesDB();
        db.Open(new TimeSeriesDB.OpenParams(
            Name: "Test",
            ConnectionString: $"Data Source={Path.Combine(folder.Path, "timeseries.sqlite")}",
            ReadWriteMode: TimeSeriesDB.Mode.ReadWrite));

        try {
            Channel channel = db.CreateChannel(new ChannelInfo("Object", "Value", DataType.Float64));
            Channel secondHandle = db.GetChannel("Object", "Value");
            Timestamp start = Timestamp.FromISO8601("2025-01-01T00:00:00Z");

            channel.Insert([
                VTQ.Make(1, start, Quality.Good),
                VTQ.Make(2, start.AddMillis(1), Quality.Good),
            ]);

            // Prepare statements on both handles before replacing the underlying table.
            Assert.Equal(2, channel.CountAll());
            Assert.Equal(2, secondHandle.CountAll());

            channel.Truncate();

            Assert.True(db.ExistsChannel("Object", "Value"));
            Assert.Equal(0, channel.CountAll());
            Assert.Equal(0, secondHandle.CountAll());

            secondHandle.Insert([VTQ.Make(3, start.AddMillis(2), Quality.Good)]);
            Assert.Equal(1, channel.CountAll());

            // Dropping the deferred table must not invalidate either existing channel handle.
            db.Vacuum();
            Assert.Equal(1, secondHandle.CountAll());
        }
        finally {
            db.Close();
        }
    }

    [Fact]
    public void TruncateOfEmptyChannelDoesNotCreateTrash() {
        using var folder = new TemporaryFolder();
        string dbPath = Path.Combine(folder.Path, "timeseries.sqlite");
        var db = new SQLiteTimeseriesDB();
        db.Open(new TimeSeriesDB.OpenParams(
            Name: "Test",
            ConnectionString: $"Data Source={dbPath}",
            ReadWriteMode: TimeSeriesDB.Mode.ReadWrite));

        try {
            Channel channel = db.CreateChannel(new ChannelInfo("Object", "Value", DataType.Float64));

            channel.Truncate();
            channel.Truncate();
            Assert.Equal(0, CountTrashEntries(dbPath));

            channel.Insert([VTQ.Make(1, Timestamp.Now, Quality.Good)]);
            channel.Truncate();
            Assert.Equal(1, CountTrashEntries(dbPath));

            channel.Truncate();
            channel.Truncate();
            Assert.Equal(1, CountTrashEntries(dbPath));
        }
        finally {
            db.Close();
        }
    }

    private static long CountTrashEntries(string dbPath) {
        using var connection = Factory.MakeConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
        connection.Open();
        using var command = Factory.MakeCommand("SELECT COUNT(*) FROM channel_trash", connection);
        return (long)command.ExecuteScalar()!;
    }

    private sealed class TemporaryFolder : IDisposable
    {
        public TemporaryFolder() {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MediatorNet-SQLiteChannel-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() {
            Directory.Delete(Path, recursive: true);
        }
    }
}
