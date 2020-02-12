// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc
{
    public class Module : ModelObjectModule<Config.Calc_Model>
    {
        public Func<string, Type[]> fLoadCalcTypesFromAssembly = (s) => new Type[0];

        private readonly List<CalcInstance> adapters = new List<CalcInstance>();
        private readonly Dictionary<string, Type> mapAdapterTypes = new Dictionary<string, Type>();
        private ModuleInitInfo initInfo;
        private ModuleThread moduleThread = null;
        private Connection connection = null;
        private Mediator.Config moduleConfig = null;
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

            string[] assemblies = strAssemblies.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

            string[] absoluteAssemblies = assemblies.Select(d => Path.GetFullPath(d)).ToArray();
            foreach (string assembly in absoluteAssemblies) {
                if (!File.Exists(assembly)) throw new Exception($"calculation-assembly does not exist: {assembly}");
            }

            var adapterTypes = Reflect.GetAllNonAbstractSubclasses(typeof(CalculationBase)).ToList();
            adapterTypes.AddRange(absoluteAssemblies.SelectMany(fLoadCalcTypesFromAssembly));

            mapAdapterTypes.Clear();
            foreach (var type in adapterTypes) {
                Identify id = type.GetCustomAttribute<Identify>();
                if (id != null) {
                    mapAdapterTypes[id.ID] = type;
                }
            }

            foreach (CalcInstance adapter in adapters) {
                adapter.CreateInstance(mapAdapterTypes);
            }

            Dictionary<VariableRef, VTQ> varMap = restoreVariableValues.ToDictionary(v => v.Variable, v => v.Value);
            foreach (CalcInstance adapter in adapters) {
                adapter.SetInitialOutputValues(varMap);
            }

            try {
                Task[] initTasks = adapters.Select(InitAdapter).ToArray();
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

        protected override async Task OnConfigModelChanged(bool init) {

            await base.OnConfigModelChanged(init);

            // model.ValidateOrThrow();
            model.Normalize();

            Config.Calculation[] enabledAdapters = model.GetAllCalculations().Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Type) && !string.IsNullOrWhiteSpace(a.Definition)).ToArray();

            CalcInstance[] removedAdapters = adapters.Where(a => !enabledAdapters.Any(x => x.ID == a.CalcConfig.ID)).ToArray();
            CalcInstance[] newAdapters = enabledAdapters.Where(x => !adapters.Any(a => x.ID == a.CalcConfig.ID)).Select(a => new CalcInstance(a, moduleID)).ToArray();

            adapters.RemoveAll(removedAdapters);
            adapters.AddRange(newAdapters);

            var restartAdapters = new List<CalcInstance>();
            // var inputVars = new HashSet<VariableRef>();

            foreach (CalcInstance adapter in adapters) {

                bool changed = adapter.SetConfig(enabledAdapters.FirstOrDefault(x => x.ID == adapter.CalcConfig.ID));
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
                        adapter.CreateInstance(mapAdapterTypes);
                    }

                    Task[] initTasks = newAdapters.Select(InitAdapter).ToArray();
                    await Task.WhenAll(initTasks);

                    foreach (CalcInstance adapter in newAdapters) {
                        StartRunLoopTask(adapter);
                    }
                }

                if (restartAdapters.Count > 0) {
                    Task[] restartTasks = restartAdapters.Select(a => RestartAdapterOrCrash(a, "Config changed", critical: false)).ToArray();
                    await Task.WhenAll(restartTasks);
                }
            }

            // await connection.DisableChangeEvents();
            // await connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), inputVars.ToArray());
        }

        private async Task InitAdapter(CalcInstance adapter) {
            Calculation info = adapter.CalcConfig.ToCalculation();
            try {
                var initParams = new InitParameter() {
                    Calculation = info,
                    LastOutput = adapter.LastOutputValues,
                    ConfigFolder = Path.GetDirectoryName(base.modelFileName),
                    DataFolder = initInfo.DataFolder,
                    ModuleConfig = moduleConfig.ToNamedValues()
                };
                adapter.State = State.InitStarted;
                InitResult res = await adapter.Instance.Initialize(initParams, new Wrapper(this, info));
                if (adapter.State == State.InitStarted) {

                    Config.Input[] newInputs = res.Inputs.Select(ip => MakeInput(ip, adapter.CalcConfig)).ToArray();
                    Config.Output[] newOutputs = res.Outputs.Select(ip => MakeOutput(ip, adapter.CalcConfig)).ToArray();

                    bool inputsChanged = !StdJson.ObjectsDeepEqual(adapter.CalcConfig.Inputs, newInputs);
                    bool outputsChanged = !StdJson.ObjectsDeepEqual(adapter.CalcConfig.Outputs, newOutputs);

                    var changedMembers = new List<MemberValue>(2);

                    if (inputsChanged) {
                        changedMembers.Add(MemberValue.Make(moduleID, adapter.CalcConfig.ID, "Inputs", DataValue.FromObject(newInputs)));
                    }

                    if (outputsChanged) {
                        changedMembers.Add(MemberValue.Make(moduleID, adapter.CalcConfig.ID, "Outputs", DataValue.FromObject(newOutputs)));
                    }

                    if (changedMembers.Count > 0) {
                        await UpdateConfig(GetModuleOrigin(),
                            updateOrDeleteObjects: new ObjectValue[0],
                            updateOrDeleteMembers: changedMembers.ToArray(),
                            addArrayElements: new AddArrayElement[0]);
                    }

                    adapter.State = State.InitComplete;
                }
            }
            catch (Exception e) {
                Exception exp = e.GetBaseException() ?? e;
                adapter.State = State.InitError;
                adapter.LastError = exp.Message;
                throw new Exception($"Initialize of calculation {info.Name} failed: " + exp.Message, exp);
            }
        }

        private static Config.Input MakeInput(InputDef input, Config.Calculation calc) {
            Config.Input old = calc.Inputs.FirstOrDefault(x => x.ID == input.ID);
            return new Config.Input() {
                ID = input.ID,
                Name = input.Name,
                Unit = input.Unit,
                Dimension = input.Dimension,
                Type = input.Type,
                Constant = old != null ? old.Constant : (input.DefaultValue.HasValue ? input.DefaultValue.Value: (DataValue?)null),
                Variable = old != null ? old.Variable : null,
            };
        }

        private static Config.Output MakeOutput(OutputDef output, Config.Calculation calc) {
            Config.Output old = calc.Outputs.FirstOrDefault(x => x.ID == output.ID);
            return new Config.Output() {
                ID = output.ID,
                Name = output.Name,
                Unit = output.Unit,
                Dimension = output.Dimension,
                Type = output.Type,
                Variable = old != null ? old.Variable : null,
            };
        }

        private async Task RestartAdapterOrCrash(CalcInstance adapter, string reason, bool critical = true) {

            string msg = "Restarting calculation " + adapter.Name + ". Reason: " + reason;
            if (critical) {
                Log_Warn("CalcRestart", msg);
            }
            else {
                Log_Info("CalcRestart", msg);
            }

            const int TimeoutSeconds = 10;
            try {
                Task shutdown = ShutdownAdapter(adapter);
                Task t = await Task.WhenAny(shutdown, Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds)));
                if (t != shutdown) {
                    Log_Warn("CalcShutdownTimeout", $"Shutdown request for calculation {adapter.Name} failed to complete within {TimeoutSeconds} seconds.");
                    // go ahead and hope for the best...
                }
                adapter.CreateInstance(mapAdapterTypes);
                await InitAdapter(adapter);
                if (adapter.State == State.InitComplete) {
                    StartRunLoopTask(adapter);
                }
            }
            catch (Exception exp) {
                string errMsg = "Restart of calculation " + adapter.Name + " failed: " + exp.Message;
                Log_Error("CalcRestartError", errMsg);
                if (critical) {
                    Thread.Sleep(1000);
                    Environment.Exit(1); // will result in restart of entire module by Mediator
                }
                else {
                    throw new Exception(errMsg);
                }
            }
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
            adapter.Instance = null;
        }

        public override async Task Run(Func<bool> shutdown) {

            this.connection = await HttpConnection.ConnectWithModuleLogin(initInfo);

            foreach (CalcInstance a in adapters) {
                StartRunLoopTask(a);
            }

            while (!shutdown()) {
                await Task.Delay(100);
            }
            await Shutdown();
        }

        private void StartRunLoopTask(CalcInstance adapter) {
            Task readTask = AdapterRunLoopTask(adapter);
            adapter.RunLoopTask = readTask;
            var ignored1 = readTask.ContinueOnMainThread(t => {

                if (t.IsFaulted && !moduleShutdown && adapter.State == State.Running) {
                    Exception exp = t.Exception.GetBaseException() ?? t.Exception;
                    Task ignored2 = RestartAdapterOrCrash(adapter, "Run loop exception: " + exp.Message, critical: true);
                }
            });
        }

        private async Task AdapterRunLoopTask(CalcInstance adapter) {

            adapter.State = State.Running;

            Duration cycle = adapter.ScaledCycle();
            Timestamp t = GetNextNormalizedTimestamp(cycle);
            string moduleID = base.moduleID;

            await adapter.WaitUntil(t);

            Config.Input[] inputs = new Config.Input[0];
            VariableRef[] inputVars = new VariableRef[0];

            // var sw = new System.Diagnostics.Stopwatch();
            while (adapter.State == State.Running) {

                int N_In = adapter.CalcConfig.Inputs.Count(inp => inp.Variable.HasValue);

                if (N_In != inputs.Length) {
                    inputs = new Config.Input[N_In];
                    inputVars = new VariableRef[N_In];
                }

                int i = 0;
                foreach (Config.Input inp in adapter.CalcConfig.Inputs) {
                    if (inp.Variable.HasValue) {
                        inputs[i] = inp;
                        inputVars[i] = inp.Variable.Value;
                        i++;
                    }
                }

                // Config.Input[] inputs = adapter.CalcConfig.Inputs.Where(inp => inp.Variable.HasValue).ToArray();
                // VariableRef[] inputVars = inputs.Select(inp => inp.Variable.Value).ToArray();

                // sw.Restart();
                VTQ[] values = await ReadInputVars(adapter, inputs, inputVars, t);

                // sw.Stop();
                // double dd = sw.ElapsedTicks;
                //if (dd > 100) {
                //    Console.Error.WriteLine(dd / TimeSpan.TicksPerMillisecond + " ms");
                //}

                adapter.UpdateInputValues(inputVars, values);

                InputValue[] inputValues = adapter.CurrentInputValues(t);

                VariableValue[] inValues = inputValues.Select(v => VariableValue.Make(adapter.GetInputVarRef(v.InputID), v.Value.WithTime(t))).ToArray();
                notifier.Notify_VariableValuesChanged(inValues);

                StepResult result = await adapter.Instance.Step(t, inputValues);
                OutputValue[] outValues = result.Output;
                // TODO Store result.State

                //Console.WriteLine($"{Timestamp.Now}: out: " + StdJson.ObjectToString(outValues));

                List<VariableValue> outVarValues = outValues.Select(v => VariableValue.Make(adapter.GetOutputVarRef(v.OutputID), v.Value.WithTime(t))).ToList();

                List<VariableValue> outputDest = new List<VariableValue>();
                foreach (Config.Output ot in adapter.CalcConfig.Outputs) {
                    if (ot.Variable.HasValue) {
                        int idx = outValues.FindIndex(o => o.OutputID == ot.ID);
                        if (idx > -1) {
                            outputDest.Add(VariableValue.Make(ot.Variable.Value, outValues[idx].Value.WithTime(t)));
                        }
                    }
                }
                notifier.Notify_VariableValuesChanged(outVarValues);

                await connection.WriteVariables(outputDest.ToArray()); // TODO Error handling!

                adapter.SetLastOutputValues(outValues);

                t = GetNextNormalizedTimestamp(t, cycle, adapter);

                await adapter.WaitUntil(t);
            }
        }

        private async Task<VTQ[]> ReadInputVars(CalcInstance adapter, IList<Config.Input> inputs, VariableRef[] inputVars, Timestamp now) {
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
                    throw;
                }
                else if (e is RequestException) {

                    int N = inputs.Count;
                    var invalidInputVars = new List<int>();
                    VTQ[] res = new VTQ[N];

                    for (int i = 0; i < N; ++i) {
                        VariableRef variable = inputVars[i];
                        try {
                            res[i] = await connection.ReadVariable(variable);
                        }
                        catch(Exception) {
                            res[i] = VTQ.Make(inputs[i].GetDefaultValue(), now, Quality.Bad);
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

        private Timestamp GetNextNormalizedTimestamp(Timestamp tCurrent, Duration cycle, CalcInstance adapter) {

            Timestamp tNext = GetNextNormalizedTimestamp(cycle);

            Duration minDuration = Duration.FromMilliseconds(1);
            Duration c = cycle < minDuration ? minDuration : cycle;

            while (tNext <= tCurrent) {
                tNext += c;
            }

            if (tNext > tCurrent + cycle) {
                Log_Warn("Cycle", $"Cycle length of {cycle} not long enough for calculation {adapter.Name}!", null, new ObjectRef[] { adapter.ID });
            }

            return tNext;
        }

        private static Timestamp GetNextNormalizedTimestamp(Duration cycle) {
            long nowTicks = Timestamp.Now.JavaTicks;
            long cycleTicks = cycle.TotalMilliseconds;
            long tNext = nowTicks - (nowTicks % cycleTicks) + cycleTicks;
            return Timestamp.FromJavaTicks(tNext);
        }

        private void Log_Info(string type, string msg) {
            Log_Event(Severity.Info, type, msg);
        }

        private void Log_Error(string type, string msg, Origin? initiator = null) {
            Log_Event(Severity.Alarm, type, msg, initiator);
        }

        private void Log_ErrorDetails(string type, string msg, string details, ObjectRef[] affectedObjects) {
            Log_Event(Severity.Alarm, type, msg, null, details, affectedObjects);
        }

        private void Log_Warn(string type, string msg, Origin? initiator = null, ObjectRef[] affectedObjects = null) {
            Log_Event(Severity.Warning, type, msg, initiator, "", affectedObjects);
        }

        private void Log_Event(Severity severity, string type, string msg, Origin? initiator = null, string details = "", ObjectRef[] affectedObjects = null) {

            var ae = new AlarmOrEventInfo() {
                Time = Timestamp.Now,
                Severity = severity,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedObjects = affectedObjects ?? new ObjectRef[0],
                Initiator = initiator
            };

            notifier.Notify_AlarmOrEvent(ae);
        }

        // This will be called from a different Thread, therefore post it to the main thread!
        public void Notify_NeedRestart(string reason, Calculation adapter) {
            moduleThread.Post(Do_Notify_NeedRestart, reason, adapter);
        }

        private void Do_Notify_NeedRestart(string reason, Calculation adapter) {
            CalcInstance ast = adapters.FirstOrDefault(a => a.CalcConfig.ID == adapter.ID);
            Task ignored = RestartAdapterOrCrash(ast, reason);
        }

        // This will be called from a different Thread, therefore post it to the main thread!
        public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo, Calculation adapter) {
            moduleThread.Post(Do_Notify_AlarmOrEvent, eventInfo, adapter);
        }

        private void Do_Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo, Calculation adapter) {

            var ae = new AlarmOrEventInfo() {
                Time = eventInfo.Time,
                Severity = eventInfo.Severity,
                Type = eventInfo.Type,
                Message = adapter.Name + ": " + eventInfo.Message,
                Details = eventInfo.Details,
                AffectedObjects = eventInfo.AffectedDataItems.Select(di => ObjectRef.Make(moduleID, di)).ToArray(),
                Initiator = null
            };

            notifier.Notify_AlarmOrEvent(ae);
        }
    }

    class Wrapper : AdapterCallback
    {
        private readonly Module m;
        private readonly Calculation a;

        public Wrapper(Module m, Calculation a) {
            this.m = m;
            this.a = a;
        }

        public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
            m.Notify_AlarmOrEvent(eventInfo, a);
        }

        public void Notify_NeedRestart(string reason) {
            m.Notify_NeedRestart(reason, a);
        }
    }
}
