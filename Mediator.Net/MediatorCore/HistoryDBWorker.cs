// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Timeseries;
using Ifak.Fast.Mediator.Util;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
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

        private readonly AsyncQueue<WorkItem> queue = new AsyncQueue<WorkItem>();
        private volatile bool started = false;
        private volatile bool terminated = false;

        public HistoryDBWorker(
                    string dbName, 
                    string dbConnectionString, 
                    string[] dbSettings, 
                    bool prioritizeReadRequests, 
                    bool allowOutOfOrderAppend, 
                    Duration? retentionTime,
                    Duration retentionCheckInterval,
                    Func<TimeSeriesDB> dbCreator, 
                    Action<IEnumerable<VarHistoyChange>, bool> notifyAppend) {

            this.dbName = dbName;
            this.dbConnectionString = dbConnectionString;
            this.dbSettings = dbSettings;
            this.prioritizeReadRequests = prioritizeReadRequests;
            this.allowOutOfOrderAppend = allowOutOfOrderAppend;
            this.retentionTime = retentionTime;
            this.retentionCheckInterval = retentionCheckInterval;
            this.dbCreator = dbCreator;
            this.notifyAppend = notifyAppend;

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

        private class WI_Start : WorkItem
        {
            public readonly TaskCompletionSource<bool> Promise;
            public override bool IsReadRequest => false;

            public WI_Start(TaskCompletionSource<bool> promise) {
                Promise = promise;
            }
        }

        private class WI_Terminate : WorkItem
        {
            public readonly TaskCompletionSource<bool> Promise;
            public override bool IsReadRequest => false;

            public WI_Terminate(TaskCompletionSource<bool> promise) {
                Promise = promise;
            }
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

        private class WI_BatchAppend : WorkItem
        {
            public readonly IList<StoreValue> Values;

            public override bool IsReadRequest => false;

            public WI_BatchAppend(IList<StoreValue> values) {
                Values = values;
            }
        }

        public Task<List<VTTQ>> ReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {
            var promise = new TaskCompletionSource<List<VTTQ>>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_ReadRaw(variable, startInclusive, endInclusive, maxValues, bounding, filter, promise));
            }
            return promise.Task;
        }

        private class WI_ReadRaw : WorkItem
        {
            public readonly VariableRef Variable;
            public readonly Timestamp StartInclusive;
            public readonly Timestamp EndInclusive;
            public readonly int MaxValues;
            public readonly BoundingMethod Bounding;
            public readonly QualityFilter Filter;

            public readonly TaskCompletionSource<List<VTTQ>> Promise;
            public override bool IsReadRequest => true;

            public WI_ReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter, TaskCompletionSource<List<VTTQ>> promise) {
                Variable = variable;
                StartInclusive = startInclusive;
                EndInclusive = endInclusive;
                MaxValues = maxValues;
                Bounding = bounding;
                Filter = filter;
                Promise = promise;
            }
        }

        public Task<long> Count(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter) {
            var promise = new TaskCompletionSource<long>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_Count(variable, startInclusive, endInclusive, filter, promise));
            }
            return promise.Task;
        }

        private class WI_Count : WorkItem
        {
            public readonly VariableRef Variable;
            public readonly Timestamp StartInclusive;
            public readonly Timestamp EndInclusive;
            public readonly QualityFilter Filter;

            public readonly TaskCompletionSource<long> Promise;
            public override bool IsReadRequest => true;

            public WI_Count(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter, TaskCompletionSource<long> promise) {
                Variable = variable;
                StartInclusive = startInclusive;
                EndInclusive = endInclusive;
                Filter = filter;
                Promise = promise;
            }
        }

        public Task<List<VTQ>> ReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter) {
            var promise = new TaskCompletionSource<List<VTQ>>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_ReadAggregatedIntervals(variable, intervalBounds, aggregation, filter, promise));
            }
            return promise.Task;
        }

        private class WI_ReadAggregatedIntervals : WorkItem
        {
            public readonly VariableRef Variable;
            public readonly Timestamp[] IntervalBounds;
            public readonly Aggregation Aggregation;
            public readonly QualityFilter Filter;

            public readonly TaskCompletionSource<List<VTQ>> Promise;
            public override bool IsReadRequest => true;

            public WI_ReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter, TaskCompletionSource<List<VTQ>> promise) {
                Variable = variable;
                IntervalBounds = intervalBounds;
                Aggregation = aggregation;
                Filter = filter;
                Promise = promise;
            }
        }

        public Task<long> DeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
            var promise = new TaskCompletionSource<long>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_DeleteInterval(variable, startInclusive, endInclusive, promise));
            }
            return promise.Task;
        }

        private class WI_DeleteInterval : WorkItem
        {
            public readonly VariableRef Variable;
            public readonly Timestamp StartInclusive;
            public readonly Timestamp EndInclusive;

            public readonly TaskCompletionSource<long> Promise;
            public override bool IsReadRequest => false;

            public WI_DeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, TaskCompletionSource<long> promise) {
                Variable = variable;
                StartInclusive = startInclusive;
                EndInclusive = endInclusive;
                Promise = promise;
            }
        }

        public Task Truncate(VariableRef variable) {
            var promise = new TaskCompletionSource();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_Truncate(variable, promise));
            }
            return promise.Task;
        }

        private class WI_Truncate : WorkItem {
            public readonly VariableRef Variable;

            public readonly TaskCompletionSource Promise;
            public override bool IsReadRequest => false;

            public WI_Truncate(VariableRef variable, TaskCompletionSource promise) {
                Variable = variable;
                Promise = promise;
            }
        }

        public Task<VTTQ?> GetLatestTimestampDb(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {
            var promise = new TaskCompletionSource<VTTQ?>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_GetLatestTimestampDb(variable, startInclusive, endInclusive, promise));
            }
            return promise.Task;
        }

        private class WI_GetLatestTimestampDb : WorkItem
        {
            public readonly VariableRef Variable;
            public readonly Timestamp StartInclusive;
            public readonly Timestamp EndInclusive;

            public readonly TaskCompletionSource<VTTQ?> Promise;
            public override bool IsReadRequest => true;

            public WI_GetLatestTimestampDb(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, TaskCompletionSource<VTTQ?> promise) {
                Variable = variable;
                StartInclusive = startInclusive;
                EndInclusive = endInclusive;
                Promise = promise;
            }
        }

        public Task Modify(VariableRef variable, Variable varDesc, VTQ[] data, ModifyMode mode) {
            var promise = new TaskCompletionSource<bool>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_Modify(variable, varDesc, data, mode, promise));
            }
            return promise.Task;
        }

        private class WI_Modify : WorkItem
        {
            public readonly VariableRef Variable;
            public readonly Variable VarDesc;
            public readonly VTQ[] Data;
            public readonly ModifyMode Mode;

            public readonly TaskCompletionSource<bool> Promise;
            public override bool IsReadRequest => false;

            public WI_Modify(VariableRef variable, Variable varDesc, VTQ[] data, ModifyMode mode, TaskCompletionSource<bool> promise) {
                Variable = variable;
                VarDesc = varDesc;
                Data = data;
                Mode = mode;
                Promise = promise;
            }
        }

        public Task Delete(VariableRef variable) {
            var promise = new TaskCompletionSource<bool>();
            if (CheckPrecondition(promise)) {
                queue.Post(new WI_Delete(variable, promise));
            }
            return promise.Task;
        }

        private class WI_Delete : WorkItem
        {
            public readonly VariableRef Variable;

            public readonly TaskCompletionSource<bool> Promise;
            public override bool IsReadRequest => false;

            public WI_Delete(VariableRef variable, TaskCompletionSource<bool> promise) {
                Variable = variable;
                Promise = promise;
            }
        }

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

        private TimeSeriesDB? db = null;
        private readonly Dictionary<VariableRef, Channel> mapChannels = new Dictionary<VariableRef, Channel>();

        private TimeSeriesDB GetDbOrThrow() {
            if (db == null) throw new Exception("Database is closed");
            return db;
        }

        private async Task Runner() {

            while (true) {

                WorkItem it = await ReceiveNext();

                if (it is WI_BatchAppend batchAppend) {
                    try {
                        Append(batchAppend);
                    }
                    catch (Exception exp) {
                        logger.Error(exp, "Batch Append failed");
                    }
                }
                else if (it is WI_ReadRaw readRaw) {
                    ReadRaw(readRaw);
                }
                else if (it is WI_Count count) {
                    DoCount(count);
                }
                else if (it is WI_ReadAggregatedIntervals readAggregatedIntervals) {
                    DoReadAggregatedIntervals(readAggregatedIntervals);
                }
                else if (it is WI_DeleteInterval deleteInterval) {
                    DoDeleteInterval(deleteInterval);
                }
                else if (it is WI_Truncate truncate) {
                    DoTruncate(truncate);
                }
                else if (it is WI_Modify modify) {
                    DoModify(modify);
                }
                else if (it is WI_Delete delete) {
                    DoDelete(delete);
                }
                else if (it is WI_GetLatestTimestampDb getLatestTimestampDb) {
                    DoGetLatestTimestampDb(getLatestTimestampDb);
                }
                else if (it is WI_Start start) {
                    try {
                        db = dbCreator();
                        var openParams = new TimeSeriesDB.OpenParams(
                            Name: dbName,
                            ConnectionString: dbConnectionString,
                            Settings: dbSettings,
                            RetentionTime: retentionTime,
                            RetentionCheckInterval: retentionCheckInterval);
                        db.Open(openParams);
                        start.Promise.SetResult(true);
                        started = true;
                    }
                    catch (Exception e) {
                        start.Promise.SetException(e);
                        return;
                    }
                }
                else if (it is WI_Terminate terminate) {
                    db?.Close();
                    terminate.Promise.SetResult(true);
                    return;
                }
            }
        }

        private void ReadRaw(WI_ReadRaw read) {
            var promise = read.Promise;
            try {
                Channel? ch = GetChannelOrNull(read.Variable);
                if (ch == null) {
                    promise.SetResult(new List<VTTQ>(0));
                }
                else {
                    List<VTTQ> res = ch.ReadData(read.StartInclusive, read.EndInclusive, read.MaxValues, Map(read.Bounding), Map(read.Filter));
                    promise.SetResult(res);
                }
            }
            catch (Exception exp) {
                promise.SetException(exp);
            }
        }

        private void DoCount(WI_Count req) {
            var promise = req.Promise;
            try {
                Channel? ch = GetChannelOrNull(req.Variable);
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

        private void DoReadAggregatedIntervals(WI_ReadAggregatedIntervals req) {
            var promise = req.Promise;
            try {
                Channel? ch = GetChannelOrNull(req.Variable);
                if (ch == null) {
                    promise.SetResult(new List<VTQ>());
                }
                else {
                    Timestamp[] intervalBounds = req.IntervalBounds;
                    Aggregation aggregation = req.Aggregation;
                    QualityFilter filter = req.Filter;

                    List<VTQ> result = ch.ReadAggregatedIntervals(intervalBounds, aggregation, Map(filter));
                    promise.SetResult(result);
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
                    }
                    else {
                        res = ch.DeleteData(req.StartInclusive, req.EndInclusive);
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
                        break;

                    case ModifyMode.Delete:
                        ch.DeleteData(req.Data.Select(x => x.T).ToArray());
                        break;

                    default:
                        throw new Exception("Unknown modify mode: " + req.Mode);
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
                GetDbOrThrow().RemoveChannel(v.Object.LocalObjectID, v.Name);
                promise.SetResult(true);
            }
            catch (Exception exp) {
                promise.SetException(exp);
            }
        }

        private void DoGetLatestTimestampDb(WI_GetLatestTimestampDb req) {
            var promise = req.Promise;
            try {
                Channel? ch = GetChannelOrNull(req.Variable);
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

        private Timeseries.BoundingMethod Map(BoundingMethod b) {
            switch (b) {
                case BoundingMethod.CompressToN: return Timeseries.BoundingMethod.CompressToN;
                case BoundingMethod.TakeFirstN: return Timeseries.BoundingMethod.TakeFirstN;
                case BoundingMethod.TakeLastN: return Timeseries.BoundingMethod.TakeLastN;
            }
            throw new Exception("Unknown bounding");
        }

        private Timeseries.QualityFilter Map(QualityFilter f) {
            switch (f) {
                case QualityFilter.ExcludeNone: return Timeseries.QualityFilter.ExcludeNone;
                case QualityFilter.ExcludeBad: return Timeseries.QualityFilter.ExcludeBad;
                case QualityFilter.ExcludeNonGood: return Timeseries.QualityFilter.ExcludeNonGood;
            }
            throw new Exception("Unknown quality filter");
        }

        private void Append(WI_BatchAppend append) {

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
                    mapChannels[nonExisting[i].Value.Variable] = channels[i];
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

            notifyAppend(set.Values, allowOutOfOrderAppend);
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
            if (mapChannels.ContainsKey(v)) {
                return mapChannels[v];
            }
            else {
                Channel res = GetDbOrThrow().GetChannel(v.Object.LocalObjectID, v.Name);
                mapChannels[v] = res;
                return res;
            }
        }

        private Channel GetOrCreateChannelOrThrow(VariableRef v, Variable varDesc) {
            var db = GetDbOrThrow();
            if (db.ExistsChannel(v.Object.LocalObjectID, v.Name))
                return GetChannelOrThrow(v);

            Channel res = db.CreateChannel(new ChannelInfo(v.Object.LocalObjectID, v.Name, varDesc.Type));
            mapChannels[v] = res;
            return res;
        }

        private readonly Queue<WorkItem> localQueue = new Queue<WorkItem>();

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
}
