// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO
{
    public class Module : ModelObjectModule<Config.IO_Model>
    {
        private readonly Dictionary<string, ItemState> dataItemsState = new Dictionary<string, ItemState>();
        private readonly List<AdapterState> adapters = new List<AdapterState>();
        private ModuleThread? moduleThread = null;

        private readonly Dictionary<string, Type> mapAdapterTypes = new Dictionary<string, Type>();

        private const string VariableName = "Value";

        public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {

            this.moduleThread = moduleThread;
            await base.Init(info, restoreVariableValues, notifier, moduleThread);

            var config = info.GetConfigReader();

            string strAssemblies = config.GetOptionalString("adapter-assemblies", "");

            const string releaseDebugPlaceHolder = "{RELEASE_OR_DEBUG}";
            if (strAssemblies.Contains(releaseDebugPlaceHolder)) {
#if DEBUG
                strAssemblies = strAssemblies.Replace(releaseDebugPlaceHolder, "Debug");
#else
                strAssemblies = strAssemblies.Replace(releaseDebugPlaceHolder, "Release");
#endif
            }

            string[] assemblies = strAssemblies
                .Split(new char[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .ToArray();

            string[] absoluteAssemblies = assemblies.Select(d => Path.GetFullPath(d)).ToArray();
            foreach (string assembly in absoluteAssemblies) {
                if (!File.Exists(assembly)) throw new Exception($"adapter-assembly does not exist: {assembly}");
            }

            absoluteAssemblies = absoluteAssemblies.Select(assembly => {
                if (assembly.ToLowerInvariant().EndsWith(".cs")) {
                    return CompileAdapter.CSharpFile2Assembly(assembly);
                }
                return assembly;
            }).ToArray();

            foreach (VariableValue v in restoreVariableValues) {
                string dataItemID = v.Variable.Object.LocalObjectID;
                if (dataItemsState.ContainsKey(dataItemID)) {
                    dataItemsState[dataItemID].LastReadValue = v.Value;
                    dataItemsState[dataItemID].LastWrittenValue = v.Value;
                }
            }

            var adapterTypes = Reflect.GetAllNonAbstractSubclasses(typeof(AdapterBase)).ToList();
            adapterTypes.AddRange(absoluteAssemblies.SelectMany(LoadTypesFromAssemblyFile));

            mapAdapterTypes.Clear();
            foreach (var type in adapterTypes) {
                Identify? id = type.GetCustomAttribute<Identify>();
                if (id != null) {
                    mapAdapterTypes[id.ID] = type;
                }
            }

            foreach (AdapterState adapter in adapters) {
                adapter.CreateInstance(mapAdapterTypes);
            }

            try {
                Task[] initTasks = adapters.Select(InitAdapter).ToArray();
                await Task.WhenAll(initTasks);
            }
            catch (Exception exp) {

                string[] failedAdapters = adapters
                    .Where(a => a.State == State.InitError)
                    .Select(a => "Init of IO adapter '" + a.Config.Name + "' failed: " + a.LastError)
                    .ToArray();

                string errMessage = failedAdapters.Length > 0 ? string.Join("; ", failedAdapters) : exp.Message;
                Console.Error.WriteLine(errMessage);
                await Shutdown();
                throw new Exception(errMessage);
            }
        }

        private static Type[] LoadTypesFromAssemblyFile(string fileName) {
            try {
                Type baseClass = typeof(AdapterBase);

                var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                        fileName,
                        sharedTypes: new Type[] { baseClass });

                return loader.LoadDefaultAssembly()
                    .GetExportedTypes()
                    .Where(t => t.IsSubclassOf(baseClass) && !t.IsAbstract)
                    .ToArray();
            }
            catch (Exception exp) {
                Console.Error.WriteLine($"Failed to load IO adapter types from assembly '{fileName}': {exp.Message}");
                Console.Error.Flush();
                return new Type[0];
            }
        }

        protected override void ModifyModelAfterInit() {
            foreach(var adapter in model.GetAllAdapters()) {
                foreach (var dataItem in adapter.GetAllDataItems()) {
                    if (string.IsNullOrEmpty(dataItem.Name)) {
                        dataItem.Name = dataItem.ID;
                    }
                }
            }
        }

        protected override async Task OnConfigModelChanged(bool init) {

            await base.OnConfigModelChanged(init);

            model.ValidateOrThrow();
            model.Normalize();

            Config.Adapter[] enabledAdapters = model.GetAllAdapters().Where(a => a.Enabled && !string.IsNullOrEmpty(a.Type)).ToArray();

            AdapterState[] removedAdapters = adapters.Where(a => !enabledAdapters.Any(x => x.ID == a.Config.ID)).ToArray();
            AdapterState[] newAdapters = enabledAdapters.Where(x => !adapters.Any(a => x.ID == a.Config.ID)).Select(a => new AdapterState(a)).ToArray();

            adapters.RemoveAll(removedAdapters);
            adapters.AddRange(newAdapters);

            var restartAdapters = new List<AdapterState>();
            var newDataItems = new List<ItemState>();
            foreach (AdapterState adapter in adapters) {

                bool changed = adapter.SetConfig(enabledAdapters.FirstOrDefault(x => x.ID == adapter.Config.ID));
                if (changed) {
                    restartAdapters.Add(adapter);
                }

                adapter.UpdateScheduledItems(model);

                foreach (Config.DataItem dataItem in adapter.Config.GetAllDataItems()) {
                    string id = dataItem.ID;
                    VTQ value = dataItemsState.ContainsKey(id) ? dataItemsState[id].LastReadValue : new VTQ(Timestamp.Empty, Quality.Bad, dataItem.GetDefaultValue());
                    newDataItems.Add(new ItemState(dataItem, adapter, value, adapter.Config.MaxFractionalDigits));
                }
            }
            dataItemsState.Clear();
            foreach (ItemState it in newDataItems) {
                dataItemsState[it.ID] = it;
            }

            if (init == false) {

                if (removedAdapters.Length > 0) {
                    await ShutdownAdapters(removedAdapters);
                }

                if (newAdapters.Length > 0) {

                    foreach (AdapterState adapter in newAdapters) {
                        adapter.CreateInstance(mapAdapterTypes);
                    }

                    Task[] initTasks = newAdapters.Select(InitAdapter).ToArray();
                    await Task.WhenAll(initTasks);

                    foreach (AdapterState adapter in newAdapters) {
                        StartScheduledReadTask(adapter);
                    }
                }

                if (restartAdapters.Count > 0) {
                    Task[] restartTasks = restartAdapters.Select(a => RestartAdapter(a, "Config changed", critical: false)).ToArray();
                    await Task.WhenAll(restartTasks);
                }
            }
        }

        private async Task RestartAdapter(AdapterState adapter, string reason, bool critical = true, int tryCounter = 0) {

            if (adapter.IsRestarting && tryCounter == 0) { return; }
            adapter.IsRestarting = true;

            if (critical) {
                if (tryCounter == 0)
                    Log_Warn("AdapterRestart", $"Restarting adapter {adapter.Name}. Reason: {reason}");
                else
                    Log_Warn("AdapterRestart", $"Restarting adapter {adapter.Name} (retry {tryCounter}). Reason: {reason}");
            }
            else {
                Log_Info("AdapterRestart", $"Restarting adapter {adapter.Name}. Reason: {reason}");
            }

            const int TimeoutSeconds = 10;
            try {
                Task shutdown = ShutdownAdapter(adapter);
                Task t = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
                if (t != shutdown) {
                    Log_Warn("AdapterShutdownTimeout", $"Shutdown request for adapter {adapter.Name} failed to complete within {TimeoutSeconds} seconds.");
                    // go ahead and hope for the best...
                }
                adapter.CreateInstance(mapAdapterTypes);
                await InitAdapter(adapter);
                if (adapter.ScheduledReadingTask == null || adapter.ScheduledReadingTask.IsCompleted) {
                    StartScheduledReadTask(adapter);
                }
                else {
                    adapter.Instance!.StartRunning();
                    adapter.State = State.Running;
                }
                adapter.IsRestarting = false;
            }
            catch (Exception exception) {
                Exception exp = exception.GetBaseException();
                string errMsg = $"Restart of adapter {adapter.Name} failed: {exp.Message}";
                Log_Error("AdapterRestartError", errMsg);
                if (critical) {
                    // Thread.Sleep(500);
                    // Environment.Exit(1); // will result in restart of entire module by Mediator
                    int delayMS = Math.Min(10 * 1000, (tryCounter + 1) * 1000);
                    await Task.Delay(delayMS);
                    Task ignored = RestartAdapter(adapter, exp.Message, critical, tryCounter + 1);
                }
                else {
                    adapter.IsRestarting = false;
                    throw new Exception(errMsg);
                }
            }
        }

        private async Task InitAdapter(AdapterState adapter) {
            Adapter info = adapter.Config.ToAdapter();
            try {

                adapter.UpdateScheduledItems(model);

                ItemSchedule[] scheduledDataItems = adapter.ScheduledDataItems;
                DataItemInfo[] items = info.GetAllDataItems()
                    .Where(it => it.Read == true)
                    .Select(it => {
                        string id = it.ID;
                        bool scheduled = scheduledDataItems.Any(sit => sit.DataItemID == id);
                        VTQ vtq = dataItemsState[id].LastReadValue;
                        VTQ? v = vtq.T.IsEmpty ? (VTQ?)null : vtq;
                        return new DataItemInfo(id, v, scheduled);
                    }).
                    ToArray();

                var wrapper = new Wrapper(this, info);
                adapter.State = State.InitStarted;
                var groups = await adapter.Instance!.Initialize(info, wrapper, items);
                if (adapter.State == State.InitStarted) {
                    adapter.State = State.InitComplete;
                    adapter.SetGroups(groups);

                    Duration maxInitDelayForGoodQuality = adapter.Config.MaxInitDelayForGoodQuality;
                    if (adapter.ScheduledDataItems.Length > 0 && maxInitDelayForGoodQuality.TotalMilliseconds > 0) {
                        VTQ empty = new VTQ();
                        ReadRequest[] requests = adapter.ScheduledDataItems.Select(di => new ReadRequest(di.DataItemID, empty)).ToArray();
                        Func<VTQ, bool> NonGood = x => x.Q != Quality.Good;

                        VTQ[] readResults = await DoAdapterRead(adapter, requests, maxInitDelayForGoodQuality, hideWarnings: true);
                        if (readResults.Any(NonGood)) {
                            var sw = new Stopwatch();
                            sw.Start();
                            do {
                                await Task.Delay(200);
                                readResults = await DoAdapterRead(adapter, requests, maxInitDelayForGoodQuality, hideWarnings: true);
                            }
                            while (readResults.Any(NonGood) && sw.Elapsed < maxInitDelayForGoodQuality.ToTimeSpan());
                            sw.Stop();
                        }
                    }
                }
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                adapter.State = State.InitError;
                adapter.LastError = e.Message;
                throw new Exception($"Initialize of adapter {info.Name} failed: " + e.Message, exp);
            }
        }

        private Task Shutdown() => ShutdownAdapters(adapters);

        private async Task ShutdownAdapters(IEnumerable<AdapterState> adapters) {

            Task[] shutdownTasks = adapters
                .Where(a => a.State == State.InitStarted || a.State == State.InitComplete || a.State == State.Running)
                .Select(ShutdownAdapter)
                .ToArray();

            await Task.WhenAll(shutdownTasks);
        }

        private async Task ShutdownAdapter(AdapterState adapter) {
            adapter.State = State.ShutdownStarted;
            try {
                var instance = adapter.Instance;
                if (instance == null) {
                    Log_Warn("AdapterShutdownError", "ShutdownAdapter: Instance is null");
                }
                else {
                    await instance.Shutdown();
                }
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                Log_Warn("AdapterShutdownError", "Shutdown exception: " + e.Message);
            }
            adapter.State = State.ShutdownCompleted;
            adapter.SetInstanceNull();
        }

        public async override Task InitAbort() {
            await Shutdown();
        }

        public override async Task Run(Func<bool> shutdown) {

            foreach (AdapterState a in adapters) {
                StartScheduledReadTask(a);
            }

            while (!shutdown()) {
                await Task.Delay(500);
                CheckWriteRepeat();
            }
            await Shutdown();
        }

        private void CheckWriteRepeat() {

            foreach (AdapterState a in adapters) {
                if (a.Config.RepeatWriteInterval.TotalMilliseconds <= 0) continue;
                var now = Timestamp.Now;
                var maxAge = now - a.Config.RepeatWriteInterval;
                var staleWriteItems = a.Config.GetAllDataItems()
                    .Where(di => di.Write && dataItemsState.ContainsKey(di.ID) && dataItemsState[di.ID].LastWritten < maxAge)
                    .ToArray();

                if (staleWriteItems.Length == 0) continue;

                Origin origin = new Origin();
                origin.Type = OriginType.Module;
                origin.ID = moduleID;
                origin.Name = moduleName;

                var varValues = staleWriteItems.Select(di => {
                    ItemState state = dataItemsState[di.ID];
                    return VariableValue.Make(moduleID, di.ID, VariableName, state.LastWrittenValue);
                }).ToArray();

                Task ignored = WriteVariables(origin, varValues, null, sync: false);
            }
        }

        public override async Task<VTQ[]> ReadVariables(Origin origin, VariableRef[] variables, Duration? timeout) {

            var adapter2Items = new Dictionary<AdapterState, List<ReadRequest_Idx>>();

            for (int i = 0; i < variables.Length; ++i) {
                VariableRef vr = variables[i];
                string id = vr.Object.LocalObjectID;
                if (dataItemsState.ContainsKey(id) && vr.Name == VariableName) {

                    ItemState itemState = dataItemsState[id];
                    AdapterState adapter = itemState.Adapter;

                    if (!adapter2Items.ContainsKey(adapter)) {
                        adapter2Items[adapter] = new List<ReadRequest_Idx>();
                    }

                    VTQ value = itemState.LastReadValue;
                    adapter2Items[adapter].Add(new ReadRequest_Idx(id, value, i));
                }
                else {
                    throw new Exception($"No data item with id {id} found.");
                }
            }

            var allReadTasks = new List<Task<VTQ_Idx[]>>(adapter2Items.Count);

            foreach (var adapterItems in adapter2Items) {

                AdapterState adapter = adapterItems.Key;
                List<ReadRequest_Idx> requests = adapterItems.Value;

                Task<VTQ[]> readTask = AdapterReadTask(adapter, requests.Select(r => r.V).ToList(), timeout);

                Task<VTQ_Idx[]> combinedTask = readTask.ContinueOnMainThread((task) => {
                    VTQ[] vtqs = task.Result;
                    VTQ_Idx[] res = new VTQ_Idx[vtqs.Length];
                    for (int i = 0; i < vtqs.Length; ++i) {
                        res[i] = new VTQ_Idx(vtqs[i], requests[i].Idx);
                    }
                    return res;
                });

                allReadTasks.Add(combinedTask);
            }

            VTQ[] result = new VTQ[variables.Length];
            VTQ_Idx[][] resArr = await Task.WhenAll(allReadTasks);
            foreach (VTQ_Idx[] arr in resArr) {
                foreach (VTQ_Idx v in arr) {
                    result[v.Idx] = v.V;
                }
            }
            return result;
        }

        internal struct VTQ_Idx {
            public VTQ V { get; set; }
            public int Idx { get; set; }

            public VTQ_Idx(VTQ v, int idx) {
                V = v;
                Idx = idx;
            }
        }

        internal struct ReadRequest_Idx
        {
            public ReadRequest V { get; set; }
            public int Idx { get; set; }

            public ReadRequest_Idx(string id, VTQ lastValue, int idx) {
                V = new ReadRequest(id, lastValue);
                Idx = idx;
            }
        }

        private async Task<VTQ[]> AdapterReadTask(AdapterState adapter, List<ReadRequest> requests, Duration? timeout) {

            if (adapter.State != State.Running || adapter.Instance == null) {
                var now = Timestamp.Now;
                VTQ[] failures = requests.Select(it => new VTQ(now, Quality.Bad, it.LastValue.V)).ToArray();
                return failures;
            }

            try {
                return await DoAdapterRead(adapter, requests, timeout, hideWarnings: false);
            }
            catch (Exception exception) {
                Exception exp = exception.GetBaseException() ?? exception;
                Task ignored = RestartAdapter(adapter, "Read exception: " + exp.Message);
                var now = Timestamp.Now;
                VTQ[] failures = requests.Select(it => new VTQ(now, Quality.Bad, it.LastValue.V)).ToArray();
                return failures;
            }
        }

        private async Task<VTQ[]> DoAdapterRead(AdapterState adapter, IList<ReadRequest> requests, Duration? timeout, bool hideWarnings) {
            IList<ReadTask> readTasks = adapter.ReadItems(requests, timeout);
            Task<DataItemValue[]>[] tasks = readTasks.Select(readTask => readTask.Task).ToArray();
            DataItemValue[][] res = await Task.WhenAll(tasks);
            DataItemValue[] dataValues = res.Length == 1 ? res[0] : res.SelectMany(xx => xx).ToArray();
            VTQ[] vtqs = new VTQ[requests.Count];
            for (int i = 0; i < requests.Count; ++i) {
                string id = requests[i].ID;
                DataItemValue dv = dataValues.First(dv => dv.ID == id);
                vtqs[i] = GetNormalizedDataItemValue(dv, hideWarnings);
            }
            return vtqs;
        }

        private VTQ GetNormalizedDataItemValue(DataItemValue val, bool hideWarnings) {
            VTQ vtq = val.Value;
            if (dataItemsState.ContainsKey(val.ID)) {
                ItemState istate = dataItemsState[val.ID];
                vtq = NormalizeReadValue(istate, vtq, hideWarnings);
                istate.LastReadValue = vtq;
            }
            return vtq;
        }

        public override async Task<WriteResult> WriteVariables(Origin origin, VariableValue[] values, Duration? timeout, bool sync) {

            var failed = new List<VariableError>(0);
            var skippedItems = new List<string>(0);
            var adapter2Items = new Dictionary<AdapterState, List<DataItemValue>>();

            foreach (VariableValue vv in values) {

                string id = vv.Variable.Object.LocalObjectID;

                if (vv.Variable.Name != VariableName) {
                    Log_Warn("InvalidVarNames", "WriteVariables: Invalid variable name: " + vv.Variable, origin);
                    failed.Add(new VariableError(vv.Variable, "Invalid variable name"));
                    continue;
                }

                if (!dataItemsState.ContainsKey(id)) {
                    Log_Warn("UnknownID", "WriteVariables: No writable data item found with id: " + id, origin);
                    failed.Add(new VariableError(vv.Variable, "No writable data item found with id " + id));
                    continue;
                }

                ItemState itemState = dataItemsState[id];

                if (!itemState.Writeable) {
                    Log_Warn("NotWriteable", $"WriteVariables: Data item {id} is not writable", origin);
                    failed.Add(new VariableError(vv.Variable, $"Data item {id} is not writable"));
                    continue;
                }

                itemState.LastWritten = Timestamp.Now;
                itemState.LastWrittenValue = vv.Value;

                AdapterState adapter = itemState.Adapter;
                if (adapter.SetOfPendingWriteItems.Contains(id)) {
                    skippedItems.Add(id);
                    failed.Add(new VariableError(vv.Variable, "Previous write still pending"));
                    continue;
                }

                VTQ normilizedValue;

                try {
                    normilizedValue = NormalizeWriteValueOrThrow(itemState, vv.Value);
                }
                catch (Exception exp) {
                    failed.Add(new VariableError(vv.Variable, exp.Message));
                    continue;
                }

                if (!adapter2Items.ContainsKey(adapter)) {
                    adapter2Items[adapter] = new List<DataItemValue>();
                }
                adapter2Items[adapter].Add(new DataItemValue(id, normilizedValue));
                adapter.SetOfPendingWriteItems.Add(id);
            }

            if (skippedItems.Count > 0) {
                string warn = string.Format("Write of {0} data items skipped because of pending write: {1}", skippedItems.Count, string.Join(", ", skippedItems));
                Log_Warn("WritesPending", warn, origin);
            }

            var allWriteTasks = new List<Task<WriteDataItemsResult>>(adapter2Items.Count);
            foreach (var adapterItems in adapter2Items) {

                AdapterState adapter = adapterItems.Key;
                List<DataItemValue> items = adapterItems.Value;

                Task<WriteDataItemsResult> task = AdapterWriteTask(adapter, items, timeout);
                allWriteTasks.Add(task);
            }

            WriteDataItemsResult[] resArr = await Task.WhenAll(allWriteTasks);
            WriteResult res = MapWriteResults(moduleID, resArr);

            if (res.Failed()) {
                failed.AddRange(res.FailedVariables!);
            }

            if (failed.Count == 0)
                return WriteResult.OK;
            else
                return WriteResult.Failure(failed.ToArray());
        }

        private static WriteResult MapWriteResults(string moduleID, IEnumerable<WriteDataItemsResult> list) {

            if (list.All(r => r.IsOK())) return WriteResult.OK;

            VariableError[] failures = list
                .Where(r => r.Failed())
                .SelectMany(x => x.FailedDataItems.Select(di => new VariableError(ObjectRef.Make(moduleID, di.ID), VariableName, di.Error)))
                .ToArray();

            return WriteResult.Failure(failures);
        }

        private async Task<WriteDataItemsResult> AdapterWriteTask(AdapterState adapter, List<DataItemValue> items, Duration? timeout) {

            if (adapter.State != State.Running || adapter.Instance == null) {
                string err = "Adapter is not in state Running. Current state: " + adapter.State;
                Log_Warn("WriteStateError", err);
                adapter.SetOfPendingWriteItems.ExceptWith(items.Select(it => it.ID));
                FailedDataItemWrite[] failures = items.Select(it => new FailedDataItemWrite(it.ID, err)).ToArray();
                return WriteDataItemsResult.Failure(failures);
            }

            try {
                IList<WriteTask> writeTasks = adapter.WriteItems(items, timeout);
                Task<WriteDataItemsResult>[] tasks = writeTasks.Select(writeTask => writeTask.Task.ContinueOnMainThread(t => {
                    adapter.SetOfPendingWriteItems.ExceptWith(writeTask.IDs);
                    WriteDataItemsResult res = t.Result;
                    if (res.Failed()) {
                        DataItemErr[] failed = ResolveDataItemErrors(res.FailedDataItems!);
                        if (failed.Length > 0) {
                            string names = string.Join(", ", failed.Select(it => it.Name + ": " + it.Error));
                            string msg = $"Write error for {failed.Length} data items: {failed[0].Name}, ...";
                            if (failed.Length == 1) {
                                msg = $"Write error for data item {failed[0].Name}: {failed[0].Error}";
                            }
                            ObjectRef[] objs = failed.Select(it => ObjectRef.Make(moduleID, it.ID)).ToArray();
                            var ev = AlarmOrEventInfo.Warning("WriteErr", msg, objs);
                            ev.Details = names;
                            notifier?.Notify_AlarmOrEvent(ev);
                        }
                    }
                    return res;

                })).ToArray();

                WriteDataItemsResult[] res = await Task.WhenAll(tasks);
                return WriteDataItemsResult.FromResults(res);
            }
            catch (Exception exception) {
                Exception exp = exception.GetBaseException() ?? exception;
                string err = adapter.Name + " adapter exception: " + exp.Message;
                Task ignored = RestartAdapter(adapter, "Write exception: " + exp.Message);
                FailedDataItemWrite[] failures = items.Select(it => new FailedDataItemWrite(it.ID, err)).ToArray();
                return WriteDataItemsResult.Failure(failures);
            }
        }

        private DataItemErr[] ResolveDataItemErrors(FailedDataItemWrite[] errors) {
            return errors.SelectIgnoreException(error => {
                ItemState info = dataItemsState[error.ID];
                return new DataItemErr(id: error.ID, name: info.Name, error: error.Error);
            }).ToArray();
        }

        private class DataItemErr
        {
            public string ID { get; set; }
            public string Name { get; set; }
            public string Error { get; set; }

            public DataItemErr(string id, string name, string error) {
                ID = id;
                Name = name;
                Error = error;
            }

            public DataItemErr() {
                ID = "";
                Name = "";
                Error = "";
            }
        }

        private void StartScheduledReadTask(AdapterState adapter) {
            Task readTask = AdapterScheduledReadTask(adapter);
            adapter.ScheduledReadingTask = readTask;
            var ignored1 = readTask.ContinueOnMainThread(t => {
                if (t.IsFaulted) {
                    Exception exp = t.Exception!.GetBaseException() ?? t.Exception;
                    Task ignored2 = RestartAdapter(adapter, "Read exception: " + exp.Message);
                }
            });
        }

        private async Task AdapterScheduledReadTask(AdapterState adapter) {

            var dict = new Dictionary<Timestamp, List<string>>();

            adapter.Instance!.StartRunning();
            adapter.State = State.Running;

            while (adapter.State == State.Running) {

                dict.Clear();

                Timestamp Now = Timestamp.Now;

                foreach (ItemSchedule it in adapter.ScheduledDataItems) {

                    long intervalMS = it.Interval.TotalMilliseconds;
                    long offMS = it.Offset.TotalMilliseconds;

                    long intervals = 1 + (Now.JavaTicks - offMS) / intervalMS;
                    Timestamp tStart = Timestamp.FromJavaTicks(intervals * intervalMS + offMS);

                    if (!dict.ContainsKey(tStart)) {
                        dict[tStart] = new List<string>();
                    }
                    dict[tStart].Add(it.DataItemID);
                }

                if (dict.Count == 0) {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
                else {
                    KeyValuePair<Timestamp, List<string>> x = dict.OrderBy(kv => kv.Key).First();
                    Timestamp timestamp = x.Key;
                    List<string> itemsToRead = x.Value;

                    Duration waitTime = timestamp - Timestamp.Now;
                    if (waitTime > Duration.FromSeconds(5)) {
                        await Task.Delay(TimeSpan.FromSeconds(1));
                    }
                    else {

                        while (waitTime.TotalMilliseconds > 0) {
                            await Task.Delay((int)waitTime.TotalMilliseconds);
                            waitTime = timestamp - Timestamp.Now;
                        }

                        if (adapter.State == State.Running && adapter.Instance != null) {

                            ReadRequest[] requests = itemsToRead
                                .Where(it => !adapter.SetOfPendingReadItems.Contains(it))
                                .Select(s => {
                                    VTQ value = dataItemsState[s].LastReadValue;
                                    return new ReadRequest(s, value);
                                }).ToArray();

                            if (requests.Length == 0) {
                                await Task.Delay(500);
                            }
                            else {
                                foreach (ReadRequest rr in requests) {
                                    adapter.SetOfPendingReadItems.Add(rr.ID);
                                }

                                IList<ReadTask> readTasks = adapter.ReadItems(requests, null);

                                foreach (ReadTask rt in readTasks) {
                                    Task tx = rt.Task.ContinueOnMainThread(completedReadTask => {
                                        adapter.SetOfPendingReadItems.ExceptWith(rt.IDs);
                                        if (adapter.State == State.Running) {

                                            DataItemValue[] result;

                                            if (completedReadTask.IsFaulted) {
                                                Exception exp = completedReadTask.Exception.GetBaseException() ?? completedReadTask.Exception;
                                                Task ignored = RestartAdapter(adapter, "Scheduled read exception: " + exp.Message);

                                                Timestamp now = Timestamp.Now;
                                                result = rt.IDs.Select(s => {
                                                    VTQ lastVal = dataItemsState[s].LastReadValue;
                                                    VTQ v = VTQ.Make(lastVal.V, now, Quality.Bad);
                                                    return new DataItemValue(s, v);
                                                }).ToArray();
                                            }
                                            else {
                                                result = completedReadTask.Result;
                                            }

                                            var values = new List<VariableValue>(result.Length);
                                            var badItems = new List<ItemState>();
                                            var goodItems = new List<ItemState>();
                                            foreach (DataItemValue val in result) {
                                                if (dataItemsState.ContainsKey(val.ID)) {
                                                    VTQ vtq = val.Value;
                                                    bool strict = !adapter.NonStrictScheduledDataItems.Contains(val.ID);
                                                    if (strict) {
                                                        vtq.T = timestamp;
                                                    }
                                                    ItemState istate = dataItemsState[val.ID];
                                                    vtq = NormalizeReadValue(istate, vtq);
                                                    if (vtq.Q == Quality.Bad && istate.LastReadValue.Q != Quality.Bad) {
                                                        badItems.Add(istate);
                                                    }
                                                    else if (vtq.Q == Quality.Good && istate.LastReadValue.Q != Quality.Good) {
                                                        goodItems.Add(istate);
                                                    }
                                                    istate.LastReadValue = vtq;
                                                    values.Add(VariableValue.Make(moduleID, val.ID, VariableName, vtq));
                                                }
                                            }

                                            if (values.Count > 0 && notifier != null) {
                                                notifier.Notify_VariableValuesChanged(values);
                                            }

                                            NotifyQualityChange(badItems, goodItems);
                                        }
                                    });
                                }
                            }
                        }
                    }
                }
            } // while running
        }

        private VTQ NormalizeReadValue(ItemState istate, VTQ vtq, bool noWarnings = false) {

            if (istate.ReplacementValue.HasValue) {
                vtq.V = istate.ReplacementValue.Value;
                if (istate.ReplacementQuality.HasValue) {
                    vtq.Q = istate.ReplacementQuality.Value;
                }
                return vtq;
            }

            if (istate.Dimension != 1) {
                if (istate.ReplacementQuality.HasValue) {
                    vtq.Q = istate.ReplacementQuality.Value;
                }
                return vtq;
            }

            bool SrcIsBool = vtq.V.IsBool;
            bool DstIsFloat = istate.Type.IsFloat();

            if (SrcIsBool) {
                if (DstIsFloat) {
                    vtq.V = DataValue.FromDouble(vtq.V.GetBool() ? 1 : 0);
                }
                else if (istate.Type.IsNumeric()) {
                    vtq.V = DataValue.FromInt(vtq.V.GetBool() ? 1 : 0);
                }
            }

            if (DstIsFloat) {

                double? vv = vtq.V.AsDouble();
                if (!vv.HasValue) {
                    if (!noWarnings) {
                        Log_Warn("ValIsNotFloat", $"Value for data item {istate.Name} is not a float value: {vtq.V}");
                    }
                    return VTQ.Make(0.0, vtq.T, Quality.Bad);
                }

                double v = vv.Value;

                try {
                    v = istate.ReadConversion(v);
                }
                catch (Exception exp) {
                    if (!noWarnings) {
                        Log_Warn_Details("ReadConversionFailed", $"Read conversion of data item {istate.Name} failed. Value: {vtq.V}", exp.Message);
                    }
                    return vtq.WithQuality(Quality.Bad);
                }

                try {
                    if (istate.FractionalDigits.HasValue) {
                        int digits = istate.FractionalDigits.Value;
                        v = Math.Round(v, digits);
                    }
                }
                catch (Exception exp) {
                    if (!noWarnings) {
                        Log_Warn_Details("RoundingFailed", $"Rounding of data item {istate.Name} failed. Value: {vtq.V}", exp.Message);
                    }
                }

                vtq.V = DataValue.FromDouble(v);
            }

            if (istate.ReplacementQuality.HasValue) {
                vtq.Q = istate.ReplacementQuality.Value;
            }

            return vtq;
        }

        private VTQ NormalizeWriteValueOrThrow(ItemState istate, VTQ vtq) {

            if (istate.Dimension != 1) {
                return vtq;
            }

            bool ValueIsBool = vtq.V.IsBool;
            bool TypeIsFloat = istate.Type.IsFloat();

            if (ValueIsBool) {
                if (TypeIsFloat) {
                    vtq.V = DataValue.FromDouble(vtq.V.GetBool() ? 1 : 0);
                }
                else if (istate.Type.IsNumeric()) {
                    vtq.V = DataValue.FromInt(vtq.V.GetBool() ? 1 : 0);
                }
            }

            if (TypeIsFloat) {

                double? vv = vtq.V.AsDouble();
                if (!vv.HasValue) {

                    if (vtq.V.IsEmpty) {
                        return vtq;
                    }

                    string msg = $"Write value for data item {istate.Name} is not a float value: {vtq.V}";
                    Log_Warn("WriteValIsNotFloat", msg);
                    throw new Exception(msg);
                }

                double v = vv.Value;

                try {
                    v = istate.WriteConversion(v);
                }
                catch (Exception exp) {
                    string msg = $"Write conversion of data item {istate.Name} failed. Value: {vtq.V}";
                    Log_Warn_Details("WriteConversionFailed", msg, exp.Message);
                    throw new Exception(msg);
                }

                vtq.V = DataValue.FromDouble(v);
            }

            return vtq;
        }

        public override async Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {

            try {

                ObjectInfo info = mapObjectInfos[member.Object];
                string type = info.ClassName;

                string dataItemID = member.Object.LocalObjectID;
                if (type == typeof(Config.DataItem).FullName && member.Name == "Address") {
                    ItemState state = dataItemsState[dataItemID];
                    AdapterState adapter = state.Adapter;
                    if (adapter != null && adapter.Instance != null) {
                        string[] items = await adapter.Instance.BrowseDataItemAddress(dataItemID);
                        return new BrowseResult() {
                            HasMore = false,
                            Values = items.Select(DataValue.FromString).ToArray()
                        };
                    }
                }
                else if (type == typeof(Config.Adapter).FullName) {

                    if (member.Name == "Type") {
                        return new BrowseResult() {
                            HasMore = false,
                            Values = mapAdapterTypes.Keys.Select(DataValue.FromString).ToArray()
                        };
                    }
                    else if (member.Name == "Address") {
                        var adapter = adapters.FirstOrDefault(a => a.Config.ID == member.Object.LocalObjectID);
                        if (adapter != null && adapter.Instance != null) {
                            string[] items = await adapter.Instance.BrowseAdapterAddress();
                            return new BrowseResult() {
                                HasMore = false,
                                Values = items.Select(DataValue.FromString).ToArray()
                            };
                        }
                    }
                }
            }
            catch (Exception exp) {
                Console.Error.WriteLine($"Browsing of {member} failed: {exp.Message}");
            }

            return new BrowseResult();
        }

        private void NotifyQualityChange(List<ItemState> badItems, List<ItemState> goodItems) {
            if (badItems.Count == 1) {
                string msg = "Bad quality for reading data item: " + badItems[0].Name;
                var ev = AlarmOrEventInfo.Warning("Quality", msg, ObjectRef.Make(moduleID, badItems[0].ID));
                notifier?.Notify_AlarmOrEvent(ev);
            }
            else if (badItems.Count > 1) {
                string names = string.Join(", ", badItems.Select(it => it.Name));
                string msg = $"Bad quality for reading {badItems.Count} data items: {badItems[0].Name}, ...";
                ObjectRef[] objs = badItems.Select(it => ObjectRef.Make(moduleID, it.ID)).ToArray();
                var ev = AlarmOrEventInfo.Warning("Quality", msg, objs);
                ev.Details = names;
                notifier?.Notify_AlarmOrEvent(ev);
            }
            if (goodItems.Count == 1) {
                string msg = "Good quality restored for data item: " + goodItems[0].Name;
                var ev = AlarmOrEventInfo.RTN("Quality", msg, ObjectRef.Make(moduleID, goodItems[0].ID));
                notifier?.Notify_AlarmOrEvent(ev);
            }
            else if (goodItems.Count > 1) {
                string names = string.Join(", ", goodItems.Select(it => it.Name));
                string msg = $"Good quality restored for {goodItems.Count} data items: {goodItems[0].Name}, ... ";
                ObjectRef[] objs = goodItems.Select(it => ObjectRef.Make(moduleID, it.ID)).ToArray();
                var ev = AlarmOrEventInfo.RTN("Quality", msg, objs);
                ev.Details = names;
                notifier?.Notify_AlarmOrEvent(ev);
            }
        }

        // This will be called from a different Thread, therefore post it to the main thread!
        public void Notify_NeedRestart(string reason, Adapter adapter) {
            moduleThread?.Post(Do_Notify_NeedRestart, reason, adapter);
        }

        private void Do_Notify_NeedRestart(string reason, Adapter adapter) {
            AdapterState ast = adapters.FirstOrDefault(a => a.Config.ID == adapter.ID);
            Task ignored = RestartAdapter(ast, reason);
        }

        // This will be called from a different Thread, therefore post it to the main thread!
        public void Notify_DataItemsChanged(DataItemValue[] result) {
            moduleThread?.Post(Do_Notify_DataItemsChanged, result);
        }

        private readonly CompareDataItemValue compare = new CompareDataItemValue();

        private void Do_Notify_DataItemsChanged(DataItemValue[] result) {

            bool needSorting = false;
            for (int i = 0; i < result.Length - 1; ++i) {
                if (compare.Compare(result[i], result[i+1]) > 0) {
                    needSorting = true;
                    break;
                }
            }

            if (needSorting) {
                Array.Sort(result, compare);
            }

            var values = new List<VariableValue>(result.Length);
            foreach (DataItemValue val in result) {
                if (dataItemsState.ContainsKey(val.ID)) {
                    ItemState istate = dataItemsState[val.ID];
                    VTQ vtq = NormalizeReadValue(istate, val.Value);
                    istate.LastReadValue = vtq;
                    values.Add(VariableValue.Make(ObjectRef.Make(moduleID, val.ID), VariableName, vtq));
                }
                else {
                    Console.Error.WriteLine($"Notify_DataItemsChanged: Unknown DataItem ID: {val.ID}");
                }
            }
            if (values.Count > 0 && notifier != null) {
                notifier.Notify_VariableValuesChanged(values);
            }
        }

        class CompareDataItemValue : IComparer<DataItemValue>
        {
            public int Compare(DataItemValue x, DataItemValue y) {
                if (x.ID != y.ID) return x.ID.CompareTo(y.ID);
                return x.Value.T.CompareTo(y.Value.T);
            }
        }

        public override async Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {

            switch (methodName) {
                case "BrowseAllAdapterDataItems": {

                        try {
                            Task<AdapterBrowseInfo>[] tasks = adapters
                                .Where(a => a.Instance != null && a.State == State.Running)
                                .Select(a => BrowseAdapter(a.Config, a.Instance!))
                                .ToArray();

                            AdapterBrowseInfo[] x = await Task.WhenAll(tasks);

                            return Result<DataValue>.OK(DataValue.FromObject(x));
                        }
                        catch (Exception exp) {
                            Exception e = exp.GetBaseException() ?? exp;
                            return Result<DataValue>.Failure($"Exception while browsing: {e.Message}");
                        }
                    }
            }

            return await base.OnMethodCall(origin, methodName, parameters);
        }

        private async Task<AdapterBrowseInfo> BrowseAdapter(Config.Adapter config, AdapterBase adapter) {
            BrowseDataItemsResult res = await adapter.BrowseDataItems();
            return new AdapterBrowseInfo() {
                AdapterID = config.ID,
                SupportsBrowsing = res.SupportsBrowsing,
                BrowsingError = res.BrowsingError,
                ClientCertificate = res.ClientCertificate,
                DataItems = res.Items
            };
        }

        private void Log_Info(string type, string msg) {
            Log_Event(Severity.Info, type, msg);
        }

        private void Log_Error(string type, string msg, Origin? initiator = null) {
            Log_Event(Severity.Alarm, type, msg, initiator);
        }

        private void Log_Warn(string type, string msg, Origin? initiator = null) {
            Log_Event(Severity.Warning, type, msg, initiator);
        }

        private void Log_Warn_Details(string type, string msg, string details) {
            Log_Event(Severity.Warning, type, msg, null, details);
        }

        private void Log_Event(Severity severity, string type, string msg, Origin? initiator = null, string details = "") {

            var ae = new AlarmOrEventInfo() {
                Time = Timestamp.Now,
                Severity = severity,
                Type = type,
                Message = msg,
                Details = details,
                AffectedObjects = new ObjectRef[0],
                Initiator = initiator
            };

            notifier?.Notify_AlarmOrEvent(ae);
        }

        // This will be called from a different Thread, therefore post it to the main thread!
        public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo, Adapter adapter) {
            moduleThread?.Post(Do_Notify_AlarmOrEvent, eventInfo, adapter);
        }

        private void Do_Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo, Adapter adapter) {

            var ae = new AlarmOrEventInfo() {
                Time = eventInfo.Time,
                Severity = eventInfo.Severity,
                Type = eventInfo.Type,
                ReturnToNormal = eventInfo.ReturnToNormal,
                Message = adapter.Name + ": " + eventInfo.Message,
                Details = eventInfo.Details,
                AffectedObjects = eventInfo.AffectedDataItems.Select(di => ObjectRef.Make(moduleID, di)).ToArray(),
                Initiator = null
            };

            notifier?.Notify_AlarmOrEvent(ae);
        }

        class AdapterState
        {
            public AdapterState(Config.Adapter a) {
                SetConfig(a);
                State = State.Created;
            }

            public void CreateInstance(Dictionary<string, Type> mapAdapterTypes) {
                if (!mapAdapterTypes.ContainsKey(Config.Type)) {
                    throw new Exception($"No adapter type '{Config.Type}' found.");
                }
                Type type = mapAdapterTypes[Config.Type];
                AdapterBase? rawAdapter = (AdapterBase?)Activator.CreateInstance(type);
                if (rawAdapter == null) throw new Exception($"Failed to create instance of adapter type {type}");
                instance = new SingleThreadIOAdapter(rawAdapter);
                State = State.Created;
                SetOfPendingReadItems.Clear();
                SetOfPendingWriteItems.Clear();
                LastError = "";
                ItemGroups = new Group[0];
                MapItem2GroupID.Clear();
            }

            public void UpdateScheduledItems(Config.IO_Model model) {
                ScheduledDataItems = GetScheduledDataItems(model);
                NonStrictScheduledDataItems = new HashSet<string>(this.ScheduledDataItems.Where(sdi => sdi.UseTimestampFromSource).Select(sdi => sdi.DataItemID));
            }

            private ItemSchedule[] GetScheduledDataItems(Config.IO_Model model) {
                AdapterBase? instance = Instance;
                if (instance != null && instance.SupportsScheduledReading) {
                    List<Tuple<Config.DataItem, Config.Scheduling>> items = this.Config.GetAllDataItemsWithScheduling(model.Scheduling);
                    var res = new List<ItemSchedule>();
                    foreach (Tuple<Config.DataItem, Config.Scheduling> tp in items) {
                        Config.DataItem dataItem = tp.Item1;
                        Config.Scheduling scheduling = tp.Item2;
                        if (scheduling.Mode == IO.Config.SchedulingMode.Interval && scheduling.Interval.HasValue) {
                            res.Add(new ItemSchedule() {
                                DataItemID = dataItem.ID,
                                Interval = scheduling.Interval.Value,
                                Offset = scheduling.Offset ?? Duration.FromSeconds(0),
                                UseTimestampFromSource = scheduling.UseTimestampFromSource
                            });
                        }
                    }
                    return res.ToArray();
                }
                else {
                    return new ItemSchedule[0];
                }
            }

            public bool IsRestarting = false;

            public string Name => Config == null ? "?" : Config.Name;

            public State State { get; set; } = State.Created;

            private string originalConfig = "";
            private SingleThreadIOAdapter? instance;

            public Config.Adapter Config { get; private set; } = new Config.Adapter();

            public bool SetConfig(Config.Adapter newConfig) {
                string newOriginalConfig = Xml.ToXml<Config.Adapter>(newConfig);
                bool changed = (newOriginalConfig != originalConfig);
                Config = newConfig;
                originalConfig = newOriginalConfig;
                return changed;
            }

            public ItemSchedule[] ScheduledDataItems { get; set; } = new ItemSchedule[0];
            public HashSet<string> NonStrictScheduledDataItems { get; set; } = new HashSet<string>();

            public SingleThreadIOAdapter? Instance => instance;

            public void SetInstanceNull() {
                instance = null;
            }

            public Task? ScheduledReadingTask { get; set; }

            public string LastError { get; set; } = "";

            public readonly HashSet<string> SetOfPendingReadItems = new HashSet<string>();
            public readonly HashSet<string> SetOfPendingWriteItems = new HashSet<string>();

            private readonly Dictionary<string, string> MapItem2GroupID = new Dictionary<string, string>();
            private Group[] ItemGroups { get; set; } = new Group[0];

            public void SetGroups(Group[] groups) {
                MapItem2GroupID.Clear();
                foreach (Group group in groups) {
                    foreach (string id in group.DataItemIDs) {
                        MapItem2GroupID[id] = group.ID;
                    }
                }
                ItemGroups = groups.Length > 0 ? groups : new Group[] { new Group("", new string[0]) };
            }

            public IList<ReadTask> ReadItems(IList<ReadRequest> values, Duration? timeout) {

                Func<Task<VTQ[]>, IList<ReadRequest>, DataItemValue[]> f = (task, vals) => {
                    VTQ[] vtqs = task.Result;
                    if (vtqs.Length != vals.Count) throw new Exception("ReadDataItems returned wrong number of VTQs");
                    DataItemValue[] divalues = new DataItemValue[vtqs.Length];
                    for (int i = 0; i < vtqs.Length; ++i) {
                        divalues[i] = new DataItemValue(vals[i].ID, vtqs[i]);
                    }
                    return divalues;
                };

                var instance = Instance;
                if (instance == null) throw new Exception("ReadItems: Instance is null");

                if (ItemGroups.Length == 1 || values.Count == 1) {
                    string group = ItemGroups.Length == 1 ? ItemGroups[0].ID : MapItem2GroupID[values[0].ID];
                    Task<VTQ[]> t = instance.ReadDataItems(group, values, timeout);
                    Task<DataItemValue[]> tt = t.ContinueOnMainThread(t => f(t, values));
                    return new ReadTask[] { new ReadTask(tt, values.Select(x => x.ID).ToArray()) };
                }
                else {
                    return values.GroupBy(x => MapItem2GroupID[x.ID]).Select(group => {
                        ReadRequest[] items = group.ToArray();
                        Task<VTQ[]> t = instance.ReadDataItems(group.Key, items, timeout);
                        Task<DataItemValue[]> tt = t.ContinueOnMainThread(t => f(t, items));
                        return new ReadTask(tt, items.Select(x => x.ID).ToArray());
                    }).ToArray();
                }
            }

            public IList<WriteTask> WriteItems(IList<DataItemValue> values, Duration? timeout) {

                var instance = Instance;
                if (instance == null) throw new Exception("WriteItems: Instance is null");

                if (ItemGroups.Length == 1 || values.Count == 1) {
                    string group = ItemGroups.Length == 1 ? ItemGroups[0].ID : MapItem2GroupID[values[0].ID];
                    Task<WriteDataItemsResult> t = instance.WriteDataItems(group, values, timeout);
                    return new WriteTask[] { new WriteTask(t, values.Select(x => x.ID).ToArray()) };
                }
                else {
                    return values.GroupBy(x => MapItem2GroupID[x.ID]).Select(group => {
                        DataItemValue[] items = group.ToArray();
                        Task<WriteDataItemsResult> t = instance.WriteDataItems(group.Key, items, timeout);
                        return new WriteTask(t, items.Select(x => x.ID).ToArray());
                    }).ToArray();
                }
            }
        }

        struct ReadTask
        {
            public ReadTask(Task<DataItemValue[]> task, string[] ids) {
                Task = task;
                IDs = ids;
            }

            public Task<DataItemValue[]> Task { get; set; }
            public string[] IDs { get; set; }
        }

        struct WriteTask
        {
            public WriteTask(Task<WriteDataItemsResult> task, string[] ds) : this() {
                Task = task;
                IDs = ds;
            }

            public Task<WriteDataItemsResult> Task { get; set; }
            public string[] IDs { get; set; }
        }

        class ItemState
        {
            public ItemState(Config.DataItem item, AdapterState adapter, VTQ value, int? fractionalDigits) {
                ID = item.ID;
                Name = item.Name;
                Adapter = adapter;
                LastReadValue = value;
                Writeable = item.Write;
                FractionalDigits = fractionalDigits;
                Type = item.Type;
                Dimension = item.Dimension;
                ReadConversion = LinearFunctionParser.MakeConversion(item.ConversionRead);
                WriteConversion = LinearFunctionParser.MakeConversion(item.ConversionWrite);
                ReplacementValue = item.ReplacementValue;
                ReplacementQuality = item.ReplacementQuality;
            }

            public string ID { get; private set; }
            public string Name { get; private set; }
            public bool Writeable { get; private set; }
            public DataType Type { get; private set; }
            public int Dimension { get; private set; }
            public int? FractionalDigits { get; set; }
            public AdapterState Adapter { get; set; }
            public VTQ LastReadValue { get; set; }
            public Timestamp LastWritten { get; set; } = Timestamp.Empty;
            public VTQ LastWrittenValue { get; set; }
            public Func<double, double> ReadConversion { get; private set; }
            public Func<double, double> WriteConversion { get; private set; }
            public DataValue? ReplacementValue { get; private set; }
            public Quality? ReplacementQuality { get; private set; }
        }

        struct ItemSchedule
        {
            public string DataItemID { get; set; }
            public Duration Interval { get; set; }
            public Duration Offset { get; set; }
            public bool UseTimestampFromSource { get; set; }
        }

        enum State
        {
            Created,
            InitStarted,
            InitError,
            InitComplete,
            Running,
            ShutdownStarted,
            ShutdownCompleted
        }

        class Wrapper : AdapterCallback
        {
            private readonly Module m;
            private readonly Adapter a;

            public Wrapper(Module m, Adapter a) {
                this.m = m;
                this.a = a;
            }

            public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
                m.Notify_AlarmOrEvent(eventInfo, a);
            }

            public void Notify_DataItemsChanged(DataItemValue[] values) {
                m.Notify_DataItemsChanged(values);
            }

            public void Notify_NeedRestart(string reason) {
                m.Notify_NeedRestart(reason, a);
            }
        }
    }

    public class AdapterBrowseInfo
    {
        public string AdapterID { get; set; } = "";
        public bool SupportsBrowsing { get; set; } = false;
        public string BrowsingError { get; set; } = "";
        public string ClientCertificate { get; set; } = "";
        public DataItemBrowseInfo[] DataItems { get; set; } = new DataItemBrowseInfo[0];
    }
}
