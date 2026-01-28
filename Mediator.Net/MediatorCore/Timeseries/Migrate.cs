using System;
using System.Linq;
using System.Diagnostics;
using Ifak.Fast.Mediator.Timeseries.Archive;

namespace Ifak.Fast.Mediator.Timeseries
{
    public class Migrate
    {
        public static void CopyData(string srcType, string srcConnectionString, string dstType, string dstConnectionString, int? skipChannelsOlderThanDays = null) {
            try {
                TimeSeriesDB src = OpenDatabase(srcType, srcConnectionString, TimeSeriesDB.Mode.ReadOnly);
                
                if (dstType == "ArchiveSQLite") {
                    // Archive destination - connection string is the base folder path
                    using var storage = new SQLiteStorage(dstConnectionString, readOnly: false);
                    CopyToArchive(source: src, storage: storage, skipChannelsOlderThanDays: skipChannelsOlderThanDays);
                }
                else {
                    TimeSeriesDB dst = OpenDatabase(dstType, dstConnectionString, TimeSeriesDB.Mode.ReadWrite);
                    CopyDatabase(source: src, dest: dst, skipChannelsOlderThanDays: skipChannelsOlderThanDays);
                }
            }
            catch (Exception exp) {
                Console.Error.WriteLine(exp.Message);
                Console.Error.WriteLine(exp.StackTrace);
            }
        }

        private static TimeSeriesDB OpenDatabase(string type, string connectionString, TimeSeriesDB.Mode mode) {
            switch(type) {
                case "SQLite": {
                        var db = new SQLite.SQLiteTimeseriesDB();
                        db.Open(new TimeSeriesDB.OpenParams("SQLite", connectionString, mode));
                        return db;
                    }
                case "Postgres": {
                        var db = new Postgres.PostgresTimeseriesDB();
                        db.Open(new TimeSeriesDB.OpenParams("Postgres", connectionString, mode));
                        return db;
                    }
                default: throw new Exception("Unknown database type: " + type);
            }
        }

        public static void CopyDatabase(TimeSeriesDB source, TimeSeriesDB dest, int? skipChannelsOlderThanDays = null) {

            ChannelInfo[] sourceChannels = source.GetAllChannels();

            Console.WriteLine($"CopyDatabase source db channel count: {sourceChannels.Length}.");

            double Total = sourceChannels.Length;
            double counter = 0;

            foreach (ChannelInfo ch in sourceChannels) {
                counter += 1;
                Channel srcChannel = source.GetChannel(ch.Object, ch.Variable);

                if (ShouldSkipChannel(srcChannel, ch, skipChannelsOlderThanDays)) {
                    continue;
                }

                Channel dstChannel = dest.ExistsChannel(ch.Object, ch.Variable) ?
                    dest.GetChannel(ch.Object, ch.Variable) :
                    dest.CreateChannel(ch);

                var sw = Stopwatch.StartNew();
                long count = CopyChannel(srcChannel, dstChannel);
                sw.Stop();
                string progress = string.Format("{0:0.0}%", 100.0 * counter / Total);
                Console.WriteLine($"Copied {count} entries of channel {ch.Object} in {sw.ElapsedMilliseconds} ms ({progress})");
                
            }
        }

        public static void CopyToArchive(TimeSeriesDB source, SQLiteStorage storage, int? skipChannelsOlderThanDays = null) {

            ChannelInfo[] sourceChannels = source.GetAllChannels();

            Console.WriteLine($"CopyToArchive source db channel count: {sourceChannels.Length}.");

            double Total = sourceChannels.Length;
            double counter = 0;

            foreach (ChannelInfo ch in sourceChannels) {
                counter += 1;
                Channel srcChannel = source.GetChannel(ch.Object, ch.Variable);

                if (ShouldSkipChannel(srcChannel, ch, skipChannelsOlderThanDays)) {
                    continue;
                }

                // Create ArchiveChannel with the shared SQLiteStorage
                var channelRef = ChannelRef.Make(ch.Object, ch.Variable);
                var dstChannel = new ArchiveChannel(channelRef, storage);

                var sw = Stopwatch.StartNew();
                long count = CopyChannel(srcChannel, dstChannel);
                sw.Stop();
                string progress = string.Format("{0:0.0}%", 100.0 * counter / Total);
                Console.WriteLine($"Copied {count} entries of channel {ch.Object} in {sw.ElapsedMilliseconds} ms ({progress})");
            }
        }

        private static long CopyChannel(Channel srcChannel, Channel dstChannel) {
            long count = 0;
            Timestamp t = Timestamp.Empty;
            var sw = Stopwatch.StartNew();
            while (true) {
                sw.Restart();
                var vttqs = srcChannel.ReadData(t, Timestamp.Max, 32000, BoundingMethod.TakeFirstN, QualityFilter.ExcludeNone);
                sw.Stop();
                Console.WriteLine($"Read {vttqs.Count} entries in {sw.ElapsedMilliseconds} ms.");
                if (vttqs.Count == 0) return count;
                count += vttqs.Count;
                var vtqs = vttqs.Select(VTQ.Make).ToArray();
                sw.Restart();
                dstChannel.Upsert(vtqs);
                sw.Stop();
                Console.WriteLine($"Wrote {vtqs.Length} entries in {sw.ElapsedMilliseconds} ms.");
                t = vttqs.Last().T.AddMillis(1);
            }
        }

        private static bool ShouldSkipChannel(Channel srcChannel, ChannelInfo channelInfo, int? skipChannelsOlderThanDays) {

            if (!skipChannelsOlderThanDays.HasValue || skipChannelsOlderThanDays.Value <= 0) {
                return false;
            }

            VTTQ? latest = srcChannel.GetLatest();
            if (!latest.HasValue) {
                Console.WriteLine($"Skipping channel {channelInfo.Object}: no data.");
                return true;
            }

            Duration age = Timestamp.Now - latest.Value.T;
            if (age > Duration.FromDays(skipChannelsOlderThanDays.Value)) {
                Console.WriteLine($"Skipping channel {channelInfo.Object}: latest timestamp {latest.Value.T} older than {skipChannelsOlderThanDays.Value} days.");
                return true;
            }

            return false;
        }
    }
}
