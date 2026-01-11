using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;

namespace Ifak.Fast.Mediator.Timeseries.Archive; 

public sealed class Promote2Archive(int limitDays, int checkEveryHours, Func<Channel, ArchiveChannel> makeArchiveChannel) {

    private static readonly Logger Logger = LogManager.GetLogger("DBArchiver");

    private record ChannelPair(
        string Obj, 
        Channel ChannelMain, 
        Timestamp T, 
        ArchiveChannel ChannelArchive);

    private readonly Queue<ChannelPair> channels = [];

    private bool Exhausted => channels.Count == 0;
    private int Count => channels.Count;

    private bool inRunMode = false;
    private Timestamp timeOfLastRunCompleted = Timestamp.Empty;
    private Timestamp tLimitRead = Timestamp.Empty;
    private long countDeleted = 0;

    public bool Busy => inRunMode;

    public void RunStepWhileIdle(TimeSeriesDB db, Func<bool> moreWorkInQueue) {

        try {

            if (!CheckRunMode(out bool started)) {
                return;
            }

            if (started) {
                countDeleted = 0;
                Timestamp NowTrunc = Timestamp.Now.TruncateMinutes();
                tLimitRead = NowTrunc - Duration.FromDays(limitDays);
                ScanChannels(db);
            }

            bool completed = RunLoop(moreWorkInQueue);

            if (completed) {
                inRunMode = false;
                channels.Clear();
                timeOfLastRunCompleted = Timestamp.Now;

                double? freeSpacePercent = db.FreeSpacePercent();
                bool needVacuumFromFreeSpace = freeSpacePercent.HasValue && freeSpacePercent.Value > 20.0;
                bool needVacuum = freeSpacePercent.HasValue ? needVacuumFromFreeSpace : 
                                                              countDeleted > 10000*1440;
                string freeSpaceStr = freeSpacePercent.HasValue ? $" {freeSpacePercent.Value:F1}% unused space within database. " : " ";

                if (needVacuum) {
                    Logger.Info($"Moved {countDeleted} data points to archive.{freeSpaceStr}Vacuum...");
                    var sw = Stopwatch.StartNew();
                    db.Vacuum();
                    sw.Stop();
                    Logger.Info($"Vacuum completed in {sw.ElapsedMilliseconds} ms");
                }
                else {
                    LogDebug($"Moved {countDeleted} data points to archive.{freeSpaceStr}Skip vacuum.");
                }
            }
        }
        catch (Exception exp) {
            LogErr(exp, "Error in Promote2Archive RunStepWhileIdle");
        }
    }

    private bool CheckRunMode(out bool started) {

        if (inRunMode) {
            started = false;
            return true;
        }

        Duration timeSinceLastCompleted = Timestamp.Now - timeOfLastRunCompleted;

        if (timeSinceLastCompleted > Duration.FromHours(checkEveryHours)) {
            started = true;
            inRunMode = true;
            return true;
        }
        else {
            started = false;
            return false;
        }
    }

    private void ScanChannels(TimeSeriesDB dbMain) {

        ChannelInfo[] allChannels = dbMain.GetAllChannels();

        var list = new List<ChannelPair>();
        foreach (var channelInfo in allChannels) {

            string objec = channelInfo.Object;
            string varia = channelInfo.Variable;

            Channel channelMain = dbMain.GetChannel(objec, varia);
            Timestamp? timestampFirst = channelMain.GetOldestTimestamp();

            if (timestampFirst.HasValue) {
                ArchiveChannel channelArchive = makeArchiveChannel(channelMain);
                list.Add(new ChannelPair(objec, channelMain, timestampFirst.Value, channelArchive));
            }
        }

        list = list.OrderBy(x => x.T).ToList(); // OrderBy for stable sort!

        channels.Clear();
        foreach (ChannelPair ch in list) {
            channels.Enqueue(ch);
        }

        if (list.Count > 0)
            Logger.Debug($"State refresh with {list.Count} channels. Old: {list.First().T} New: {list.Last().T} Limit: {tLimitRead}");
        else
            Logger.Debug("State refresh with 0 channels.");
    }

    private bool RunLoop(Func<bool> moreWorkInQueue) {

        var sw = Stopwatch.StartNew();
        var batch = new List<VTTQ>();

        while (!Exhausted && !moreWorkInQueue()) {

            ChannelPair it = Peek();

            if (tLimitRead < it.T) {
                LogDebug($"Exit RunLoop completed because tLimitRead < it.T: {tLimitRead} < {it.T} {it.Obj} state.Count: {Count}");
                return true;
            }

            try {
                var swReadTotal = Stopwatch.StartNew();
                const int ChunkSizeMin = 750;
                const int ChunkSizeMax = 24000;
                int ChunkSize = ChunkSizeMin;

                Timestamp t = it.T;
                batch.Clear();

                var swMainRead = Stopwatch.StartNew();
                var swReadLen = Stopwatch.StartNew();

                while (t <= tLimitRead && batch.Count < 200000 && !moreWorkInQueue()) {

                    swReadLen.Restart();
                    List<VTTQ> chunck = it.ChannelMain.ReadData(t, tLimitRead, ChunkSize, BoundingMethod.TakeFirstN, QualityFilter.ExcludeNone);
                    swReadLen.Stop();

                    batch.AddRange(chunck);
                    if (chunck.Count < ChunkSize) {
                        Pop();
                        // Log($"POP chunckSize {ChunkSize} {it.Obj} state.Count: {Count}");
                        break;
                    }

                    t = chunck.Last().T.AddMillis(1);

                    if (swReadLen.ElapsedMilliseconds < 500) {
                        ChunkSize *= 2;
                        ChunkSize = Math.Min(ChunkSizeMax, ChunkSize);
                    }
                }

                swMainRead.Stop();

                var swArchiveUpsert = Stopwatch.StartNew();
                var swMainDelete = Stopwatch.StartNew();

                if (batch.Count > 0) {

                    swArchiveUpsert.Restart();
                    it.ChannelArchive.UpsertVTTQs(batch);
                    swArchiveUpsert.Stop();

                    swMainDelete.Restart();
                    it.ChannelMain.DeleteData(batch[0].T, batch.Last().T);
                    swMainDelete.Stop();
                    countDeleted += batch.Count;
                }

                LogDebug($"Read {batch.Count} vttqs in {swReadTotal.ElapsedMilliseconds} ms: {it.Obj}: {swMainRead.ElapsedMilliseconds}, {swArchiveUpsert.ElapsedMilliseconds}, {swMainDelete.ElapsedMilliseconds} state.Count: {Count}");

            }
            catch (Exception exp) {
                LogErr(exp, $"Error when moving data of {it.Obj}");
            }
        }

        LogDebug($"Exit RunLoop. Completed: {Exhausted}. Elapsed: {sw.ElapsedMilliseconds} ms, More work in queue: {moreWorkInQueue()}");

        return Exhausted;
    }

    private ChannelPair Peek() {
        if (Exhausted) throw new Exception("Exhausted");
        return channels.Peek();
    }

    private void Pop() {
        if (Exhausted) throw new Exception("Exhausted");
        channels.Dequeue();
    }

    private static void LogDebug(string msg) {
        Logger.Debug(msg);
    }

    private static void LogErr(Exception exp, string msg) {
        Logger.Error(exp, msg);
    }
}
