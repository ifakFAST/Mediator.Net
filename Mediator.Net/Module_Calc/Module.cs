// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;
using VariableRefs = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableRef>;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Calc;

public class Module : ModelObjectModule<Config.Calc_Model>
{
    public Func<string, Type[]> fLoadCalcTypesFromAssembly = (s) => [];

    private readonly List<CalcInstance> adapters = [];
    private readonly Dictionary<string, Type> mapAdapterTypes = [];
    private readonly List<AdapterInfo> adapterTypesAttribute = [];
    private ModuleInitInfo initInfo;
    private ModuleThread? moduleThread = null;
    private Connection connection = new ClosedConnection();
    private Mediator.Config moduleConfig = new(Array.Empty<NamedValue>());
    private bool moduleShutdown = false;

    public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {

        this.moduleThread = moduleThread;
        this.initInfo = info;
        this.moduleConfig = info.GetConfigReader();

        await base.Init(info, restoreVariableValues, notifier, moduleThread);

        string strAssemblies = moduleConfig.GetOptionalString("adapter-assemblies", "");

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
            if (!File.Exists(assembly)) throw new Exception($"calculation-assembly does not exist: {assembly}");
        }

        var adapterTypes = Reflect.GetAllNonAbstractSubclasses(typeof(CalculationBase)).ToList();
        adapterTypes.AddRange(absoluteAssemblies.SelectMany(fLoadCalcTypesFromAssembly));

        adapterTypesAttribute.Clear();
        mapAdapterTypes.Clear();
        foreach (Type type in adapterTypes) {
            Identify? id = type.GetCustomAttribute<Identify>();
            if (id != null) {
                mapAdapterTypes[id.ID] = type;
                adapterTypesAttribute.Add(AdapterInfoFromIdentify(id));
            }
        }

        foreach (CalcInstance adapter in adapters) {
            adapter.CreateInstance(mapAdapterTypes, initInfo);
        }

        Dictionary<VariableRef, VTQ> varMap = restoreVariableValues.ToDictionary(v => v.Variable, v => v.Value);
        foreach (CalcInstance adapter in adapters) {
            adapter.SetInitialOutputValues(varMap);
            adapter.SetInitialStateValues(varMap);
        }

        try {
            Task[] initTasks = adapters.Select(a => InitAdapter(a, InitContext.Init)).ToArray();
            await Task.WhenAll(initTasks);
        }
        catch (Exception exp) {

            string[] failedAdapters = adapters
                .Where(a => a.State == State.InitError)
                .Select(a => "Init of calculation '" + a.CalcConfig.Name + "' failed: " + a.LastError)
                .ToArray();

            string errMessage = failedAdapters.Length > 0 ? string.Join("; ", failedAdapters) : exp.Message;
            Console.Error.WriteLine(errMessage);
            await Shutdown();
            throw new Exception(errMessage);
        }
    }

    private static AdapterInfo AdapterInfoFromIdentify(Identify att) {
        return new AdapterInfo {
            Type = att.ID,
            Show_WindowVisible = att.Show_WindowVisible,
            Show_Definition = att.Show_Definition,
            DefinitionLabel = att.DefinitionLabel,
            DefinitionIsCode = att.DefinitionIsCode,
            CodeLang = att.CodeLang,
            Subtypes = att.Subtypes,
        };
    }

    protected override async Task OnConfigModelChanged(bool init) {

        await base.OnConfigModelChanged(init);

        // model.ValidateOrThrow();
        model.Normalize(adapterTypesAttribute);

        Config.Calculation[] enabledAdapters = model.GetAllCalculations()
            .Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Type) && !string.IsNullOrWhiteSpace(a.Definition))
            .ToArray();

        CalcInstance[] removedAdapters = adapters
            .Where(a => !enabledAdapters.Any(x => x.ID == a.CalcConfig.ID))
            .ToArray();

        CalcInstance[] newAdapters = enabledAdapters
            .Where(x => !adapters.Any(a => x.ID == a.CalcConfig.ID))
            .Select(a => new CalcInstance(a, moduleID))
            .ToArray();

        adapters.RemoveAll(removedAdapters);
        adapters.AddRange(newAdapters);

        var restartAdapters = new List<CalcInstance>();
        // var inputVars = new HashSet<VariableRef>();

        foreach (CalcInstance adapter in adapters) {

            Config.Calculation config = enabledAdapters.First(x => x.ID == adapter.CalcConfig.ID);
            bool changed = adapter.SetConfig(config);
            if (changed) {
                restartAdapters.Add(adapter);
            }

            //foreach (Config.Input input in adapter.Config.Inputs) {
            //    if (input.Variable.HasValue) {
            //        inputVars.Add(input.Variable.Value);
            //    }
            //}
        }

        if (init == false) {

            if (removedAdapters.Length > 0) {
                await ShutdownAdapters(removedAdapters);
            }

            if (newAdapters.Length > 0) {

                foreach (CalcInstance adapter in newAdapters) {
                    adapter.CreateInstance(mapAdapterTypes, initInfo);
                }

                foreach (CalcInstance adapter in newAdapters) {
                    var variables = new VariableRefs();
                    variables.AddRange(adapter.CalcConfig.States.Select(s => adapter.GetStateVarRef(s.ID)));
                    variables.AddRange(adapter.CalcConfig.Outputs.Select(o => adapter.GetOutputVarRef(o.ID)));
                    VariableValues varValues = await connection.ReadVariablesIgnoreMissing(variables);
                    Dictionary<VariableRef, VTQ> varMap = varValues.ToDictionary(v => v.Variable, v => v.Value);
                    adapter.SetInitialStateValues(varMap);
                    adapter.SetInitialOutputValues(varMap);
                }

                Task[] initTasks = newAdapters.Select(a => InitAdapter(a, InitContext.ConfigChanged)).ToArray();
                await Task.WhenAll(initTasks);

                foreach (CalcInstance adapter in newAdapters) {
                    StartRunLoopTaskIfInitCompleted(adapter);
                }
            }

            if (restartAdapters.Count > 0) {
                Task[] restartTasks = restartAdapters.Select(a => RestartAdapter(a, "Config changed", critical: false)).ToArray();
                await Task.WhenAll(restartTasks);
            }
        }

        // await connection.DisableChangeEvents();
        // await connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), inputVars.ToArray());
    }

    enum InitContext { Init, ConfigChanged, Restart }

    private async Task InitAdapter(CalcInstance adapter, InitContext context) {
        if (adapter.Instance == null) {
            throw new Exception("InitAdapter: instance is null");
        }
        Calculation info = adapter.CalcConfig.ToCalculation();
        try {
            var initParams = new InitParameter() {
                Calculation = info,
                LastOutput = adapter.LastOutputValues,
                LastState = adapter.LastStateValues,
                ConfigFolder = Path.GetDirectoryName(base.modelFileName) ?? "",
                DataFolder = initInfo.DataFolder,
                ModuleID = initInfo.ModuleID,
                ModuleConfig = moduleConfig.ToNamedValues()
            };
            adapter.State = State.InitStarted;
            InitResult res = await adapter.Instance.Initialize(initParams, new Wrapper(this, info));
            if (adapter.State == State.InitStarted) {

                Config.Input[] newInputs = res.Inputs.Select(ip => MakeInput(ip, adapter.CalcConfig)).ToArray();
                Config.Output[] newOutputs = res.Outputs.Select(ip => MakeOutput(ip, adapter.CalcConfig)).ToArray();
                Config.State[] newStates = res.States.Select(MakeState).ToArray();

                bool inputsChanged = !StdJson.ObjectsDeepEqual(adapter.CalcConfig.Inputs, newInputs);
                bool outputsChanged = !StdJson.ObjectsDeepEqual(adapter.CalcConfig.Outputs, newOutputs);
                bool statesChanged = !StdJson.ObjectsDeepEqual(adapter.CalcConfig.States, newStates);

                var changedMembers = new List<MemberValue>(2);

                if (inputsChanged) {
                    changedMembers.Add(MemberValue.Make(moduleID, adapter.CalcConfig.ID, "Inputs", DataValue.FromObject(newInputs)));
                }

                if (outputsChanged) {
                    changedMembers.Add(MemberValue.Make(moduleID, adapter.CalcConfig.ID, "Outputs", DataValue.FromObject(newOutputs)));
                }

                if (statesChanged) {
                    changedMembers.Add(MemberValue.Make(moduleID, adapter.CalcConfig.ID, "States", DataValue.FromObject(newStates)));
                }

                if (changedMembers.Count > 0) {
                    Result result = await UpdateConfig(GetModuleOrigin(),
                        updateOrDeleteObjects: Array.Empty<ObjectValue>(),
                        updateOrDeleteMembers: changedMembers.ToArray(),
                        addArrayElements: Array.Empty<AddArrayElement>());
                    if (result.Failed()) {
                        throw new Exception(result.Error!);
                    }
                }

                adapter.State = State.InitComplete;
                Log_ReturnToNormal("CalcInit", $"Init of calculation {info.Name} completed successfully.", [ adapter.ID ]);
            }
        }
        catch (Exception e) {

            Exception exp = e.GetBaseException() ?? e;
            adapter.State = State.InitError;
            adapter.LastError = exp.Message;

            bool init = context == InitContext.Init;
            bool failOnInitError = adapter.CalcConfig.InitErrorResponse == Config.InitErrorResponse.Fail;

            bool throwException = !init || failOnInitError;

            if (throwException) {
                throw new Exception($"Initialize of calculation {info.Name} failed: " + exp.Message, exp);
            }
            
            bool retry = adapter.CalcConfig.InitErrorResponse == Config.InitErrorResponse.Retry;
            if (retry) {
                Task _ = WaitAndRetry(adapter, "Init failed: " + exp.Message);
                return;
            }

            Log_ErrorDetails("CalcInit", $"Init of calculation {info.Name} failed (no retry): {exp.Message}", exp.StackTrace ?? "", [ adapter.ID ]);
        }
    }

    private async Task WaitAndRetry(CalcInstance adapter, string reason) {
        await Task.Delay(5000);
        await RestartAdapter(adapter, reason);
    }

    private static Config.Input MakeInput(InputDef input, Config.Calculation calc) {
        Config.Input? old = calc.Inputs.FirstOrDefault(x => x.ID == input.ID);
        return new Config.Input() {
            ID = input.ID,
            Name = input.Name,
            Unit = input.Unit,
            Dimension = input.Dimension,
            Type = input.Type,
            Constant = old != null ? old.Constant : input.DefaultValue,
            Variable = old != null ? old.Variable : input.DefaultVariable,
        };
    }

    private static Config.Output MakeOutput(OutputDef output, Config.Calculation calc) {
        Config.Output? old = calc.Outputs.FirstOrDefault(x => x.ID == output.ID);
        return new Config.Output() {
            ID = output.ID,
            Name = output.Name,
            Unit = output.Unit,
            Dimension = output.Dimension,
            Type = output.Type,
            Variable = old?.Variable,
        };
    }

    private static Config.State MakeState(StateDef output) {
        return new Config.State() {
            ID = output.ID,
            Name = output.Name,
            Unit = output.Unit,
            Dimension = output.Dimension,
            Type = output.Type,
            DefaultValue = output.DefaultValue.HasValue ? output.DefaultValue.Value.JSON : null,
        };
    }

    private async Task RestartAdapter(CalcInstance adapter, string reason, bool critical = true, int tryCounter = 0) {

        if (adapter.IsRestarting && tryCounter == 0) { return; }
        adapter.IsRestarting = true;

        if (critical) {
            if (tryCounter == 0)
                Log_Warn("CalcRestart", $"Restarting calculation {adapter.Name}. Reason: {reason}");
            else
                Log_Warn("CalcRestart", $"Restarting calculation {adapter.Name} (retry {tryCounter}). Reason: {reason}");
        }
        else {
            Log_Info("CalcRestart", $"Restarting calculation {adapter.Name}. Reason: {reason}");
        }

        const int TimeoutSeconds = 10;
        try {
            Task shutdown = ShutdownAdapter(adapter);
            Task t = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
            if (t != shutdown) {
                Log_Warn("CalcShutdownTimeout", $"Shutdown request for calculation {adapter.Name} failed to complete within {TimeoutSeconds} seconds.");
                // go ahead and hope for the best...
            }
            adapter.CreateInstance(mapAdapterTypes, initInfo);
            await InitAdapter(adapter, InitContext.Restart);
            StartRunLoopTaskIfInitCompleted(adapter);
            adapter.IsRestarting = false;
        }
        catch (Exception exception) {
            Exception exp = exception.GetBaseException();
            string errMsg = $"Restart of calculation {adapter.Name} failed: {exp.Message}";

            if (critical) {
                Log_Error("CalcRestartError", errMsg);
                // Thread.Sleep(500);
                // Environment.Exit(1); // will result in restart of entire module by Mediator
                TimeSpan delay = BoundDuration(
                    duration: TimeSpan.FromSeconds(5 + 2 * tryCounter), 
                    min: TimeSpan.FromSeconds(5), 
                    max: TimeSpan.FromSeconds(60));
                await Task.Delay(delay);
                Task _ = RestartAdapter(adapter, exp.Message, critical, tryCounter + 1);
            }
            else {
                Log_Info("CalcRestartFailed", errMsg);
                adapter.IsRestarting = false;
                throw new Exception(errMsg);
            }
        }
    }

    private static TimeSpan BoundDuration(TimeSpan duration, TimeSpan min, TimeSpan max) {
        if (duration < min) return min;
        if (duration > max) return max;
        return duration;
    }

    private Task Shutdown() {
        moduleShutdown = true;
        return ShutdownAdapters(adapters);
    }

    private async Task ShutdownAdapters(IEnumerable<CalcInstance> adapters) {

        Task[] shutdownTasks = adapters
            .Where(a => a.State == State.InitStarted || a.State == State.InitComplete || a.State == State.Running)
            .Select(ShutdownAdapter)
            .ToArray();

        await Task.WhenAll(shutdownTasks);
    }

    private async Task ShutdownAdapter(CalcInstance adapter) {
        adapter.State = State.ShutdownStarted;

        // Console.WriteLine($"Shutting down calculation {adapter.Name} with SignalStepAbort...");
        adapter.Instance?.SignalStepAbort();
        try {

            while (adapter.RunLoopRunning) {
                await Task.Delay(50);
            }

            var instance = adapter.Instance;
            if (instance == null) {
                Log_Warn("CalcShutdownError", "ShutdownCalc: Instance is null");
            }
            else {
                await instance.Shutdown();
            }
        }
        catch (Exception e) {
            Exception exp = e.GetBaseException() ?? e;
            Log_Warn("CalcShutdownError", "ShutdownCalc exception: " + exp.Message);
        }
        adapter.State = State.ShutdownCompleted;
        adapter.SetInstanceNull();
    }

    private bool running = false;

    public override async Task Run(Func<bool> shutdown) {

        this.connection = await HttpConnection.ConnectWithModuleLogin(initInfo);

        running = true;

        _ = KeepSessionAlive();
        _ = StartCheckForModelFileModificationTask(shutdown);

        foreach (CalcInstance a in adapters) {
            StartRunLoopTaskIfInitCompleted(a);
        }

        while (!shutdown()) {
            await Task.Delay(100);
        }

        running = false;

        await Shutdown();
    }

    private async Task KeepSessionAlive() {

        while (running) {

            await Task.Delay(TimeSpan.FromMinutes(15));

            if (running) {

                try {
                    await connection.Ping();
                }
                catch (Exception) { }
            }
        }
    }

    public override Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {

        if (methodName == "GetAdapterInfo") {
            DataValue dv = DataValue.FromObject(adapterTypesAttribute);
            return Task.FromResult(Result<DataValue>.OK(dv));

        }
        else {
            return base.OnMethodCall(origin, methodName, parameters);
        }
    }

    private void StartRunLoopTaskIfInitCompleted(CalcInstance adapter) {

        if (adapter.State != State.InitComplete) {
            return;
        }

        Task readTask = AdapterRunLoopTask(adapter);
        adapter.RunLoopTask = readTask;
        var ignored1 = readTask.ContinueOnMainThread(t => {

            if (t.IsFaulted && !moduleShutdown && adapter.State == State.Running) {
                Exception exp = t.Exception!.GetBaseException() ?? t.Exception;
                Task ignored2 = RestartAdapter(adapter, "Run loop exception: " + exp.Message + "\n" + exp.StackTrace, critical: true);
            }
        });
    }

    interface ICalcRunCondition {
        Task<(Timestamp, Duration)> WaitForFirstRun();
        Task<(Timestamp, Duration)> WaitForNextRun();
    }

    sealed class ContinousRunCondition : ICalcRunCondition {

        private readonly Module module;
        private readonly CalcInstance adapter;
        private readonly bool ignoreOffsetForTimestamps;
        private readonly Duration scaledCycle;
        private readonly Duration cycle;
        private readonly Duration offset;
        private Timestamp t;
        private Timestamp t0;

        public ContinousRunCondition(CalcInstance adapter, Module module) {
            this.module = module;
            this.adapter = adapter;
            this.scaledCycle = adapter.ScaledCycle();
            this.cycle = adapter.CalcConfig.Cycle;
            this.offset = adapter.ScaledOffset();
            this.t = Time.GetNextNormalizedTimestamp(scaledCycle, offset);
            this.ignoreOffsetForTimestamps = adapter.CalcConfig.IgnoreOffsetForTimestamps;
            this.t0 = ignoreOffsetForTimestamps ? t - offset : t;
        }

        public async Task<(Timestamp, Duration)> WaitForFirstRun() {
            await adapter.WaitUntil(t);
            var dt = cycle; // we must not use the scaledCycle!
            return (t0, dt);
        }

        public async Task<(Timestamp, Duration)> WaitForNextRun() {
            var prevT0 = t0;
            t = module.GetNextNormalizedTimestamp(t, scaledCycle, offset, adapter);
            t0 = ignoreOffsetForTimestamps ? t - offset : t;
            await adapter.WaitUntil(t);
            var dt = t0 - prevT0;
            return (t0, dt);
        }
    }

    sealed class TriggeredRunCondition : ICalcRunCondition {

        private readonly Module module;
        private readonly CalcInstance adapter;
        private Timestamp t0 = Timestamp.Empty;

        public TriggeredRunCondition(CalcInstance adapter, Module module) {
            this.module = module;
            this.adapter = adapter;
        }

        public async Task<(Timestamp, Duration)> WaitForFirstRun() {
            var (t, dt) = await adapter.WaitUntilTriggered();
            t0 = t ?? Timestamp.Empty;
            var dt0 = dt ?? adapter.CalcConfig.Cycle;
            return (t0, dt0);
        }

        public async Task<(Timestamp, Duration)> WaitForNextRun() {

            while (true) {

                var (tOpt, dtOpt) = await adapter.WaitUntilTriggered();

                if (tOpt == null) {
                    return (Timestamp.Empty, Duration.Zero);
                }

                Timestamp t = tOpt.Value;

                if (t <= t0) {
                    ObjectRef[] calcID = new ObjectRef[] { adapter.ID };
                    string msg = $"Calculation {adapter.Name} was triggered with timestamp {t} but last timestamp was {t0}.";
                    module.Log_Warn("TriggerTime", msg, affectedObjects: calcID);
                    continue;
                }

                var dt0 = dtOpt ?? t - t0;
                t0 = t;
                
                return (t0, dt0);
            }
        }
    }

    private async Task AdapterRunLoopTask(CalcInstance adapter) {

        adapter.State = State.Running;
        string moduleID = base.moduleID;

        ICalcRunCondition runCondition = adapter.CalcConfig.RunMode switch {
            Config.RunMode.Continuous => new ContinousRunCondition(adapter, this),
            Config.RunMode.Triggered  => new TriggeredRunCondition(adapter, this),
            _ => throw new Exception("Invalid run mode: " + adapter.CalcConfig.RunMode)
        };

        var (t, dt) = await runCondition.WaitForFirstRun();

        var inputs = new List<Config.Input>();
        var inputVars = new VariableRefs();

        var notifier = this.notifier!;

        //var listVarValueTimer = new List<VariableValue>(1);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (adapter.State == State.Running) {

            sw.Restart();

            inputs.Clear();
            inputVars.Clear();
            foreach (Config.Input inp in adapter.CalcConfig.Inputs) {
                if (inp.Variable.HasValue) {
                    inputs.Add(inp);
                    inputVars.Add(inp.Variable.Value);
                }
            }

            // Config.Input[] inputs = adapter.CalcConfig.Inputs.Where(inp => inp.Variable.HasValue).ToArray();
            // VariableRef[] inputVars = inputs.Select(inp => inp.Variable.Value).ToArray();

            VTQs values = await ReadInputVars(adapter, inputs, inputVars, t);

            // sw.Stop();
            // double dd = sw.ElapsedTicks;
            //if (dd > 100) {
            //    Console.Error.WriteLine(dd / TimeSpan.TicksPerMillisecond + " ms");
            //}

            adapter.UpdateInputValues(inputVars, values);

            InputValue[] inputValues = adapter.CurrentInputValues(t);

            VariableValues inValues = inputValues.Select(v => VariableValue.Make(adapter.GetInputVarRef(v.InputID), v.Value.WithTime(t))).ToList();
            notifier.Notify_VariableValuesChanged(inValues);

            var instance = adapter.Instance;
            if (instance == null || adapter.State != State.Running) {
                break;
            }

            StepResult result = await instance.Step(t, dt, inputValues);

            OutputValue[] outValues = result.Output ?? [];
            StateValue[] stateValues = result.State ?? [];

            // Console.WriteLine($"{Timestamp.Now}: out: " + StdJson.ObjectToString(outValues));
            var listVarValues = new VariableValues(outValues.Length + stateValues.Length + 2);
            foreach (OutputValue v in outValues) {
                var vv = VariableValue.Make(adapter.GetOutputVarRef(v.OutputID), v.Value);
                listVarValues.Add(vv);
            }
            foreach (StateValue v in stateValues) {
                var vv = VariableValue.Make(adapter.GetStateVarRef(v.StateID), VTQ.Make(v.Value, t, Quality.Good));
                listVarValues.Add(vv);
            }

            var outputDest = new VariableValues();
            foreach (Config.Output ot in adapter.CalcConfig.Outputs) {
                if (ot.Variable.HasValue) {
                    int idx = outValues.FindIndex(o => o.OutputID == ot.ID);
                    if (idx > -1) {
                        outputDest.Add(VariableValue.Make(ot.Variable.Value, outValues[idx].Value));
                    }
                }
            }

            sw.Stop();
            var varLastRunDuration = VariableValue.Make(adapter.GetLastRunDurationVarRef(), VTQ.Make(sw.ElapsedMilliseconds, t, Quality.Good));
            listVarValues.Add(varLastRunDuration);

            var varLastRunTimestamp = VariableValue.Make(adapter.GetLastRunTimestampVarRef(), VTQ.Make(t, t, Quality.Good));
            listVarValues.Add(varLastRunTimestamp);

            notifier.Notify_VariableValuesChanged(listVarValues);

            if (adapter.CalcConfig.EnableOutputVarWrite) {
                await WriteOutputVars(outputDest);
            }

            adapter.SetLastOutputValues(outValues);
            adapter.SetLastStateValues(stateValues);

            TriggerCalculation[] triggerCalcs = result.TriggeredCalculations ?? Array.Empty<TriggerCalculation>();
            foreach (TriggerCalculation tc in triggerCalcs) {
                CalcInstance? calclInst = adapters.FirstOrDefault(a => a.CalcConfig.ID == tc.CalcID);
                if (calclInst != null) {
                    calclInst.Triggered_t = tc.TriggerStep_t;
                    calclInst.Triggered_dt = tc.TriggerStep_dt;
                }
            }

            //sw.Stop();
            //var vvv1 = VariableValue.Make(adapter.GetLastRunDurationVarRef(), VTQ.Make(sw.ElapsedMilliseconds, t, Quality.Good));
            //listVarValueTimer.Clear();
            //listVarValueTimer.Add(vvv1);
            //notifier.Notify_VariableValuesChanged(listVarValueTimer);

            (t, dt) = await runCondition.WaitForNextRun();
        }
    }

    private async Task WriteOutputVars(VariableValues outputDest) {
        try {
            await connection.WriteVariablesIgnoreMissing(outputDest); // TODO Report invalid var refs
        }
        catch (Exception exp) {

            Exception e = exp.GetBaseException() ?? exp;

            if (e is ConnectivityException) {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                while (!moduleShutdown && sw.ElapsedMilliseconds < 5 * 1000) {
                    await Task.Delay(10);
                }
                sw.Stop();

                if (moduleShutdown) {
                    throw;
                }

                await RestartConnectionOrFail();
                await WriteOutputVars(outputDest);
            }
            else {
                throw e;
            }
        }
    }

    private async Task<VTQs> ReadInputVars(CalcInstance adapter, List<Config.Input> inputs, VariableRefs inputVars, Timestamp now) {
        try {
            return await connection.ReadVariables(inputVars);
        }
        catch (Exception exp) {

            Exception e = exp.GetBaseException() ?? exp;

            if (e is ConnectivityException) {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                while (!moduleShutdown && sw.ElapsedMilliseconds < 5*1000) {
                    await Task.Delay(10);
                }
                sw.Stop();

                if (moduleShutdown) {
                    throw;
                }

                await RestartConnectionOrFail();
                return await ReadInputVars(adapter, inputs, inputVars, now);
            }
            else if (e is RequestException) {

                int N = inputs.Count;
                var invalidInputVars = new List<int>();
                var res = new VTQs(N);

                for (int i = 0; i < N; ++i) {
                    VariableRef variable = inputVars[i];
                    try {
                        res.Add(await connection.ReadVariable(variable));
                    }
                    catch(Exception) {
                        res.Add(VTQ.Make(inputs[i].GetDefaultValue(), now, Quality.Bad));
                        invalidInputVars.Add(i);
                    }
                }

                var affectedObjects = new ObjectRef[] { adapter.ID };
                if (invalidInputVars.Count == 1) {
                    int i = invalidInputVars[0];
                    var input = inputs[i];
                    string details = input.Name + ": " + inputVars[i].ToString();
                    Log_ErrorDetails("Invalid_Input_Var", $"Invalid variable ref for input '{input.Name}' of calculation '{adapter.Name}'", details, affectedObjects);
                }
                else if (invalidInputVars.Count > 1) {
                    string[] items = invalidInputVars.Select(i => inputs[i].Name + ": " + inputVars[i].ToString()).ToArray();
                    string details = string.Join("; ", items);
                    Log_ErrorDetails("Invalid_Input_Var", $"Invalid variable refs for inputs of calculation '{adapter.Name}'", details, affectedObjects);
                }

                return res;
            }
            else {
                throw e;
            }
        }
    }

    private bool isRestartingConnection = false;

    private async Task RestartConnectionOrFail() {

        if (isRestartingConnection) {
            while (isRestartingConnection) {
                await Task.Delay(50);
            }
            return;
        }

        Console.Out.WriteLine($"{Timestamp.Now} WARN: Connection closed unexpectedly. Restarting connection...");
        Console.Out.Flush();

        try {
            isRestartingConnection = true;
            this.connection = await HttpConnection.ConnectWithModuleLogin(initInfo);
            isRestartingConnection = false;
            Console.Out.WriteLine($"{Timestamp.Now}: Connection restarted successfully.");
            Console.Out.Flush();
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            Console.Error.WriteLine($"{Timestamp.Now}: Restarting connection failed: {e.Message}");
            Console.Error.WriteLine($"{Timestamp.Now}: Terminating in 5 seconds...");
            Console.Error.Flush();
            await Task.Delay(5000);
            Environment.Exit(1);
        }
    }

    private Timestamp GetNextNormalizedTimestamp(Timestamp tCurrent, Duration cycle, Duration offset, CalcInstance adapter) {

        Timestamp tNext = Time.GetNextNormalizedTimestamp(cycle, offset);

        Duration minDuration = Duration.FromMilliseconds(1);
        Duration c = cycle < minDuration ? minDuration : cycle;

        while (tNext <= tCurrent) {
            tNext += c;
        }

        Trigger trigger = adapter.triggerDurationWarning.GetTrigger(isOK: tNext == tCurrent + cycle);

        if (trigger == Trigger.On) {
            Log_Warn("Cycle", $"Cycle length of {cycle} not long enough for calculation {adapter.Name}!", null, new ObjectRef[] { adapter.ID });
        }
        else if (trigger == Trigger.Off) {
            Log_ReturnToNormal("Cycle", $"Cycle length of {cycle} is long enough for calculation {adapter.Name}.", new ObjectRef[] { adapter.ID });
        }

        return tNext;
    }

    private void Log_Info(string type, string msg) {
        Log_Event(Severity.Info, type, msg);
    }

    private void Log_ReturnToNormal(string type, string msg, ObjectRef[]? affectedObjects = null) {
        Log_Event(Severity.Info, type, msg, affectedObjects: affectedObjects, returnToNormal: true);
    }

    private void Log_Error(string type, string msg, Origin? initiator = null) {
        Log_Event(Severity.Alarm, type, msg, initiator);
    }

    private void Log_ErrorDetails(string type, string msg, string details, ObjectRef[] affectedObjects) {
        Log_Event(Severity.Alarm, type, msg, null, details, affectedObjects);
    }

    private void Log_Warn(string type, string msg, Origin? initiator = null, ObjectRef[]? affectedObjects = null) {
        Log_Event(Severity.Warning, type, msg, initiator, "", affectedObjects);
    }

    private void Log_Event(Severity severity, string type, string msg, Origin? initiator = null, string details = "", ObjectRef[]? affectedObjects = null, bool returnToNormal = false) {

        var ae = new AlarmOrEventInfo() {
            Time = Timestamp.Now,
            Severity = severity,
            Type = type,
            ReturnToNormal = returnToNormal,
            Message = msg,
            Details = details ?? "",
            AffectedObjects = affectedObjects ?? Array.Empty<ObjectRef>(),
            Initiator = initiator
        };

        notifier!.Notify_AlarmOrEvent(ae);
    }

    // This will be called from a different Thread, therefore post it to the main thread!
    public void Notify_NeedRestart(string reason, Calculation adapter) {
        moduleThread?.Post(Do_Notify_NeedRestart, reason, adapter);
    }

    private void Do_Notify_NeedRestart(string reason, Calculation adapter) {
        CalcInstance? ast = adapters.FirstOrDefault(a => a.CalcConfig.ID == adapter.ID);
        if (ast != null) {
            Task ignored = RestartAdapter(ast, reason);
        }
    }

    // This will be called from a different Thread, therefore post it to the main thread!
    public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo, Calculation adapter) {
        moduleThread?.Post(Do_Notify_AlarmOrEvent, eventInfo, adapter);
    }

    private void Do_Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo, Calculation adapter) {

        var ae = new AlarmOrEventInfo() {
            Time = eventInfo.Time,
            Severity = eventInfo.Severity,
            Type = eventInfo.Type,
            ReturnToNormal = eventInfo.ReturnToNormal,
            Message = adapter.Name + ": " + eventInfo.Message,
            Details = eventInfo.Details,
            AffectedObjects = eventInfo.AffectedObjects.Select(obj => ObjectRef.Make(moduleID, obj)).ToArray(),
            Initiator = null
        };

        notifier!.Notify_AlarmOrEvent(ae);
    }
}

sealed class Wrapper(Module m, Calculation a) : AdapterCallback
{
    private readonly Module m = m;
    private readonly Calculation a = a;

    public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
        m.Notify_AlarmOrEvent(eventInfo, a);
    }

    public void Notify_NeedRestart(string reason) {
        m.Notify_NeedRestart(reason, a);
    }
}

public sealed class AdapterInfo {
    public string Type { get; set; } = "";
    public bool Show_WindowVisible { get; set; }
    public bool Show_Definition { get; set; }
    public string DefinitionLabel { get; set; } = "";
    public bool DefinitionIsCode { get; set; }
    public string CodeLang { get; set; } = "";
    public string[] Subtypes { get; set; } = [];
}
