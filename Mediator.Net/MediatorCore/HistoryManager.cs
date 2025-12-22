// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Timeseries;
using NLog;

namespace Ifak.Fast.Mediator;

public class HistoryManager
{
    public TimestampWarnMode TimestampCheckWarning { get; set; } = TimestampWarnMode.Always;

    private static readonly Logger logger = LogManager.GetLogger("HistoryManager");

    private readonly Dictionary<string, ModuleDBs> dbs = [];

    /// <summary>
    /// Tracks the last saved value per variable for proper deadband comparison.
    /// This prevents "deadband drift" where accumulated small changes are lost.
    /// </summary>
    private readonly Dictionary<VariableRef, DataValue> lastSavedForDeadband = [];

    private Func<VariableRef, Variable?> fVarResolver = (vr) => null;
    private Action<IList<HistoryChange>> fNotifyChanges = (changes) => { };

    private SynchronizationContext? syncContext = null;

    public async Task Start(Module[] modules, Func<VariableRef, Variable?> fVarResolver, Action<IList<HistoryChange>> fNotifyChanges, bool emptyDBs) {

        this.syncContext = SynchronizationContext.Current;
        this.fVarResolver = fVarResolver;
        this.fNotifyChanges = fNotifyChanges;

        foreach (Module m in modules) {

            var workers = new List<WorkerWithBuffer>();
            WorkerWithBuffer? defaultWorker = null;
            var variable2Worker = new Dictionary<string, WorkerWithBuffer>();

            foreach (HistoryDB db in m.HistoryDBs) {

                string dbt = db.Type.ToLowerInvariant();
                if (dbt != "sqlite" && dbt != "postgres" && dbt != "postgresflat") {
                    throw new Exception($"Unknown DB type '{db.Type}' in configuration of module " + m.ID);
                }

                Func<Timeseries.TimeSeriesDB> fCreateDB = () => throw new Exception($"Unknown DB type '{db.Type}' in configuration of module " + m.ID);
                if (dbt == "sqlite") {
                    fCreateDB = () => new Timeseries.SQLite.SQLiteTimeseriesDB();
                }
                else if (dbt == "postgres") {
                    fCreateDB = () => new Timeseries.Postgres.PostgresTimeseriesDB();
                }
                else if (dbt == "postgresflat") {
                    fCreateDB = () => new Timeseries.PostgresFlat.PostgresFlatTimeseriesDB();
                }

                if (emptyDBs) {
                    var d = fCreateDB();
                    d.ClearDatabase(new TimeSeriesDB.OpenParams(db.Name, db.ConnectionString, db.Settings));
                }

                Duration? retentionTime = null;
                if (!string.IsNullOrWhiteSpace(db.RetentionTime)) {
                    retentionTime = Duration.Parse(db.RetentionTime);
                }
                Duration retentionCheckInterval = Duration.Parse(db.RetentionCheckInterval);

                var worker = new HistoryDBWorker(db.Name, db.ConnectionString, db.Settings, db.PrioritizeReadRequests, db.AllowOutOfOrderAppend, retentionTime, retentionCheckInterval, fCreateDB, Notify_Append);
                var wb = new WorkerWithBuffer(worker);

                workers.Add(wb);

                if (db.Variables != null && db.Variables.Length > 0) {
                    foreach (string varName in db.Variables) {
                        variable2Worker[varName] = wb;
                    }
                }
                else {
                    if (defaultWorker == null) {
                        defaultWorker = wb;
                    }
                    else {
                        logger.Warn("More than one default DB worker for module " + m.ID);
                    }
                }

                await worker.Start();
            }

            if (defaultWorker == null && m.HistoryDBs.Count > 0) {
                logger.Warn("No default DB worker for module " + m.ID);
            }

            dbs[m.ID] = new ModuleDBs(m.ID, workers.ToArray(), variable2Worker, defaultWorker);
        }
    }

    private void Notify_Append(IEnumerable<VarHistoyChange> variables, bool allowOutOfOrder) {
        HistoryChangeType type = allowOutOfOrder ? HistoryChangeType.Upsert : HistoryChangeType.Append;
        var changes = variables.Select(v => new HistoryChange(v.Var, v.Start, v.End, type)).ToArray();
        syncContext?.Post(delegate (object? state) {
                fNotifyChanges(changes);
            }, null);
    }

    public Task Stop() {
        Task[] terminateTasks = dbs.Values.SelectMany(mDBs => mDBs.Workers.Select(w => w.Worker.Terminate()).ToArray()).ToArray();
        return Task.WhenAll(terminateTasks);
    }

    public async Task<List<VTTQ>> HistorianReadRaw(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);
        CheckExistingVariable(variable);
        return await worker.ReadRaw(variable, startInclusive, endInclusive, maxValues, bounding, filter);
    }

    public async Task<List<VTQ>> HistorianReadAggregatedIntervals(VariableRef variable, Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);
        CheckExistingVariable(variable);
        return await worker.ReadAggregatedIntervals(variable, intervalBounds, aggregation, filter);
    }

    public async Task<long> HistorianCount(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);
        CheckExistingVariable(variable);
        return await worker.Count(variable, startInclusive, endInclusive, filter);
    }

    public async Task<long> HistorianDeleteInterval(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);
        CheckExistingVariable(variable);
        long res = await worker.DeleteInterval(variable, startInclusive, endInclusive);
        NotifyChange(variable, startInclusive, endInclusive, HistoryChangeType.Delete);
        return res;
    }

    public async Task HistorianTruncate(VariableRef variable) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);
        CheckExistingVariable(variable);
        await worker.Truncate(variable);
        NotifyChange(variable, Timestamp.Empty, Timestamp.Max, HistoryChangeType.Delete);
    }

    public async Task<VTTQ?> HistorianGetLatestTimestampDb(VariableRef variable, Timestamp startInclusive, Timestamp endInclusive) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);
        CheckExistingVariable(variable);
        return await worker.GetLatestTimestampDb(variable, startInclusive, endInclusive);
    }

    public async Task HistorianModify(VariableRef variable, VTQ[] data, ModifyMode mode) {

        HistoryDBWorker worker = WorkerByVarRef(variable) ?? throw new Exception("Failed to find DB worker for variable " + variable);

        Variable varDesc = CheckExistingVariable(variable);
        await worker.Modify(variable, varDesc, data, mode);

        if (data.Length > 0) {
            Timestamp start = data.Min(x => x.T);
            Timestamp end   = data.Max(x => x.T);
            NotifyChange(variable, start, end, MapMode(mode));
        }
    }

    private static HistoryChangeType MapMode(ModifyMode mode) => mode switch {
        ModifyMode.Delete => HistoryChangeType.Delete,
        ModifyMode.Insert => HistoryChangeType.Insert,
        ModifyMode.Update => HistoryChangeType.Update,
        ModifyMode.Upsert => HistoryChangeType.Upsert,
        ModifyMode.ReplaceAll => HistoryChangeType.Mixed,
        _ => throw new Exception("Unknown modify mode: " + mode),
    };

    public async Task DeleteVariables(IList<VariableRef> variables) {

        var groups = variables.Select(v => Tuple.Create(WorkerByVarRef(v), v)).GroupBy(x => x.Item1).ToArray();

        Task[] tasks = groups.Select(group => {
            HistoryDBWorker? worker = group.Key;
            VariableRef[] variablesForWorker = group.Select(tp => tp.Item2).ToArray();
            if (worker == null) {
                if (variablesForWorker.Length == 1)
                    logger.Warn("Failed to find DB worker for variable: {0}", variablesForWorker[0]);
                else
                    logger.Warn("Failed to find DB worker for {0} variables: {1}, ...", variablesForWorker.Length, variablesForWorker[0]);
                return Task.FromResult(true);
            }
            return DeleteVariablesOfWorker(worker, variablesForWorker);
        }).ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task DeleteVariablesOfWorker(HistoryDBWorker worker, VariableRef[] variables) {
        foreach (VariableRef variable in variables) {
            await worker.Delete(variable);
            NotifyChange(variable, Timestamp.Empty, Timestamp.Max, HistoryChangeType.Delete);
        }
    }

    private void NotifyChange(VariableRef variable, Timestamp start, Timestamp end, HistoryChangeType changeType) {
        fNotifyChanges([new HistoryChange(variable, start, end, changeType)]);
    }

    private Variable CheckExistingVariable(VariableRef variable) {
        Variable? varDesc = fVarResolver(variable);
        if (varDesc != null) return varDesc;
        throw new Exception("Undefined variable: " + variable.ToString());
    }

    private HistoryDBWorker? WorkerByVarRef(VariableRef variable) {

        string moduleID = variable.Object.ModuleID;

        if (dbs.TryGetValue(moduleID, out ModuleDBs? moduleDBs)) {

            if (moduleDBs.Workers.Length == 1) {
                return moduleDBs.Workers[0].Worker;
            }
            else {
                string varName = variable.Name;
                if (moduleDBs.Variable2Worker.TryGetValue(varName, out WorkerWithBuffer? worker)) {
                    return worker.Worker;
                }
                else {
                    if (moduleDBs.DefaultWorker != null) {
                        return moduleDBs.DefaultWorker.Worker;
                    }
                    else {
                        return null;
                    }
                }
            }
        }
        else {
            return null;
        }
    }

    private bool ReportTimestampWarning(History history) {
        if (TimestampCheckWarning == TimestampWarnMode.Always) return true;
        if (TimestampCheckWarning == TimestampWarnMode.Never) return false;
        return history.Mode != HistoryMode.None;
    }

    public int OnVariableValuesChanged(string moduleID, IList<VariableValuePrev> values) {

        dbs.TryGetValue(moduleID, out ModuleDBs? moduleDBs);

        bool oneDbWorker = moduleDBs != null && moduleDBs.Workers.Length == 1;
        bool doAllowOutOfOrder = oneDbWorker && moduleDBs!.Workers[0].Worker.AllowOutOfOrderAppend;

        bool AllowOutOfOrder(in VariableValue v) {
            if (oneDbWorker) return doAllowOutOfOrder;
            if (moduleDBs == null) return false;
            WorkerWithBuffer? worker = moduleDBs.Variable2Worker.GetValueOrDefault(v.Variable.Name);
            if (worker != null) return worker.Worker.AllowOutOfOrderAppend;
            return moduleDBs.DefaultWorker?.Worker.AllowOutOfOrderAppend ?? false;
        }

        var valuesToSave = new List<StoreValue>();

        for (int i = 0; i < values.Count; ++i) {

            VariableValue value = values[i].Value;
            VTQ previousValue = values[i].PreviousValue;

            Variable? variable = fVarResolver(value.Variable);
            if (variable == null) {
                logger.Warn("OnVariableValuesChanged: invalid variable reference: " + value.Variable);
                continue;
            }

            Timestamp tNew = value.Value.T;
            Timestamp tOld = previousValue.T;
            History history = variable.History;
            double? deadband = variable.Deadband ?? history.Deadband;

            bool preventOutOfOrderAppend = !AllowOutOfOrder(value);

            if (preventOutOfOrderAppend && tNew < tOld) {
                if (ReportTimestampWarning(history)) {
                    logger.Warn("Timestamp of new VTQ is older than current timestamp: " + value.Variable.ToString() + "\n\tOld: " + previousValue + "\n\tNew: " + value.Value);
                }
            }
            else if (preventOutOfOrderAppend && tNew == tOld) {
                if (value.Value != previousValue) {
                    if (ReportTimestampWarning(history)) {
                        logger.Warn("Timestamp of new VTQ is equal to current timestamp but value (or quality) differs: " + value.Variable.ToString() + "\n\tOld: " + previousValue + "\n\tNew: " + value.Value);
                    }
                }
            }
            else {
                DataType type = variable.Type;

                switch (history.Mode) {
                    case HistoryMode.None:
                        break;

                    case HistoryMode.Complete:
                            valuesToSave.Add(new StoreValue(value, type));
                            break;

                    case HistoryMode.ValueOrQualityChanged: {
                            // Compare against last saved value (not last observed) for proper deadband behavior
                            DataValue compareValue = GetLastSavedOrPreviousValue(value.Variable, previousValue.V);
                            if (value.Value.Q != previousValue.Q ||
                                HasValueChanged(value.Value.V, compareValue, type, deadband)) {
                                valuesToSave.Add(new StoreValue(value, type));
                            }
                            break;
                        }

                    case HistoryMode.Interval: {
                            if (IsIntervalHit(tNew, history) ||
                                (tNew - tOld >= history.Interval) ||
                                IsIntervalBetweenTimetamps(tOld, tNew, history)) {

                                valuesToSave.Add(new StoreValue(value, type));
                            }
                            break;
                        }

                    case HistoryMode.IntervalOrChanged: {
                            // Compare against last saved value (not last observed) for proper deadband behavior
                            DataValue compareValue = GetLastSavedOrPreviousValue(value.Variable, previousValue.V);
                            if (value.Value.Q != previousValue.Q ||
                                HasValueChanged(value.Value.V, compareValue, type, deadband) ||
                                IsIntervalHit(tNew, history) ||
                                (tNew - tOld >= history.Interval) ||
                                IsIntervalBetweenTimetamps(tOld, tNew, history)) {

                                valuesToSave.Add(new StoreValue(value, type));
                            }
                            break;
                        }

                    case HistoryMode.IntervalExact: {
                            if (IsIntervalHit(tNew, history)) {
                                valuesToSave.Add(new StoreValue(value, type));
                            }
                            break;
                        }

                    case HistoryMode.IntervalExactOrChanged: {
                            // Compare against last saved value (not last observed) for proper deadband behavior
                            DataValue compareValue = GetLastSavedOrPreviousValue(value.Variable, previousValue.V);
                            if (value.Value.Q != previousValue.Q ||
                                HasValueChanged(value.Value.V, compareValue, type, deadband) ||
                                IsIntervalHit(tNew, history)) {
                                valuesToSave.Add(new StoreValue(value, type));
                            }
                            break;
                        }

                    default:
                        logger.Error("Unknown history mode: " + history.Mode);
                        break;
                }
            }
        }

        if (valuesToSave.Count > 0) {
            int result = SaveToDB(moduleDBs, moduleID, valuesToSave);
            // Update last saved values for deadband tracking
            UpdateLastSavedForDeadband(valuesToSave);
            return result;
        }
        else {
            return 0;
        }
    }

    /// <summary>
    /// Gets the value to compare against for change detection.
    /// Returns the last saved value if available, otherwise falls back to the previous observed value.
    /// This ensures proper deadband behavior by always comparing against the last actually saved value.
    /// </summary>
    private DataValue GetLastSavedOrPreviousValue(VariableRef varRef, DataValue previousObservedValue) {
        if (lastSavedForDeadband.TryGetValue(varRef, out DataValue lastSaved)) {
            return lastSaved;
        }
        return previousObservedValue;
    }

    /// <summary>
    /// Updates the last saved value dictionary after successfully saving values.
    /// This ensures proper deadband comparison against the last actually saved value.
    /// </summary>
    private void UpdateLastSavedForDeadband(IList<StoreValue> savedValues) {
        foreach (StoreValue sv in savedValues) {
            lastSavedForDeadband[sv.Value.Variable] = sv.Value.Value.V;
        }
    }

    /// <summary>
    /// Checks if a value has changed, considering the deadband for numeric types.
    /// Quality changes should be checked separately before calling this method.
    /// </summary>
    /// <param name="newValue">The new value</param>
    /// <param name="oldValue">The previous value</param>
    /// <param name="type">The data type of the variable</param>
    /// <param name="deadband">Optional absolute deadband threshold</param>
    /// <returns>True if the value has changed (beyond the deadband for numeric types)</returns>
    private static bool HasValueChanged(DataValue newValue, DataValue oldValue, DataType type, double? deadband) {
        // If values are exactly equal, no change
        if (newValue == oldValue) return false;

        // If deadband is specified and type is numeric, apply deadband logic
        if (deadband.HasValue && type.IsNumeric()) {
            double? newDouble = newValue.AsDouble();
            double? oldDouble = oldValue.AsDouble();

            if (newDouble.HasValue && oldDouble.HasValue) {
                // Handle NaN: if either value is NaN, consider it a change
                // (unless both are NaN, which would have been caught by the JSON string equality check above)
                return 
                    double.IsNaN(newDouble.Value) || 
                    double.IsNaN(oldDouble.Value) || 
                    Math.Abs(newDouble.Value - oldDouble.Value) > deadband.Value;
            }
        }

        // For non-numeric types or when deadband doesn't apply, values are different
        return true;
    }

    private static bool IsIntervalBetweenTimetamps(Timestamp tLeft, Timestamp tRight, History history) {

        if (!history.Interval.HasValue) return false;

        long intervalMS = history.Interval.Value.TotalMilliseconds;
        long offMS = history.Offset.HasValue ? history.Offset.Value.TotalMilliseconds : 0;

        long intervals = 1 + (tLeft.JavaTicks - offMS) / intervalMS;
        Timestamp tNext = Timestamp.FromJavaTicks(intervals * intervalMS + offMS);

        return tNext < tRight;
    }

    private static bool IsIntervalHit(Timestamp t, History history) {

        if (!history.Interval.HasValue) return false;

        long intervalMS = history.Interval.Value.TotalMilliseconds;
        long offMS = history.Offset.HasValue ? history.Offset.Value.TotalMilliseconds : 0;

        return (t.JavaTicks - offMS) % intervalMS == 0;
    }

    private static int SaveToDB(ModuleDBs? moduleDBs, string moduleID, IList<StoreValue> values) {

        if (moduleDBs != null) {

            if (moduleDBs.Workers.Length == 1) {
                return moduleDBs.Workers[0].Worker.Append(values);
            }
            else {

                foreach (StoreValue v in values) {
                    string varName = v.Value.Variable.Name;
                    if (moduleDBs.Variable2Worker.TryGetValue(varName, out WorkerWithBuffer? worker)) {
                        worker.Buffer.Add(v);
                    }
                    else {
                        if (moduleDBs.DefaultWorker != null) {
                            moduleDBs.DefaultWorker.Buffer.Add(v);
                        }
                        else {
                            logger.Warn("Failed to store value because of missing default DB worker: " + v.Value.Variable);
                        }
                    }
                }

                int maxBufferCount = 0;
                foreach (WorkerWithBuffer wb in moduleDBs.Workers) {
                    if (wb.Buffer.Count > 0) {
                        int count = wb.Worker.Append(wb.Buffer.ToArray());
                        maxBufferCount = Math.Max(maxBufferCount, count);
                        wb.Buffer.Clear();
                    }
                }
                return maxBufferCount;
            }
        }
        else {
            logger.Warn("No DB worker for modules " + moduleID);
            return 0;
        }
    }

    private sealed class ModuleDBs(string moduleID, WorkerWithBuffer[] workers, Dictionary<string, WorkerWithBuffer> variable2Worker, WorkerWithBuffer? defaultWorker) {
        public string ModuleID { get; private set; } = moduleID;
        public WorkerWithBuffer[] Workers { get; private set; } = workers;
        public Dictionary<string, WorkerWithBuffer> Variable2Worker { get; private set; } = variable2Worker;
        public WorkerWithBuffer? DefaultWorker { get; private set; } = defaultWorker;
    }

    private sealed class WorkerWithBuffer(HistoryDBWorker worker) {
        public HistoryDBWorker Worker { get; private set; } = worker;
        public List<StoreValue> Buffer { get; private set; } = [];
    }
}

public struct StoreValue(VariableValue value, DataType type) {
    public VariableValue Value { get; private set; } = value;
    public DataType Type { get; private set; } = type;
}
