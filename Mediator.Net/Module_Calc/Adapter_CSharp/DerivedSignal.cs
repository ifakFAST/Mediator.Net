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
    public Duration Resolution { get; } = Duration.FromMinutes(1);
    public InputDef[] Inputs { get; }
    public readonly StateFloat64[] States;
    public Delegate Calculation { get; }

    private bool parametersAsArray = false;
    private readonly object?[] calcParameterValues;
    private readonly MethodInfo calcMethod;

    public DerivedSignal(string parentFolderID, InputDef[] inputs, Delegate calculation, Duration resolution, string fullID = "", string fullName = "", string unit = "") {
        FullID = fullID;
        FullName = string.IsNullOrWhiteSpace(fullName) ? fullID : fullName;
        Unit = unit;
        ParentFolderID = parentFolderID;
        Resolution = resolution;
        Inputs = inputs;
        Calculation = calculation;

        States = InitTest();
        calcParameterValues = new object?[(parametersAsArray ? 1 : Inputs.Length) + States.Length];
        calcMethod = Calculation.Method;
    }

    private StateFloat64[] InitTest() {

        if (Inputs.Length == 0) {
            throw new ArgumentException("No parameters defined.");
        }

        MethodInfo method = Calculation.Method;
        ParameterInfo[] parameters = method.GetParameters();
        ParameterInfo[] parametersNotRef = parameters.Where(p => !p.ParameterType.IsByRef).ToArray();
        ParameterInfo[] parametersByRef = parameters.Where(p => p.ParameterType.IsByRef).ToArray();

        if (parametersNotRef.Length != Inputs.Length && parametersNotRef.Length != 1) {
            throw new ArgumentException("Number of parameters does not match.");
        }

        // verify that all by ref parameters come after all non by ref parameters:
        if (parametersByRef.Length > 0) {
            int idx = Array.IndexOf(parameters, parametersByRef[0]);
            if (idx < parametersNotRef.Length) {
                throw new ArgumentException("ByRef parameters must come after all non ByRef parameters.");
            }
        }

        parametersAsArray = parametersNotRef.Length == 1 && parametersNotRef[0].ParameterType == typeof(double[]);

        if (!parametersAsArray) {
            // Verify that the parameter types and names match:
            for (int i = 0; i < Inputs.Length; ++i) {
                var paramMethod = parameters[i];
                var param = Inputs[i];
                if (paramMethod.ParameterType != typeof(double)) {
                    throw new ArgumentException("Parameter type does not match.");
                }
                if (paramMethod.Name != param.Name) {
                    throw new ArgumentException("Parameter name does not match.");
                }
            }
        }

        List<StateFloat64> states = [];

        foreach (var param in parametersByRef) {
            string name = param.Name ?? throw new ArgumentException("Parameter name is missing.");
            Type type = param.ParameterType;
            Type elementType = type.GetElementType()!;
            bool isNullable = elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(Nullable<>);
            double ? defaultValue = isNullable ? null : 0.0;
            StateFloat64 state = new(name, defaultValue: defaultValue) {
                ID = name
            };
            Console.WriteLine($"Adding state {state.ID} with default value {state.DefaultValue}");
            states.Add(state);
        }

        // Verify that the return type is double:
        if (method.ReturnType != typeof(double)) {
            throw new ArgumentException("Return type is not double.");
        }

        return states.ToArray();
    }

    public double Calculate(double[] values) {

        int numberOfInputParameters;

        if (parametersAsArray) {
            calcParameterValues[0] = values;
            numberOfInputParameters = 1;
        }
        else {
            for (int i = 0; i < values.Length; ++i) {
                calcParameterValues[i] = values[i];
            }
            numberOfInputParameters = values.Length;
        }

        for (int i = 0; i < States.Length; ++i) {
            calcParameterValues[numberOfInputParameters + i] = States[i].ValueOrNull;
        }

        double result = (double)calcMethod.Invoke(Calculation.Target, calcParameterValues)!;

        for (int i = 0; i < States.Length; ++i) {
            object? updatedStateValue = calcParameterValues[numberOfInputParameters + i];
            double? value = (double?)updatedStateValue;
            States[i].ValueOrNull = value;
        }

        return result;
    }

    public static InputDef DefineInput(string name, VariableRef var) {
        return new InputDef(name, var);
    }

    public static InputDef DefineInput(VariableRef var) {
        return new InputDef("", var);
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
            signal.Update(tEnd);
        }
    }

    readonly Api api = new();

    public void Update(Timestamp tEnd) {

        if (Inputs.Length == 0) return;

        const int ChunckSize = 5000;

        string signalID   = string.IsNullOrWhiteSpace(FullID)   ? ID   : FullID;
        string signalName = string.IsNullOrWhiteSpace(FullName) ? Name : FullName;

        var signalInfo = new SignalInfo(signalID) {
            Name = signalName,
            Unit = Unit,
        };

        VariableRef varRef = api.CreateSignalIfNotExists(ParentFolderID, signalInfo);

        List<VTQ> oldVTQ = api.HistorianReadRaw(varRef, Timestamp.Empty, Timestamp.Max, 1, BoundingMethod.TakeLastN);

        Timestamp time;

        if (oldVTQ.Count == 0) {

            VariableRef v = Inputs[0].Var;
            List<VTQ> firstVTQ = api.HistorianReadRaw(v, Timestamp.Empty, Timestamp.Max, 1, BoundingMethod.TakeFirstN);

            if (firstVTQ.Count == 0) {
                Console.WriteLine($"No data found for {v}");
                return;
            }

            time = firstVTQ[0].T - Resolution;
        }
        else {
            time = oldVTQ[0].T;
        }

        var values = new List<double>();
        var buffer = new List<VTQ>(ChunckSize);

        while (time < tEnd) {

            time += Resolution;

            bool allValuesFound = true;
            values.Clear();

            foreach (var param in Inputs) {
                double? v = param.GetValueFor(api, time, Resolution, out bool canAbort);
                if (canAbort) {
                    return;
                }
                if (v == null) {
                    // Console.WriteLine($"No value found for {param.Name} at {time}");
                    allValuesFound = false;
                    break;
                }
                values.Add(v.Value);
            }

            if (!allValuesFound) {
                continue;
            }

            double valueRes = Calculate(values.ToArray());
            buffer.Add(VTQ.Make(valueRes, time, Quality.Good));

            if (buffer.Count >= ChunckSize) {
                api.HistorianModify(varRef, ModifyMode.Insert, buffer.ToArray());
                buffer.Clear();
            }
        }

        if (buffer.Count > 0) {
            api.HistorianModify(varRef, ModifyMode.Insert, buffer.ToArray());
            buffer.Clear();
        }
    }
}

public record InputDef(string Name, VariableRef Var)
{
    const int ChunckSize = 5000;

    private readonly List<VTQ> buffer = new(ChunckSize);
    private int bufferStartIdx = 0;

    public double? GetValueFor(Api api, Timestamp t, Duration interval, out bool canAbort) {

        canAbort = false;

        if (buffer.Count == 0 || bufferStartIdx >= buffer.Count || buffer.Last().T < t) {

            bufferStartIdx = 0;
            buffer.Clear();
            buffer.AddRange(api.HistorianReadRaw(Var, startInclusive: t, Timestamp.Max, ChunckSize, BoundingMethod.TakeFirstN));

            if (buffer.Count == 0) {
                canAbort = true;
                return null;
            }

            if (buffer[0].T == t) {
                bufferStartIdx = 1;
                return buffer[0].V.AsDouble();
            }

            Timestamp afterStartLastInterval = t - interval + Duration.FromMilliseconds(1);
            List<VTQ> last = api.HistorianReadRaw(Var, startInclusive: afterStartLastInterval, t, 1, BoundingMethod.TakeLastN);

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
}
