// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Timeseries;
using Ifak.Fast.Mediator.Timeseries.Archive;
using Ifak.Fast.Mediator.Util;
using NLog;

namespace Ifak.Fast.Mediator;

public class VarHistoyChange
{
    public VariableRef Var { get; set; }
    public Timestamp Start { get; set; }
    public Timestamp End { get; set; }
}

public class HistoryDBWorker
{
    private static readonly Logger logger = LogManager.GetLogger("HistoryDBWorker");

    private readonly string dbName;
    private readonly string dbConnectionString;
    private readonly string[] dbSettings;
    private readonly bool prioritizeReadRequests;
    private readonly bool allowOutOfOrderAppend;
    private readonly Duration? retentionTime;
    private readonly Duration retentionCheckInterval;
    private readonly Func<TimeSeriesDB> dbCreator;
    private readonly Action<IEnumerable<VarHistoyChange>, bool> notifyAppend;
    private readonly int maxConcurrentReads;
    private readonly string aggregationCacheFile;

    private readonly AsyncQueue<WorkItem> queue = new();
    private volatile bool started = false;
    private volatile bool terminated = false;

    // Concurrent read support
    private ReadWorker[]? readWorkers = null;
    private readonly Dictionary<VariableRef, int> channelAffinity = [];
    private int nextWorkerIndex = 0;
    private readonly List<Task> activeReadTasks = [];

    // Aggregation cache for improved read performance
    private HistoryAggregationCache? aggregationCache = null;

    public HistoryDBWorker(
                string dbName,
                string dbConnectionString,
                string[] dbSettings,
                bool prioritizeReadRequests,
                bool allowOutOfOrderAppend,
                Duration? retentionTime,
                Duration retentionCheckInterval,
                Func<TimeSeriesDB> dbCreator,
                Action<IEnumerable<VarHistoyChange>, bool> notifyAppend,
                int maxConcurrentReads,
                AggregationCacheSettings aggregationCache,
                ArchiveSettings archiv) {

        this.dbName = dbName;
        this.dbConnectionString = dbConnectionString;
        this.dbSettings = dbSettings;
        this.prioritizeReadRequests = prioritizeReadRequests;
        this.allowOutOfOrderAppend = allowOutOfOrderAppend;
        this.retentionTime = retentionTime;
        this.retentionCheckInterval = retentionCheckInterval;
        this.dbCreator = dbCreator;
        this.notifyAppend = notifyAppend;
        this.maxConcurrentReads = maxConcurrentReads;
        this.aggregationCacheFile = aggregationCache.Location;

        archiveSupport = string.IsNullOrWhiteSpace(archiv.Location) ? 
                                    null :
                                    new ArchiveSupportInfo(
                                        ArchivePath: archiv.Location,
                                        ArchiveOlderThanDays: archiv.OlderThanDays);

        this.archiver = new Promote2Archive(archiv.OlderThanDays, archiv.CheckEveryHours, MakeArchiveChannel);

        Thread thread = new Thread(TheThread);
        thread.IsBackground = true;
        thread.Start();
    }

    public bool AllowOutOfOrderAppend => allowOutOfOrderAppend;

    private void TheThread() {
        try {
            SingleThreadedAsync.Run(() => Runner());
        }
        catch (Exception exp) {
            logger.Error("HistoryDBWorker terminated unexpectedly: " + exp.Message);
        }
        terminated = true;
    }

    private abstract class WorkItem {
        public abstract bool IsReadRequest { get; }
    }

    private abstract class ReadWorkItem : WorkItem {
        public abstract Task GetTask();
        public abstract VariableRef GetVariableRef();
        public override bool IsReadRequest => true;
    }

    private abstract class WriteWorkItem : WorkItem {
        public override bool IsReadRequest => false;
    }

    private class WI_Start(TaskCompletionSource<bool> promise) : WorkItem
    {
        public readonly TaskCompletionSource<bool> Promise = promise;
        public override bool IsReadRequest => false;
    }

    private class WI_ArchiveProgress : WorkItem
    {
        public override bool IsReadRequest => false;
    }

    private class WI_Terminate(TaskCompletionSource<bool> promise) : WorkItem
    {
        public readonly TaskCompletionSource<bool> Promise = promise;
        public override bool IsReadRequest => false;
    }

    public Task Start() {
        var promise = new TaskCompletionSource<bool>();
        queue.Post(new WI_Start(promise));
        return promise.Task;
    }

    public Task Terminate() {
        if (terminated || !started) return Task.FromResult(true);
        var promise = new TaskCompletionSource<bool>();
        queue.Post(new WI_Terminate(promise));
        return promise.Task;
    }

    public int Append(IList<StoreValue> values) {
        if (!terminated) {
            queue.Post(new WI_BatchAppend(values));
            return queue.Count;
        }
        else {
            return 0;
        }
    }

    private class WI_BatchAppend(IList<StoreValue> values) : WriteWorkItem
    {
        public readonly IList<StoreValue> Values = values;
    }

    public Task<List<VTTQ>> ReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {
        var promise = new TaskCompletionSource<List<VTTQ>>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_ReadRaw(variable, startInclusive, endInclusive, maxValues, bounding, filter, promise));
        }
        return promise.Task;
    }

    private class WI_ReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter, TaskCompletionSource<List<VTTQ>> promise) : ReadWorkItem
    {
        public readonly VariableRef Variable = variable;
        public readonly Timestamp StartInclusive = startInclusive;
        public readonly Timestamp EndInclusive = endInclusive;
        public readonly int MaxValues = maxValues;
        public readonly BoundingMethod Bounding = bounding;
        public readonly QualityFilter Filter = filter;

        public readonly TaskCompletionSource<List<VTTQ>> Promise = promise;
        public override Task GetTask() => Promise.Task;
        public override VariableRef GetVariableRef() => Variable;
    }

    public Task<long> Count(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter) {
        var promise = new TaskCompletionSource<long>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_Count(variable, startInclusive, endInclusive, filter, promise));
        }
        return promise.Task;
    }

    private class WI_Count(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter, TaskCompletionSource<long> promise) : ReadWorkItem
    {
        public readonly VariableRef Variable = variable;
        public readonly Timestamp StartInclusive = startInclusive;
        public readonly Timestamp EndInclusive = endInclusive;
        public readonly QualityFilter Filter = filter;

        public readonly TaskCompletionSource<long> Promise = promise;
        public override Task GetTask() => Promise.Task;
        public override VariableRef GetVariableRef() => Variable;
    }

    public Task<List<VTQ>> ReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter) {
        var promise = new TaskCompletionSource<List<VTQ>>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_ReadAggregatedIntervals(variable, intervalBounds, aggregation, filter, promise));
        }
        return promise.Task;
    }

    private class WI_ReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter, TaskCompletionSource<List<VTQ>> promise) : ReadWorkItem
    {
        public readonly VariableRef Variable = variable;
        public readonly Timestamp[] IntervalBounds = intervalBounds;
        public readonly Aggregation Aggregation = aggregation;
        public readonly QualityFilter Filter = filter;

        public readonly TaskCompletionSource<List<VTQ>> Promise = promise;
        public override Task GetTask() => Promise.Task;
        public override VariableRef GetVariableRef() => Variable;
    }

    public Task<long> DeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
        var promise = new TaskCompletionSource<long>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_DeleteInterval(variable, startInclusive, endInclusive, promise));
        }
        return promise.Task;
    }

    private class WI_DeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, TaskCompletionSource<long> promise) : WriteWorkItem
    {
        public readonly VariableRef Variable = variable;
        public readonly Timestamp StartInclusive = startInclusive;
        public readonly Timestamp EndInclusive = endInclusive;

        public readonly TaskCompletionSource<long> Promise = promise;
    }

    public Task Truncate(VariableRef variable) {
        var promise = new TaskCompletionSource();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_Truncate(variable, promise));
        }
        return promise.Task;
    }

    private class WI_Truncate(VariableRef variable, TaskCompletionSource promise) : WriteWorkItem {
        public readonly VariableRef Variable = variable;

        public readonly TaskCompletionSource Promise = promise;
    }

    public Task<VTTQ?> GetLatestTimestampDb(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
        var promise = new TaskCompletionSource<VTTQ?>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_GetLatestTimestampDb(variable, startInclusive, endInclusive, promise));
        }
        return promise.Task;
    }

    private class WI_GetLatestTimestampDb(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, TaskCompletionSource<VTTQ?> promise) : ReadWorkItem
    {
        public readonly VariableRef Variable = variable;
        public readonly Timestamp StartInclusive = startInclusive;
        public readonly Timestamp EndInclusive = endInclusive;

        public readonly TaskCompletionSource<VTTQ?> Promise = promise;
        public override Task GetTask() => Promise.Task;
        public override VariableRef GetVariableRef() => Variable;
    }

    public Task Modify(VariableRef variable, Variable varDesc, VTQ[] data, ModifyMode mode) {
        var promise = new TaskCompletionSource<bool>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_Modify(variable, varDesc, data, mode, promise));
        }
        return promise.Task;
    }

    private class WI_Modify(VariableRef variable, Variable varDesc, VTQ[] data, ModifyMode mode, TaskCompletionSource<bool> promise) : WriteWorkItem
    {
        public readonly VariableRef Variable = variable;
        public readonly Variable VarDesc = varDesc;
        public readonly VTQ[] Data = data;
        public readonly ModifyMode Mode = mode;

        public readonly TaskCompletionSource<bool> Promise = promise;
    }

    public Task Delete(VariableRef variable) {
        var promise = new TaskCompletionSource<bool>();
        if (CheckPrecondition(promise)) {
            queue.Post(new WI_Delete(variable, promise));
        }
        return promise.Task;
    }

    private class WI_Delete(VariableRef variable, TaskCompletionSource<bool> promise) : WriteWorkItem
    {
        public readonly VariableRef Variable = variable;

        public readonly TaskCompletionSource<bool> Promise = promise;
    }

    #region ReadWorker

    private abstract class ReaderWorkItem { }

    private sealed class RWI_Start(TaskCompletionSource<bool> promise) : ReaderWorkItem
    {
        public readonly TaskCompletionSource<bool> Promise = promise;
    }

    private sealed class RWI_Terminate(TaskCompletionSource<bool> promise) : ReaderWorkItem
    {
        public readonly TaskCompletionSource<bool> Promise = promise;
    }

    private sealed class RWI_ReadRequest(ReadWorkItem original) : ReaderWorkItem
    {
        public readonly ReadWorkItem Original = original;
    }

    private sealed class RWI_RemoveChannel(VariableRef variable) : ReaderWorkItem
    {
        public readonly VariableRef Variable = variable;
    }

    private sealed class ReadWorker
    {
        private readonly int workerIndex;
        private readonly string dbName;
        private readonly string dbConnectionString;
        private readonly string[] dbSettings;
        private readonly Duration? retentionTime;
        private readonly Duration retentionCheckInterval;
        private readonly Func<TimeSeriesDB> dbCreator;
        private readonly HistoryAggregationCache? aggregationCache;
        private readonly ArchiveSupportInfo? archiveSupport;

        private readonly AsyncQueue<ReaderWorkItem> queue = new();
        private readonly Thread thread;
        private readonly Dictionary<VariableRef, Channel> mapChannels = [];

        private TimeSeriesDB? db = null;
        private volatile bool started = false;
        private volatile bool terminated = false;

        private static readonly Logger logger = LogManager.GetLogger("HistoryDBWorker.ReadWorker");

        public ReadWorker(
            int workerIndex,
            string dbName,
            string dbConnectionString,
            string[] dbSettings,
            Duration? retentionTime,
            Duration retentionCheckInterval,
            Func<TimeSeriesDB> dbCreator,
            HistoryAggregationCache? aggregationCache,
            ArchiveSupportInfo? archiveSupport) {

            this.workerIndex = workerIndex;
            this.dbName = dbName;
            this.dbConnectionString = dbConnectionString;
            this.dbSettings = dbSettings;
            this.retentionTime = retentionTime;
            this.retentionCheckInterval = retentionCheckInterval;
            this.dbCreator = dbCreator;
            this.aggregationCache = aggregationCache;
            this.archiveSupport = archiveSupport;

            thread = new Thread(TheThread);
            thread.IsBackground = true;
        }

        public Task Start() {
            var promise = new TaskCompletionSource<bool>();
            queue.Post(new RWI_Start(promise));
            thread.Start();
            return promise.Task;
        }

        public Task Terminate() {
            if (terminated || !started) return Task.FromResult(true);
            var promise = new TaskCompletionSource<bool>();
            queue.Post(new RWI_Terminate(promise));
            return promise.Task;
        }

        public void PostReadRequest(ReadWorkItem item) {
            queue.Post(new RWI_ReadRequest(item));
        }

        public void RemoveChannel(VariableRef variable) {
            queue.Post(new RWI_RemoveChannel(variable));
        }

        private void TheThread() {
            try {
                SingleThreadedAsync.Run(() => Runner());
            }
            catch (Exception exp) {
                logger.Error($"ReadWorker[{workerIndex}] terminated unexpectedly: " + exp.Message);
            }
            terminated = true;
        }

        private StorageBase? archiveStorage = null;

        private async Task Runner() {

            while (true) {

                ReaderWorkItem it = await queue.ReceiveAsync();

                if (it is RWI_ReadRequest readRequest) {
                    HandleReadRequest(readRequest.Original, GetChannelOrNull, aggregationCache);
                }
                else if (it is RWI_RemoveChannel removeChannel) {
                    mapChannels.Remove(removeChannel.Variable);
                }
                else if (it is RWI_Start start) {
                    try {
                        db = dbCreator();
                        var openParams = new TimeSeriesDB.OpenParams(
                            Name: dbName,
                            ConnectionString: dbConnectionString,
                            ReadWriteMode: TimeSeriesDB.Mode.ReadOnly,
                            Settings: dbSettings,
                            RetentionTime: retentionTime,
                            RetentionCheckInterval: retentionCheckInterval);
                        db.Open(openParams);

                        if (archiveSupport != null) {
                            archiveStorage = StorageFactory(archiveSupport, readOnly: true);
                        }

                        start.Promise.SetResult(true);
                        started = true;
                    }
                    catch (Exception e) {
                        start.Promise.SetException(e);
                        return;
                    }
                }
                else if (it is RWI_Terminate terminate) {
                    db?.Close();
                    mapChannels.Clear();
                    archiveStorage?.Dispose();
                    archiveStorage = null;
                    terminate.Promise.SetResult(true);
                    return;
                }
            }
        }

        private TimeSeriesDB GetDbOrThrow() {
            return db ?? throw new Exception("Database is closed");
        }

        private Channel? GetChannelOrNull(VariableRef v) {
            try { return GetChannelOrThrow(v); }
            catch (Exception) { return null; }
        }

        private Channel GetChannelOrThrow(VariableRef v) {
            if (mapChannels.TryGetValue(v, out Channel? ch)) {
                return ch;
            }
            else {
                Channel res = WrapChannelForArchiveSupport(GetDbOrThrow().GetChannel(v.Object.LocalObjectID, v.Name));
                mapChannels[v] = res;
                return res;
            }
        }

        private Channel WrapChannelForArchiveSupport(Channel channel) {
            return DoWrapChannelForArchiveSupport(channel, archiveSupport, archiveStorage);
        }
    }

    #endregion

    private bool CheckPrecondition<T>(TaskCompletionSource<T> promise) {
        if (terminated) {
            promise.SetException(new Exception("HistoryDBWorker terminated"));
            return false;
        }
        if (!started) {
            promise.SetException(new Exception("HistoryDBWorker not yet started"));
            return false;
        }
        return true;
    }

    private bool CheckPrecondition(TaskCompletionSource promise) {
        if (terminated) {
            promise.SetException(new Exception("HistoryDBWorker terminated"));
            return false;
        }
        if (!started) {
            promise.SetException(new Exception("HistoryDBWorker not yet started"));
            return false;
        }
        return true;
    }

    public record ArchiveSupportInfo(
        string ArchivePath,
        int ArchiveOlderThanDays
    );

    private TimeSeriesDB? db = null;
    private readonly ArchiveSupportInfo? archiveSupport;

    private readonly Dictionary<VariableRef, Channel> mapChannels = new Dictionary<VariableRef, Channel>();

    private TimeSeriesDB GetDbOrThrow() {
        return db ?? throw new Exception("Database is closed");
    }

    private StorageBase? archiveStorage = null;
    private readonly Promote2Archive archiver;

    private static StorageBase? StorageFactory(ArchiveSupportInfo archiveSupport, bool readOnly) {
        try {
            string dir = archiveSupport.ArchivePath;
            if (string.IsNullOrWhiteSpace(dir)) throw new Exception("Archive path is empty");
            if (!readOnly) {
                if (!System.IO.Directory.Exists(dir)) {
                    System.IO.Directory.CreateDirectory(dir);
                }
            }
            return new SQLiteStorage(dir, readOnly);
        }
        catch (Exception ex) {
            logger.Error("Failed to create archive storage: " + ex.Message);
            return null;
        }
    }

    private async Task Runner() {

        while (true) {

            WorkItem it = await ReceiveNext();

            if (it is WriteWorkItem write) {

                await WaitForActiveReadsToComplete();

                if (write is WI_BatchAppend batchAppend) {
                    DoAppend(batchAppend);
                }
                else if (write is WI_DeleteInterval deleteInterval) {
                    DoDeleteInterval(deleteInterval);
                }
                else if (write is WI_Truncate truncate) {
                    DoTruncate(truncate);
                }
                else if (write is WI_Modify modify) {
                    DoModify(modify);
                }
                else if (write is WI_Delete delete) {
                    DoDelete(delete);
                }
            }
            else if (it is ReadWorkItem read) {

                if (readWorkers != null) {
                    int idx = GetOrAssignWorkerIndex(read.GetVariableRef());
                    readWorkers[idx].PostReadRequest(read);
                    activeReadTasks.Add(read.GetTask());
                    CleanupCompletedReadTasks();
                }
                else {
                    HandleReadRequest(read, GetChannelOrNull, aggregationCache);
                }
            }
            else if (it is WI_ArchiveProgress archive) {
                if (db != null && !terminated) {
                    bool neededToWait = await WaitForActiveReadsToComplete();
                    if (neededToWait || HasNext()) {
                        //logger.Info("Postponing next archive step");
                        queue.Post(new WI_ArchiveProgress());
                    }
                    else {
                        //logger.Info("Starting archive step");
                        archiver.RunStepWhileIdle(db, HasNext);
                        if (archiver.Busy) {
                            queue.Post(new WI_ArchiveProgress());
                        }
                        else {
                            _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(t => {
                                if (!terminated) queue.Post(new WI_ArchiveProgress());
                            });
                        }
                    }
                }
            }
            else if (it is WI_Start start) {

                try {
                    db = dbCreator();
                    var openParams = new TimeSeriesDB.OpenParams(
                        Name: dbName,
                        ConnectionString: dbConnectionString,
                        ReadWriteMode: TimeSeriesDB.Mode.ReadWrite,
                        Settings: dbSettings,
                        RetentionTime: retentionTime,
                        RetentionCheckInterval: retentionCheckInterval);
                    db.Open(openParams);

                    // Initialize aggregation cache only if explicitly configured
                    if (!string.IsNullOrWhiteSpace(aggregationCacheFile)) {
                        aggregationCache = TryCreateAggregationCache(aggregationCacheFile);
                        logger.Info($"Aggregation cache initialized for timeseries db {dbName} at {aggregationCacheFile}");
                    }
                    else {
                        logger.Info($"No aggregation cache configured for timeseries db {dbName}");
                    }

                    if (archiveSupport != null) {
                        archiveStorage = StorageFactory(archiveSupport, readOnly: false);
                    }

                    // Initialize read workers if concurrent reads are enabled
                    if (maxConcurrentReads > 0) {
                        readWorkers = new ReadWorker[maxConcurrentReads];
                        var startTasks = new Task[maxConcurrentReads];
                        for (int i = 0; i < maxConcurrentReads; i++) {
                            readWorkers[i] = new ReadWorker(i, dbName, dbConnectionString, dbSettings, retentionTime, retentionCheckInterval, dbCreator, aggregationCache, archiveSupport);
                            startTasks[i] = readWorkers[i].Start();
                        }
                        await Task.WhenAll(startTasks);
                    }

                    start.Promise.SetResult(true);
                    started = true;

                    if (archiveSupport != null && archiveStorage != null) {
                        queue.Post(new WI_ArchiveProgress());
                    }
                }
                catch (Exception e) {
                    start.Promise.SetException(e);
                    return;
                }
            }
            else if (it is WI_Terminate terminate) {
                // Terminate read workers first
                if (readWorkers != null) {
                    var terminateTasks = readWorkers.Select(w => w.Terminate()).ToArray();
                    await Task.WhenAll(terminateTasks);
                    readWorkers = null;
                }

                // Dispose aggregation cache
                aggregationCache?.Dispose();
                aggregationCache = null;
                if (archiveStorage != null) {
                    bool compact = archiveStorage.CanCompact();
                    if (compact) {
                        logger.Info("Starting archive storage compaction...");
                        var sw = Stopwatch.StartNew();
                        archiveStorage.Compact();
                        sw.Stop();
                        logger.Info($"Archive storage compaction completed in {sw.Elapsed.TotalSeconds:F1} seconds");
                    }
                    archiveStorage.Dispose();
                    archiveStorage = null;
                }
                db?.Close();
                terminate.Promise.SetResult(true);
                return;
            }
        }
    }

    private static void HandleReadRequest(ReadWorkItem read, Func<VariableRef, Channel?> getChannelOrNull, HistoryAggregationCache? cache) {
        if (read is WI_ReadRaw readRaw) {
            DoReadRaw(readRaw, getChannelOrNull, cache);
        }
        else if (read is WI_Count count) {
            DoCount(count, getChannelOrNull);
        }
        else if (read is WI_ReadAggregatedIntervals readAggregatedIntervals) {
            DoReadAggregatedIntervals(readAggregatedIntervals, getChannelOrNull, cache);
        }
        else if (read is WI_GetLatestTimestampDb getLatestTimestampDb) {
            DoGetLatestTimestampDb(getLatestTimestampDb, getChannelOrNull);
        }
        else {
            throw new Exception("Unknown read work item");
        }
    }

    private static void DoReadRaw(WI_ReadRaw read, Func<VariableRef, Channel?> getChannelOrNull, HistoryAggregationCache? cache) {
        var promise = read.Promise;
        try {
            Channel? ch = getChannelOrNull(read.Variable);
            if (ch == null) {
                promise.SetResult([]);
            }
            else {

                if (read.Bounding == BoundingMethod.TakeFirstN || read.Bounding == BoundingMethod.TakeLastN) {
                    List<VTTQ> res = ch.ReadData(read.StartInclusive, read.EndInclusive, read.MaxValues, Map(read.Bounding), Map(read.Filter));
                    promise.SetResult(res);
                }
                else if (read.Bounding == BoundingMethod.CompressToN) {
                    List<VTTQ> res;
                    if (cache != null) {
                        res = CompressedTimeseriesReader.ReadCompressedWithCache(ch, read.Variable, read.StartInclusive, read.EndInclusive, read.MaxValues, Map(read.Filter), cache);
                    }
                    else {
                        res = CompressedTimeseriesReader.ReadCompressed(ch, read.StartInclusive, read.EndInclusive, read.MaxValues, Map(read.Filter));
                    }
                    promise.SetResult(res);
                }
                else {
                    throw new Exception("Unsupported BoundingMethod: " + read.Bounding);
                }
            }
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private static void DoCount(WI_Count req, Func<VariableRef, Channel?> getChannelOrNull) {
        var promise = req.Promise;
        try {
            Channel? ch = getChannelOrNull(req.Variable);
            if (ch == null) {
                promise.SetResult(0);
            }
            else {

                Timestamp start = req.StartInclusive;
                Timestamp end = req.EndInclusive;
                QualityFilter filter = req.Filter;

                long res;
                if (start == Timestamp.Empty && end == Timestamp.Max && filter == QualityFilter.ExcludeNone) {
                    res = ch.CountAll();
                }
                else {
                    res = ch.CountData(start, end, Map(filter));
                }

                promise.SetResult(res);
            }
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private static void DoReadAggregatedIntervals(WI_ReadAggregatedIntervals req, Func<VariableRef, Channel?> getChannelOrNull, HistoryAggregationCache? cache) {
        var promise = req.Promise;
        try {
            Channel? ch = getChannelOrNull(req.Variable);
            if (ch == null) {
                promise.SetResult([]);
            }
            else {
                Timestamp[] intervalBounds = req.IntervalBounds;
                Aggregation aggregation = req.Aggregation;
                QualityFilter filter = req.Filter;

                // Use cache for supported aggregation types
                if (cache != null && HistoryAggregationCache.IsCacheableAggregation(aggregation)) {
                    List<VTQ> result = ReadAggregatedIntervalsWithCache(ch, req.Variable, intervalBounds, aggregation, filter, cache);
                    promise.SetResult(result);
                }
                else {
                    // Fall back to direct channel read for First/Last or when cache is unavailable
                    List<VTQ> result = ch.ReadAggregatedIntervals(intervalBounds, aggregation, Map(filter));
                    promise.SetResult(result);
                }
            }
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private static List<VTQ> ReadAggregatedIntervalsWithCache(
        Channel channel,
        VariableRef variable,
        Timestamp[] intervalBounds,
        Aggregation aggregation,
        QualityFilter filter,
        HistoryAggregationCache cache) {

        if (intervalBounds.Length < 2) {
            return [];
        }

        var result = new List<VTQ>(intervalBounds.Length - 1);

        for (int i = 0; i < intervalBounds.Length - 1; i++) {
            Timestamp intervalStart = intervalBounds[i];
            Timestamp intervalEnd = intervalBounds[i + 1];

            VTQ vtq = ComputeAggregatedInterval(channel, variable, intervalStart, intervalEnd, aggregation, filter, cache);
            result.Add(vtq);
        }

        return result;
    }

    private static VTQ ComputeAggregatedInterval(
        Channel channel,
        VariableRef variable,
        Timestamp intervalStart,
        Timestamp intervalEnd,
        Aggregation aggregation,
        QualityFilter filter,
        HistoryAggregationCache cache) {

        // Find complete UTC days within this interval
        List<Timestamp> completeDays = HistoryAggregationCache.GetCompleteDaysInRange(intervalStart, intervalEnd);

        if (completeDays.Count == 0) {
            // No complete days - compute directly from channel
            Timestamp[] bounds = [intervalStart, intervalEnd];
            List<VTQ> channelResult = channel.ReadAggregatedIntervals(bounds, aggregation, Map(filter));
            return channelResult.Count > 0 ? channelResult[0] : MakeEmptyVTQ(intervalStart, aggregation);
        }

        // Collect cached values and identify days that need computation
        var cachedDays = new List<(Timestamp Day, double Value, long? Count)>();
        var uncachedDays = new List<Timestamp>();

        Timeseries.QualityFilter tsFilter = Map(filter);

        foreach (Timestamp day in completeDays) {
            if (cache.TryGet(variable, aggregation, filter, day, out double? value, out long? count)) {
                if (value.HasValue) {
                    cachedDays.Add((day, value.Value, count));
                }
            }
            else {
                uncachedDays.Add(day);
            }
        }

        // Compute and cache uncached days
        foreach (Timestamp day in uncachedDays) {

            Timestamp dayEnd = day.AddDays(1);
            var (value, count) = ReadAggregatedValue(channel, day, dayEnd, aggregation, tsFilter);

            if (value.HasValue) {
                cachedDays.Add((day, value.Value, count));
                cache.Set(variable, aggregation, filter, day, value.Value, count);
            }
            else {
                // Empty day
                cache.Set(variable, aggregation, filter, day, null, null);
            }
        }

        // Handle partial intervals at start and end
        Timestamp firstCompleteDayStart = completeDays[0];
        Timestamp lastCompleteDayEnd = completeDays[completeDays.Count - 1].AddDays(1);

        var partialResults = new List<(Timestamp IntervalStart, Timestamp IntervalEnd, double Value, long? Count)>();

        // Partial interval at start (intervalStart to firstCompleteDay)
        if (intervalStart < firstCompleteDayStart) {
            var (value, count) = ReadAggregatedValue(channel, intervalStart, firstCompleteDayStart, aggregation, tsFilter);
            if (value.HasValue) {
                partialResults.Add((intervalStart, firstCompleteDayStart, value.Value, count));
            }
        }

        // Partial interval at end (lastCompleteDayEnd to intervalEnd)
        if (lastCompleteDayEnd < intervalEnd) {
            var (value, count) = ReadAggregatedValue(channel, lastCompleteDayEnd, intervalEnd, aggregation, tsFilter);
            if (value.HasValue) {
                partialResults.Add((lastCompleteDayEnd, intervalEnd, value.Value, count));
            }
        }

        // Combine all results
        return CombineAggregatedResults(intervalStart, aggregation, cachedDays, partialResults);
    }


    /// <summary>
    /// Reads an aggregated value from the channel for the given bounds.
    /// For Average aggregation, also reads the count to enable proper weighted combination.
    /// </summary>
    private static (double? Value, long? Count) ReadAggregatedValue(
        Channel channel,
        Timestamp tStart,
        Timestamp tEnd,
        Aggregation aggregation,
        Timeseries.QualityFilter tsFilter) {

        Timestamp[] bounds = [tStart, tEnd];
        List<VTQ> result = channel.ReadAggregatedIntervals(bounds, aggregation, tsFilter);
        double? numericValue = result.Count > 0 ? result[0].V.AsDouble() : null;

        if (!numericValue.HasValue) {
            return (null, null);
        }

        long? count = null;
        if (aggregation == Aggregation.Average) {
            List<VTQ> countResult = channel.ReadAggregatedIntervals(bounds, Aggregation.Count, tsFilter);
            if (countResult.Count > 0) {
                double? countVal = countResult[0].V.AsDouble();
                count = countVal.HasValue ? (long)countVal.Value : 0;
            }
        }

        return (numericValue.Value, count);
    }

    private static VTQ CombineAggregatedResults(
        Timestamp timestamp,
        Aggregation aggregation,
        List<(Timestamp Day, double Value, long? Count)> cachedDays,
        List<(Timestamp IntervalStart, Timestamp IntervalEnd, double Value, long? Count)> partialResults) {

        // The aggregations Average, Min, Max, Sum, Count can be combined from sub-intervals regardless of their time spans and order
        // => We can simply collect all values and compute the final aggregation

        List<(double Value, long? Count)> allValues = [];
        allValues.AddRange(cachedDays.Select(d => (d.Value, d.Count)));
        allValues.AddRange(partialResults.Select(d => (d.Value, d.Count)));

        if (allValues.Count == 0) {
            return MakeEmptyVTQ(timestamp, aggregation);
        }

        double resultValue = aggregation switch {
            Aggregation.Average => ComputeWeightedAverage(allValues) ?? 0.0,
            Aggregation.Min     => allValues.Min(v => v.Value),
            Aggregation.Max     => allValues.Max(v => v.Value),
            Aggregation.Sum     => allValues.Sum(v => v.Value),
            Aggregation.Count   => allValues.Sum(v => v.Value),
            _ => throw new Exception("Unsupported aggregation: " + aggregation)
        };

        return new VTQ(timestamp, Quality.Good, DataValue.FromDouble(resultValue));
    }

    private static double? ComputeWeightedAverage(List<(double Value, long? Count)> values) {

        long totalCount = 0;
        double weightedSum = 0;

        foreach (var (value, count) in values) {
            if (count is null) { // should not happen for average
                continue;
            }
            long c = count.Value;
            totalCount += c;
            weightedSum += value * c;
        }

        return totalCount > 0 ? weightedSum / totalCount : null;
    }

    private static VTQ MakeEmptyVTQ(Timestamp timestamp, Aggregation aggregation) {
        DataValue value = aggregation == Aggregation.Count
            ? DataValue.FromDouble(0)
            : DataValue.Empty;
        return new VTQ(timestamp, Quality.Good, value);
    }

    private static void DoGetLatestTimestampDb(WI_GetLatestTimestampDb req, Func<VariableRef, Channel?> getChannelOrNull) {
        var promise = req.Promise;
        try {
            Channel? ch = getChannelOrNull(req.Variable);
            if (ch == null) {
                promise.SetResult(null);
            }
            else {
                VTTQ? res = ch.GetLatestTimestampDB(req.StartInclusive, req.EndInclusive);
                promise.SetResult(res);
            }
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private void DoDeleteInterval(WI_DeleteInterval req) {
        var promise = req.Promise;
        try {
            Channel? ch = GetChannelOrNull(req.Variable);
            if (ch == null) {
                promise.SetResult(0);
            }
            else {

                Timestamp start = req.StartInclusive;
                Timestamp end = req.EndInclusive;

                long res;
                if (start == Timestamp.Empty && end == Timestamp.Max) {
                    res = ch.DeleteAll();
                    // Invalidate entire cache for this variable
                    aggregationCache?.InvalidateAll(req.Variable);
                }
                else {
                    res = ch.DeleteData(req.StartInclusive, req.EndInclusive);
                    // Invalidate cache for affected days
                    aggregationCache?.InvalidateDays(req.Variable, start, end);
                }

                promise.SetResult(res);
            }
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private void DoTruncate(WI_Truncate req) {
        var promise = req.Promise;
        try {
            Channel? ch = GetChannelOrNull(req.Variable);
            if (ch == null) {
                promise.SetResult();
            }
            else {
                ch.Truncate();
                // Invalidate entire cache for this variable
                aggregationCache?.InvalidateAll(req.Variable);
                promise.SetResult();
            }
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private void DoModify(WI_Modify req) {
        var promise = req.Promise;
        try {
            Channel ch = GetOrCreateChannelOrThrow(req.Variable, req.VarDesc);

            switch (req.Mode) {
                case ModifyMode.Insert:
                    ch.Insert(req.Data);
                    break;

                case ModifyMode.Update:
                    ch.Update(req.Data);
                    break;

                case ModifyMode.Upsert:
                    ch.Upsert(req.Data);
                    break;

                case ModifyMode.ReplaceAll:
                    ch.ReplaceAll(req.Data);
                    // ReplaceAll affects entire variable - invalidate all
                    aggregationCache?.InvalidateAll(req.Variable);
                    break;

                case ModifyMode.Delete:
                    ch.DeleteData(req.Data.Select(x => x.T).ToArray());
                    break;

                default:
                    throw new Exception("Unknown modify mode: " + req.Mode);
            }

            // Invalidate cache for affected time range (except ReplaceAll which is handled above)
            if (req.Mode != ModifyMode.ReplaceAll && req.Data.Length > 0 && aggregationCache != null) {
                Timestamp minT = req.Data.Min(x => x.T);
                Timestamp maxT = req.Data.Max(x => x.T);
                aggregationCache.InvalidateDays(req.Variable, minT, maxT);
            }

            promise.SetResult(true);
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private void DoDelete(WI_Delete req) {
        var promise = req.Promise;
        try {
            VariableRef v = req.Variable;
            // Remove any cached aggregations/compressed values for the deleted variable
            aggregationCache?.InvalidateAll(req.Variable);

            // Remove archived data if archive support is enabled
            if (archiveSupport != null && archiveStorage != null) {
                var chRef = ChannelRef.Make(v.Object.LocalObjectID, v.Name);
                (int dayStart, int dayEnd)? range = archiveStorage.GetStoredDayNumberRange(chRef);
                if (range != null) {
                    archiveStorage.DeleteDayData(chRef, range.Value.dayStart, range.Value.dayEnd);
                }
            }
            
            archiver.RemoveChannel(v.Object.LocalObjectID, v.Name);

            GetDbOrThrow().RemoveChannel(v.Object.LocalObjectID, v.Name);

            // Clear cached channels and affinity
            mapChannels.Remove(v);
            channelAffinity.Remove(v);
            if (readWorkers != null) {
                foreach (var w in readWorkers) {
                    w.RemoveChannel(v);
                }
            }
            promise.SetResult(true);
        }
        catch (Exception exp) {
            promise.SetException(exp);
        }
    }

    private static Timeseries.BoundingMethod Map(BoundingMethod b) {
        return b switch {
            BoundingMethod.TakeFirstN => Timeseries.BoundingMethod.TakeFirstN,
            BoundingMethod.TakeLastN => Timeseries.BoundingMethod.TakeLastN,
            BoundingMethod.CompressToN => throw new Exception("Unknown bounding"),
            _ => throw new Exception("Unknown bounding")
        };
    }

    private static Timeseries.QualityFilter Map(QualityFilter f) {
        return f switch {
            QualityFilter.ExcludeNone => Timeseries.QualityFilter.ExcludeNone,
            QualityFilter.ExcludeBad => Timeseries.QualityFilter.ExcludeBad,
            QualityFilter.ExcludeNonGood => Timeseries.QualityFilter.ExcludeNonGood,
            _ => throw new Exception("Unknown quality filter")
        };
    }

    private void DoAppend(WI_BatchAppend append) {

        try {

            var nonExisting = new List<StoreValue>();
            var set = new Dictionary<VariableRef, VarHistoyChange>();

            foreach (StoreValue sv in append.Values) {
                VariableRef vr = sv.Value.Variable;
                Timestamp newT = sv.Value.Value.T;
                if (!set.ContainsKey(vr)) {
                    set[vr] = new VarHistoyChange() {
                        Var = vr,
                        Start = newT,
                        End = newT
                    };
                    if (!ExistsChannel(vr)) {
                        nonExisting.Add(sv);
                    }
                }
                else {
                    VarHistoyChange change = set[vr];
                    if (newT < change.Start) {
                        change.Start = newT;
                    }
                    if (newT > change.End) {
                        change.End = newT;
                    }
                }
            }

            var db = GetDbOrThrow();

            if (nonExisting.Count > 0) {
                int count = nonExisting.Count;
                logger.Debug("Creating {0} not yet existing channels.", count);
                ChannelInfo[] newChannels = nonExisting.Select(v => new ChannelInfo(v.Value.Variable.Object.LocalObjectID, v.Value.Variable.Name, v.Type)).ToArray();
                var swCreate = Stopwatch.StartNew();
                Channel[] channels = db.CreateChannels(newChannels);
                logger.Debug("Created {0} channels completed in {1} ms", count, swCreate.ElapsedMilliseconds);
                for (int i = 0; i < nonExisting.Count; ++i) {
                    mapChannels[nonExisting[i].Value.Variable] = WrapChannelForArchiveSupport(channels[i]);
                }
            }

            var swBatch = Stopwatch.StartNew();

            Func<PrepareContext, string?>[] appendActions = append.Values.Select(v => {
                Channel ch = GetChannelOrThrow(v.Value.Variable);
                Func<PrepareContext, string?> f = ch.PrepareAppend(v.Value.Value, allowOutOfOrderAppend);
                return f;
            }).ToArray();

            string[] errors = db.BatchExecute(appendActions);

            logger.Debug("db.BatchExecute completed for {0} appends in {1} ms", appendActions.Length, swBatch.ElapsedMilliseconds);

            if (errors.Length > 0) {
                logger.Error("Batch append actions failed ({0} of {1}): \n\t{2}", errors.Length, appendActions.Length, string.Join("\n\t", errors));
            }

            // Invalidate aggregation cache for all affected variables and time ranges
            if (aggregationCache != null) {
                foreach (var change in set.Values) {
                    aggregationCache.InvalidateDays(change.Var, change.Start, change.End);
                }
            }

            notifyAppend(set.Values, allowOutOfOrderAppend);

        }
        catch (Exception exp) {
            logger.Error(exp, "Batch Append failed");
        }
    }

    private bool ExistsChannel(VariableRef v) {
        return GetDbOrThrow().ExistsChannel(v.Object.LocalObjectID, v.Name);
    }

    private Channel? GetChannelOrNull(VariableRef v) {
        try {
            return GetChannelOrThrow(v);
        }
        catch (Exception) {
            return null;
        }
    }

    private Channel GetChannelOrThrow(VariableRef v) {
        if (mapChannels.TryGetValue(v, out Channel? value)) {
            return value;
        }
        else {
            Channel res = WrapChannelForArchiveSupport(GetDbOrThrow().GetChannel(v.Object.LocalObjectID, v.Name));
            mapChannels[v] = res;
            return res;
        }
    }

    private Channel GetOrCreateChannelOrThrow(VariableRef v, Variable varDesc) {
        var db = GetDbOrThrow();
        if (db.ExistsChannel(v.Object.LocalObjectID, v.Name))
            return GetChannelOrThrow(v);

        Channel res = WrapChannelForArchiveSupport(db.CreateChannel(new ChannelInfo(v.Object.LocalObjectID, v.Name, varDesc.Type)));
        mapChannels[v] = res;
        return res;
    }

    private Channel WrapChannelForArchiveSupport(Channel channel) {
        return DoWrapChannelForArchiveSupport(channel, archiveSupport, archiveStorage);
    }

    private static Channel DoWrapChannelForArchiveSupport(Channel channel, ArchiveSupportInfo? archiveSupport, StorageBase? storage) {
        if (archiveSupport == null || storage == null) {
            return channel;
        }
        else {
            return new ArchiveWrapperChannel(channel, MakeArchiveChannel(channel, storage), archiveSupport.ArchiveOlderThanDays);
        }
    }

    private ArchiveChannel MakeArchiveChannel(Channel channel) {
        StorageBase storage = archiveStorage ?? throw new Exception("Archive storage not initialized");
        return MakeArchiveChannel(channel, storage);
    }

    private static ArchiveChannel MakeArchiveChannel(Channel channel, StorageBase storage) {
        return new ArchiveChannel(channel.Ref, storage);
    }

    #region Aggregation Cache Support

    private static HistoryAggregationCache? TryCreateAggregationCache(string cacheDbPath) {
        try {
            return new HistoryAggregationCache(cacheDbPath);
        }
        catch (Exception ex) {
            logger.Warn(ex, "Failed to create aggregation cache, continuing without caching");
            return null;
        }
    }

    #endregion

    #region Concurrent Read Support

    private int GetOrAssignWorkerIndex(VariableRef variable) {
        if (channelAffinity.TryGetValue(variable, out int idx)) {
            return idx;
        }

        // Round-robin assignment for new channels
        idx = nextWorkerIndex;
        nextWorkerIndex = (nextWorkerIndex + 1) % maxConcurrentReads;
        channelAffinity[variable] = idx;
        return idx;
    }

    private async Task<bool> WaitForActiveReadsToComplete() {
        if (activeReadTasks.Count > 0) {
            var pending = activeReadTasks.Where(t => !t.IsCompleted).ToArray();
            bool needToWait = pending.Length > 0;
            if (needToWait) {
                try {
                    //var sw = Stopwatch.StartNew();
                    await Task.WhenAll(pending);
                    //sw.Stop();
                    //logger.Info("WaitForActiveReadsToComplete: Waited for {0} read tasks to complete in {1} ms", pending.Length, sw.ElapsedMilliseconds);
                }
                catch (Exception ex) {
                    logger.Warn(ex, "One or more read operations failed while waiting for write");
                }
            }
            else {
                //logger.Info("WaitForActiveReadsToComplete: All read tasks already completed.");
            }
            activeReadTasks.Clear();
            return needToWait;
        }
        else {
            //logger.Info("WaitForActiveReadsToComplete: No active read tasks to wait for.");
            return false;
        }
    }

    private void CleanupCompletedReadTasks() {
        activeReadTasks.RemoveAll(t => t.IsCompleted);
    }

    #endregion

    private readonly Queue<WorkItem> localQueue = new Queue<WorkItem>();

    private bool HasNext() { return localQueue.Count > 0 || queue.Count > 0; }

    private async Task<WorkItem> ReceiveNext() {

        int N = queue.Count;

        if (localQueue.Count > 0 && N == 0) {
            return localQueue.Dequeue();
        }

        if (localQueue.Count == 0 && N == 0) {
            localQueue.Enqueue(await queue.ReceiveAsync());
            return await ReceiveNext();
        }

        for (int i = 0; i < N; ++i) {
            WorkItem item = await queue.ReceiveAsync();
            localQueue.Enqueue(item);
        }

        PrioritizeAndCompressLocalQueue();

        return localQueue.Dequeue();
    }

    private void PrioritizeAndCompressLocalQueue() {

        WorkItem front = localQueue.Peek();

        if (prioritizeReadRequests) {

            if (front.IsReadRequest) {
                return;
            }
            else {
                WorkItem? readReq = localQueue.FirstOrDefault(x => x.IsReadRequest);
                if (readReq != null) {
                    WorkItem[] other = localQueue.Where(x => x != readReq).ToArray();
                    localQueue.Clear();
                    localQueue.Enqueue(readReq);
                    foreach (WorkItem it in other) {
                        localQueue.Enqueue(it);
                    }
                    return;
                }
            }
        }

        if (front is WI_BatchAppend && localQueue.Count > 1) {

            WI_BatchAppend[] appends = localQueue.TakeWhile(wi => wi is WI_BatchAppend).Cast<WI_BatchAppend>().ToArray();
            if (appends.Length > 1) {
                StoreValue[] values = appends.SelectMany(app => app.Values).ToArray();
                WI_BatchAppend frontAppend = new WI_BatchAppend(values);
                WorkItem[] other = localQueue.Skip(appends.Length).ToArray();
                localQueue.Clear();
                localQueue.Enqueue(frontAppend);
                foreach (WorkItem it in other) {
                    localQueue.Enqueue(it);
                }
            }

            return;
        }
    }
}
