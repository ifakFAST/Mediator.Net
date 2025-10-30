// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp;

namespace StdLib;

public sealed class DerivedSignal : Identifiable
{
    public string FullID { get; set; } = ""; // if set, this is used as the ID
    public string FullName { get; set; } = ""; // if set, this is used as the Name
    public string ID { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; } = "";
    public string ParentFolderID { get; } = "";
    public Timestamp? StartTime { get; } = null;
    public Duration Resolution { get; } = Duration.FromMinutes(1);
    public InputDef[] Inputs { get; }
    public readonly List<StateFloat64> ParamStates = [];
    public readonly List<StateBase> InputStates = [];
    public Delegate Calculation { get; }

    private bool parametersAsArray = false;
    private int? idxParameterLast = null;
    private int? idxParameter_dt = null;
    private int? idxParameter_t = null;
    private int? idxParameter_Inputs = null;

    private readonly object?[] calcParameterValues;
    private readonly MethodInfo calcMethod;

    private VariableRef? VarRefSelf = null;

    public VariableRef VariableRef => VarRefSelf ?? throw new Exception($"DerivedSignal {ID} not initialized yet.");

    public DerivedSignal(string parentFolderID, InputDef[] inputs, Delegate calculation, Duration resolution, string fullID = "", string fullName = "", string unit = "", Timestamp? startTime = null) {
        FullID = fullID;
        FullName = string.IsNullOrWhiteSpace(fullName) ? fullID : fullName;
        Unit = unit;
        ParentFolderID = parentFolderID;
        Resolution = resolution;
        Inputs = inputs;
        Calculation = calculation;
        StartTime = startTime;

        InitTest();
        int countParameterValues = 
            (parametersAsArray ? 1 : Inputs.Length) + 
            ParamStates.Count + 
            (idxParameterLast.HasValue ? 1 : 0) + 
            (idxParameter_dt.HasValue ? 1 : 0) +
            (idxParameter_t.HasValue ? 1 : 0) +
            (idxParameter_Inputs.HasValue ? 1 : 0);

        calcParameterValues = new object?[countParameterValues];
        calcMethod = Calculation.Method;
    }

    private void InitTest() {

        if (Inputs.Length == 0) {
            throw new ArgumentException("No parameters defined.");
        }

        MethodInfo method = Calculation.Method;
        ParameterInfo[] parameters = method.GetParameters();

        if (parameters.Length == 0) {
            throw new ArgumentException("No parameters defined in calculation.");
        }

        ParameterInfo pFirst = parameters.First();
        int idxNextParamIdx = 0;

        if (pFirst.ParameterType == typeof(double[])) {
            parametersAsArray = true;
            idxNextParamIdx = 1;
        }
        else if (pFirst.ParameterType == typeof(double) && pFirst.Name == Inputs[0].Name) {
            parametersAsArray = false;
            for (int i = 1; i < Inputs.Length; ++i) {
                ParameterInfo p = parameters[i];
                if (p.Name != Inputs[i].Name) {
                    throw new ArgumentException("Parameter name does not match.");
                }
                if (p.ParameterType != typeof(double)) {
                    throw new ArgumentException("Parameter type does not match.");
                }
            }
            idxNextParamIdx = Inputs.Length;
        }

        List<ParameterInfo> parametersByRef = [];
        while (idxNextParamIdx < parameters.Length && parameters[idxNextParamIdx].ParameterType.IsByRef) {
            parametersByRef.Add(parameters[idxNextParamIdx]);
            idxNextParamIdx++;
        }

        static bool IsNullableDouble(Type t) {
            return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>) && t.GetGenericArguments()[0] == typeof(double);
        }

        for (int i = idxNextParamIdx; i < parameters.Length; ++i) {
            ParameterInfo p = parameters[i];
            string name = p.Name ?? throw new ArgumentException("Parameter name is missing.");
            Type type = p.ParameterType;
            if (IsNullableDouble(type) && name == "last") {
                idxParameterLast = i;
            }
            else if (type == typeof(Duration) && name == "dt") {
                idxParameter_dt = i;
            }
            else if (type == typeof(Timestamp) && name == "t") {
                idxParameter_t = i;
            }
            else if (type == typeof(InputDef[]) && name == "inputs") {
                idxParameter_Inputs = i;
            }
            else {
                throw new ArgumentException($"Unexpected parameter {name}.");
            }
        }


        foreach (var param in parametersByRef) {
            string name = param.Name ?? throw new ArgumentException("Parameter name is missing.");
            Type type = param.ParameterType;
            Type elementType = type.GetElementType()!;
            bool isNullable = elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Nullable<>);
            double? defaultValue = isNullable ? null : 0.0;
            StateFloat64 state = new(name, defaultValue: defaultValue) {
                ID = name
            };
            Console.WriteLine($"Adding state {state.ID} with default value {state.DefaultValue}");
            ParamStates.Add(state);
        }

        foreach (InputDef input in Inputs) {
            input.AddStates(InputStates);
        }

        // Verify that the return type is double:
        if (method.ReturnType != typeof(double)) {
            throw new ArgumentException("Return type is not double.");
        }
    }

    public double Calculate(IReadOnlyList<double> values, double? last, Duration dt, Timestamp t) {

        int numberOfInputParameters;

        if (parametersAsArray) {
            calcParameterValues[0] = values.ToArray();
            numberOfInputParameters = 1;
        }
        else {
            for (int i = 0; i < values.Count; ++i) {
                calcParameterValues[i] = values[i];
            }
            numberOfInputParameters = values.Count;
        }

        for (int i = 0; i < ParamStates.Count; ++i) {
            calcParameterValues[numberOfInputParameters + i] = ParamStates[i].ValueOrNull;
        }

        if (idxParameterLast.HasValue) {
           calcParameterValues[idxParameterLast.Value] = last;
        }

        if (idxParameter_dt.HasValue) {
            calcParameterValues[idxParameter_dt.Value] = dt;
        }

        if (idxParameter_t.HasValue) {
            calcParameterValues[idxParameter_t.Value] = t;
        }

        if (idxParameter_Inputs.HasValue) {
            calcParameterValues[idxParameter_Inputs.Value] = Inputs;
        }

        double result;

        try {
            result = (double)calcMethod.Invoke(Calculation.Target, calcParameterValues)!;
        }
        catch (TargetInvocationException ex) {
            string errMs = $"Error during calculation of DerivedSignal \"{ID}\": {ex.InnerException?.Message}";
            throw new Exception(errMs);
        }

        for (int i = 0; i < ParamStates.Count; ++i) {
            object? updatedStateValue = calcParameterValues[numberOfInputParameters + i];
            double? value = (double?)updatedStateValue;
            ParamStates[i].ValueOrNull = value;
        }

        return result;
    }

    public static InputDef DefineInput(string name, VariableRef var) {
        return new InputDef(name, VarRef: var);
    }

    public static InputDef DefineInput(string name, SiblingSignal sibling) {
        return new InputDef(name, SiblingRef: sibling);
    }

    public static InputDef DefineInput(VariableRef var) {
        return new InputDef("", VarRef: var);
    }

    public static InputDef DefineInput(SiblingSignal sibling) {
        return new InputDef("", SiblingRef: sibling);
    }

    public static SiblingSignal SiblingSignalRef(string id) {
        return new SiblingSignal(id);
    }

    public static VariableRef SignalRef(string id) {
        return VariableRef.Make("CALC", id, "Value");
    }

    public static VariableRef DataItemRef(string id) {
        return VariableRef.Make("IO", id, "Value");
    }

    public static VariableRef VarRef(string moduleID, string objectID, string name = "Value") {
        return VariableRef.Make(moduleID, objectID, name);
    }

    public static void UpdateDerivedSignals(object script, Timestamp tEnd) {

        List<DerivedSignal> derivedSignals = CSharp.GetIdentifiableMembers<DerivedSignal>(script, "", recursive: true, []);

        foreach (var signal in derivedSignals) {

            VariableRef GetSiblingWithLocalID(string siblingLocalID) {
                var (path, _) = signal.SplitId();
                bool emptyPath = string.IsNullOrEmpty(path);
                string siblingID = emptyPath ? siblingLocalID : path + "." + siblingLocalID;
                DerivedSignal? sibling = derivedSignals.FirstOrDefault(s => s.ID == siblingID);
                if (sibling == null) {
                    throw new ArgumentException($"Sibling signal {siblingID} not found.");
                }
                return sibling.VarRefSelf ?? throw new ArgumentException($"Sibling signal {siblingID} not initialized yet.");
            }

            signal.Update(tEnd, GetSiblingWithLocalID);
        }
    }

    readonly Api api = new();

    private (string path, string localID) SplitId() {
        int idx = ID.LastIndexOf('.');
        if (idx < 0) {
            return ("", ID);
        }
        return (ID.Substring(0, idx), ID.Substring(idx + 1));
    }

    public void Update(Timestamp tEnd, Func<string, VariableRef> siblingResolver) {

        if (Inputs.Length == 0) return;

        if (api.abortStep) {
            Console.WriteLine($"Calculation aborted for {ID}");
            return;
        }

        const int ChunkSize = 5000;

        string signalID   = string.IsNullOrWhiteSpace(FullID)   ? ID   : FullID;
        string signalName = string.IsNullOrWhiteSpace(FullName) ? Name : FullName;

        var signalInfo = new SignalInfo(signalID) {
            Name = signalName,
            Unit = Unit,
        };

        VariableRef varRef = api.CreateSignalIfNotExists(ParentFolderID, signalInfo);

        VarRefSelf = varRef;

        foreach (InputDef inputDef in Inputs) {
            inputDef.ResolveVarRef(siblingResolver);
            inputDef.SetApi(api);
        }

        List<VTQ> oldVTQ = api.HistorianReadRaw(varRef, Timestamp.Empty, Timestamp.Max, 1, BoundingMethod.TakeLastN);
        VTQ? latestVTQ = oldVTQ.Count > 0 ? oldVTQ.First() : null;

        Timestamp time;
        double? resultPreviousCalculation = null;

        if (!latestVTQ.HasValue) {

            VariableRef v = Inputs[0].Var;
            List<VTQ> firstVTQ = api.HistorianReadRaw(v, Timestamp.Empty, Timestamp.Max, 1, BoundingMethod.TakeFirstN);

            if (firstVTQ.Count == 0) {
                Console.WriteLine($"No data found for {v}");
                return;
            }

            time = firstVTQ[0].T.Truncate(Resolution) - Resolution;

            if (StartTime.HasValue && StartTime.Value > time) {
                time = StartTime.Value.Truncate(Resolution) - Resolution;
            }

            foreach (InputDef input in Inputs) {
                // reset state values:
                if (input.PT1TimeConstant.HasValue) {
                    Console.WriteLine($"Resetting PT1 state values for {input.Name}");
                    StateFloat64 stateValue = (StateFloat64)InputStates[input.IndexPT1StateValue];
                    StateTimestamp stateTime = (StateTimestamp)InputStates[input.IndexPT1StateTime];
                    stateValue.ValueOrNull = stateValue.DefaultValue;
                    stateTime.ValueOrNull = stateTime.DefaultValue;
                }
            }
        }
        else {
            time = latestVTQ.Value.T;
            resultPreviousCalculation = latestVTQ.Value.V.AsDouble();
        }

        var values = new List<double>();
        var buffer = new List<VTQ>(ChunkSize);

        void DrainBuffer() {
            if (buffer.Count > 0) {
                api.HistorianModify(varRef, ModifyMode.Insert, buffer.ToArray());
                buffer.Clear();
            }
        }

        Timestamp tPreviousCalculation = time;

        while (time < tEnd) {

            time += Resolution;

            bool allValuesFound = true;
            values.Clear();

            foreach (InputDef input in Inputs) {
                double? v = input.GetValueFor(time, Resolution, out bool canAbort);
                if (canAbort) {
                    DrainBuffer();
                    return;
                }
                if (v == null) {
                    // Console.WriteLine($"No value found for {param.Name} at {time}");
                    allValuesFound = false;
                    break;
                }

                if (input.PT1TimeConstant.HasValue) {
                    StateFloat64 stateValue = (StateFloat64)InputStates[input.IndexPT1StateValue];
                    StateTimestamp stateTime = (StateTimestamp)InputStates[input.IndexPT1StateTime];

                    Timestamp tLast = stateTime.ValueOrNull ?? time - Resolution;
                    Duration dt = time - tLast;

                    double T_ms = input.PT1TimeConstant.Value.TotalMilliseconds;
                    double dt_ms = dt.TotalMilliseconds;
                    double alpha = dt_ms / (T_ms + dt_ms);
                    
                    double vPrev = stateValue.ValueOrNull ?? v.Value;
                    v = alpha * v.Value + (1.0 - alpha) * vPrev;
                    stateValue.ValueOrNull = v;
                    stateTime.ValueOrNull = time;
                }

                values.Add(v.Value);
            }

            if (!allValuesFound) {
                continue;
            }

            Duration dt_calc = time - tPreviousCalculation;
            double valueRes = Calculate(values, resultPreviousCalculation, dt_calc, time);
            buffer.Add(VTQ.Make(valueRes, time, Quality.Good));
            tPreviousCalculation = time;
            resultPreviousCalculation = valueRes;

            if (buffer.Count >= ChunkSize) {
                DrainBuffer();
                if (api.abortStep) {
                    Console.WriteLine($"Calculation aborted for {ID} after DrainBuffer()");
                    return;
                }
            }
        }

        DrainBuffer();
    }
}

public record SiblingSignal(string Id);

public record InputDef(string Name, VariableRef? VarRef = null, SiblingSignal? SiblingRef = null)
{
    const int ChunkSize = 5000;

    private readonly List<VTQ> buffer = new(ChunkSize);
    private int bufferStartIdx = 0;

    public VariableRef Var { get; private set; }

    internal void ResolveVarRef(Func<string, VariableRef> siblingResolver) {
        if (VarRef != null) {
            Var = VarRef.Value;
        }
        else if (SiblingRef != null) {
            Var = siblingResolver(SiblingRef.Id);
        }
    }

    private Api? api = null;

    internal void SetApi(Api api) {
        this.api = api;
    }

    public List<VTQ> HistorianReadRaw(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter = QualityFilter.ExcludeNone) {
        if (api == null) {
            throw new Exception("Api not set.");
        }
        return api.HistorianReadRaw(Var, startInclusive, endInclusive, maxValues, bounding, filter);
    }

    public double? GetValueFor(Timestamp tt, Duration interval, out bool canAbort) {

        canAbort = false;

        Timestamp t = tt - (TimeShiftConstant ?? Duration.FromSeconds(0));

        if (buffer.Count == 0 || bufferStartIdx >= buffer.Count || buffer.Last().T < t) {

            bufferStartIdx = 0;
            buffer.Clear();
            buffer.AddRange(HistorianReadRaw(startInclusive: t, Timestamp.Max, ChunkSize, BoundingMethod.TakeFirstN));

            if (buffer.Count == 0) {
                canAbort = true;
                return null;
            }

            if (buffer[0].T == t) {
                bufferStartIdx = 1;
                return buffer[0].V.AsDouble();
            }

            Timestamp afterStartLastInterval = t - interval + Duration.FromMilliseconds(1);
            List<VTQ> last = HistorianReadRaw(startInclusive: afterStartLastInterval, t, 1, BoundingMethod.TakeLastN);

            if (last.Count == 0) {
                return null;
            }

            return last[0].V.AsDouble();
        }

        int idx = FindInSortedBufferIdx(t);
        if (idx < 0) {
            return null;
        }

        VTQ vtqFound = buffer[idx];
        bufferStartIdx = idx + 1;

        Timestamp tFound = vtqFound.T;
        if (tFound > t - interval && tFound <= t) {
            return vtqFound.V.AsDouble();
        }

        return null;
    }

    private int FindInSortedBufferIdx(Timestamp t) {
        // Iterate through buffer and find the VTQ with the largest timestamp equal or smaller than t:
        for (int i = bufferStartIdx; i < buffer.Count; ++i) {
            Timestamp tt = buffer[i].T;
            if (t == tt) {
                return i;
            }
            if (t < tt) {
                return i - 1;
            }
        }
        return buffer.Count - 1;
    }

    internal Duration? PT1TimeConstant = null;
    internal int IndexPT1StateValue = -1;
    internal int IndexPT1StateTime = -1;

    internal Duration? TimeShiftConstant = null;

    internal void AddStates(List<StateBase> states) {
        if (PT1TimeConstant.HasValue) {
            states.Add(new StateFloat64(Name + "_PT1_Value", defaultValue: null) {
                ID = Name + "_PT1_Value"
            });
            IndexPT1StateValue = states.Count - 1;
            states.Add(new StateTimestamp(Name + "_PT1_Time", defaultValue: null) {
                ID = Name + "_PT1_Time"
            });
            IndexPT1StateTime = states.Count - 1;
        }
    }

    public InputDef PT1Filter(Duration duration) {
        PT1TimeConstant = duration;
        return this;
    }

    public InputDef TimeShifted(Duration duration) {
        TimeShiftConstant = duration;
        return this;
    }
}
