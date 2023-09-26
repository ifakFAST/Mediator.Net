// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;

namespace Ifak.Fast.Mediator.Calc
{
    public class CalcInstance
    {
        private readonly string moduleID;
        internal readonly Trigger_M_outof_N triggerDurationWarning = new Trigger_M_outof_N(m: 6, n: 60);

        public CalcInstance(Config.Calculation a, string moduleID) {
            SetConfig(a);
            State = State.Created;
            this.moduleID = moduleID;
        }

        public void CreateInstance(Dictionary<string, Type> mapAdapterTypes, ModuleInitInfo info) {
            if (!mapAdapterTypes.ContainsKey(CalcConfig.Type)) {
                throw new Exception($"No adapter type '{CalcConfig.Type}' found.");
            }
            Type type = mapAdapterTypes[CalcConfig.Type];
            CalculationBase? rawAdapter = (CalculationBase?)Activator.CreateInstance(type);
            if (rawAdapter == null) throw new Exception($"Failed to create instance of calculation adapter {type}");

            if (rawAdapter is ConnectionConsumer cons) {
                this.info = info;
                cons.SetConnectionRetriever(ConnectionRetrieverAsync);
            }

            instance = new SingleThreadCalculation(rawAdapter);
            State = State.Created;
            LastError = "";
        }

        private ModuleInitInfo info = new();
        private Connection? connection = null;

        private async Task<Connection> ConnectionRetrieverAsync() {

            if (connection != null && !connection.IsClosed) {
                try {
                    await connection.Ping();
                    return connection;
                }
                catch (Exception) {
                    connection = null;
                }
            }

            return await HttpConnection.ConnectWithModuleLogin(info);
        }

        public SingleThreadCalculation? Instance => instance;

        public void SetInstanceNull() {
            instance = null;
        }

        public string LastError { get; set; } = "";

        public bool IsRestarting = false;

        public string Name => CalcConfig == null ? "?" : CalcConfig.Name;

        public ObjectRef ID => ObjectRef.Make(moduleID, CalcConfig.ID);

        public State State { get; set; } = State.Created;

        private string originalConfigWithoutIO = "";

        public Config.Calculation CalcConfig { get; private set; } = new Config.Calculation();

        public Duration ScaledCycle() {
            long cycleTimeScaledMS = (long)(CalcConfig.Cycle.TotalMilliseconds / CalcConfig.RealTimeScale);
            return Duration.FromMilliseconds(cycleTimeScaledMS);
        }

        public Duration ScaledOffset() {
            long offsetTimeScaledMS = (long)(CalcConfig.Offset.TotalMilliseconds / CalcConfig.RealTimeScale);
            return Duration.FromMilliseconds(offsetTimeScaledMS);
        }

        public bool SetConfig(Config.Calculation newConfig) {

            var inputs = newConfig.Inputs.ToArray();
            newConfig.Inputs.Clear();

            var outputs = newConfig.Outputs.ToArray();
            newConfig.Outputs.Clear();

            var states = newConfig.States.ToArray();
            newConfig.States.Clear();

            string newOriginalConfigWithoutIO = Xml.ToXml(newConfig);
            bool changed = (newOriginalConfigWithoutIO != originalConfigWithoutIO);

            newConfig.Inputs.AddRange(inputs);
            newConfig.Outputs.AddRange(outputs);
            newConfig.States.AddRange(states);

            CalcConfig = newConfig;
            originalConfigWithoutIO = newOriginalConfigWithoutIO;
            return changed;
        }

        public InputValue[] CurrentInputValues(Timestamp now) {
            int N = CalcConfig.Inputs.Count;
            InputValue[] res = new InputValue[N];
            for (int n = 0; n < N; ++n) {
                Config.Input input = CalcConfig.Inputs[n];
                InputValue inValue = new InputValue() {
                    InputID = input.ID
                };
                if (input.Constant.HasValue) {
                    inValue.Value = VTQ.Make(input.Constant.Value, now, Quality.Good);
                }
                else if (mapInputValues.ContainsKey(input.ID)) {
                    VariableValue vv = mapInputValues[input.ID];
                    inValue.Value = vv.Value;
                    inValue.AttachedVariable = vv.Variable;
                }
                else {
                    inValue.Value = VTQ.Make(input.GetDefaultValue(), now, Quality.Bad);
                }
                res[n] = inValue;
            }
            return res;
        }

        private Dictionary<string, VariableValue> mapInputValues = new Dictionary<string, VariableValue>();

        //public void UpdateInputValues(VariableValue[] variables) {
        //    foreach (Config.Input input in Config.Inputs) {
        //        if (input.Variable.HasValue) {
        //            VariableRef va = input.Variable.Value;
        //            int idx = variables.FindIndex(v => v.Variable == va);
        //            if (idx > -1) {
        //                mapInputValues[input.ID] = variables[idx];
        //            }
        //        }
        //    }
        //}

        public void UpdateInputValues(List<VariableRef> variables, VTQs values) {
            foreach (Config.Input input in CalcConfig.Inputs) {
                if (input.Variable.HasValue) {
                    VariableRef va = input.Variable.Value;
                    int idx = variables.FindIndex(v => v == va);
                    if (idx > -1) {
                        mapInputValues[input.ID] = VariableValue.Make(va, values[idx]);
                    }
                }
            }
        }

        public async Task WaitUntil(Timestamp t) {
            while (State == State.Running && Timestamp.Now <= t) {
                Duration sleepFull = t - Timestamp.Now;
                long sleepMS = sleepFull.TotalMilliseconds.InRange(1, 500);
                await Task.Delay((int)sleepMS);
            }
        }

        public void SetInitialOutputValues(Dictionary<VariableRef, VTQ> mapVarValues) {
            lastOutputValues.Clear();
            foreach (Config.Output output in CalcConfig.Outputs) {
                VariableRef v = GetOutputVarRef(output.ID);
                if (mapVarValues.ContainsKey(v)) {
                    VTQ value = mapVarValues[v];
                    lastOutputValues.Add(new OutputValue() {
                        OutputID = output.ID,
                        Value = value
                    });
                }
            }
        }

        public void SetInitialStateValues(Dictionary<VariableRef, VTQ> mapVarValues) {
            lastStateValues.Clear();
            foreach (Config.State state in CalcConfig.States) {
                VariableRef v = GetStateVarRef(state.ID);
                if (mapVarValues.ContainsKey(v)) {
                    VTQ value = mapVarValues[v];
                    lastStateValues.Add(new StateValue() {
                        StateID = state.ID,
                        Value = value.V
                    });
                }
            }
        }

        private List<OutputValue> lastOutputValues = new List<OutputValue>();
        private List<StateValue> lastStateValues = new List<StateValue>();
        private SingleThreadCalculation? instance;

        public OutputValue[] LastOutputValues => lastOutputValues.ToArray();
        public StateValue[] LastStateValues => lastStateValues.ToArray();

        public Task? RunLoopTask { get; internal set; }

        public bool RunLoopRunning => RunLoopTask != null && !RunLoopTask.IsCompleted;

        public void SetLastOutputValues(OutputValue[] outValues) {
            lastOutputValues.Clear();
            lastOutputValues.AddRange(outValues);
        }

        public void SetLastStateValues(StateValue[] stateValues) {
            lastStateValues.Clear();
            lastStateValues.AddRange(stateValues);
        }

        public VariableRef GetInputVarRef(string inputID) {
            string calcID = this.CalcConfig.ID;
            string fullInputID = calcID + Config.Input.ID_Separator + inputID;
            return VariableRef.Make(moduleID, fullInputID, "Value");
        }

        public VariableRef GetOutputVarRef(string outputID) {
            string calcID = this.CalcConfig.ID;
            string fullOutputID = calcID + Config.Output.ID_Separator + outputID;
            return VariableRef.Make(moduleID, fullOutputID, "Value");
        }

        public VariableRef GetStateVarRef(string stateID) {
            string calcID = this.CalcConfig.ID;
            string fullStateID = calcID + Config.State.ID_Separator + stateID;
            return VariableRef.Make(moduleID, fullStateID, "Value");
        }

        public VariableRef GetLastRunDurationVarRef() {
            string calcID = this.CalcConfig.ID;
            return VariableRef.Make(moduleID, calcID, "Duration");
        }
    }

    public enum State
    {
        Created,
        InitStarted,
        InitError,
        InitComplete,
        Running,
        ShutdownStarted,
        ShutdownCompleted
    }
}
