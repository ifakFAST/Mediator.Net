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

public sealed class DerivedSignal
{
    public string ID { get; }
    public string Name { get; } = "";
    public string Unit { get; } = "";
    public string ParentFolderID { get; } = "";
    public Duration Resolution { get; } = Duration.FromMinutes(1);
    public InputDef[] Inputs { get; }
    public Delegate Calculation { get; }

    private bool parametersAsArray = false;

    public DerivedSignal(string parentFolderID, InputDef[] inputs, Delegate calculation, Duration resolution, string id = "", string name = "", string unit = "") {
        ID = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        Unit = unit;
        ParentFolderID = parentFolderID;
        Resolution = resolution;
        Inputs = inputs;
        Calculation = calculation;

        InitTest();
    }

    private void InitTest() {

        if (Inputs.Length == 0) {
            throw new ArgumentException("No parameters defined.");
        }

        var method = Calculation.Method;
        var parametersMethod = method.GetParameters();
        if (parametersMethod.Length != Inputs.Length && parametersMethod.Length != 1) {
            throw new ArgumentException("Number of parameters does not match.");
        }

        parametersAsArray = parametersMethod.Length == 1 && parametersMethod[0].ParameterType == typeof(double[]);

        if (!parametersAsArray) {
            // Verify that the parameter types and names match:
            for (int i = 0; i < Inputs.Length; ++i) {
                var paramMethod = parametersMethod[i];
                var param = Inputs[i];
                if (paramMethod.ParameterType != typeof(double)) {
                    throw new ArgumentException("Parameter type does not match.");
                }
                if (paramMethod.Name != param.Name) {
                    throw new ArgumentException("Parameter name does not match.");
                }
            }
        }

        // Verify that the return type is double:
        if (method.ReturnType != typeof(double)) {
            throw new ArgumentException("Return type is not double.");
        }
    }

    public double Calculate(double[] values) {
        return parametersAsArray ?
            (double)Calculation.DynamicInvoke([values])! :
            (double)Calculation.DynamicInvoke(values.Select(v => (object)v).ToArray())!;
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

    private static (T value, string fieldName)[] GetFieldsOfTypeWithFieldName<T>(object script) {
        return script.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(f => f.FieldType == typeof(T))
            .Where(f => f.GetValue(script) != null)
            .Select(f => ((T)f.GetValue(script)!, f.Name))
            .ToArray();
    }

    public static void UpdateDerivedSignals(object script, Timestamp t, Duration dt) {
        foreach (var (value, fieldName) in GetFieldsOfTypeWithFieldName<DerivedSignal>(script)) {
            value.Update(t, fieldName);
        }
    }

    readonly Api api = new();

    private void Update(Timestamp tEnd, string fieldName) {

        if (Inputs.Length == 0) return;

        const int ChunckSize = 5000;

        string signalID = string.IsNullOrWhiteSpace(ID) ? fieldName : ID;
        string signalName = string.IsNullOrWhiteSpace(Name) ? signalID : Name;

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
                double? v = param.GetValueFor(api, time, Resolution);
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

    public double? GetValueFor(Api api, Timestamp t, Duration interval) {

        if (buffer.Count == 0 || bufferStartIdx >= buffer.Count || buffer.Last().T < t) {

            bufferStartIdx = 0;
            buffer.Clear();
            buffer.AddRange(api.HistorianReadRaw(Var, startInclusive: t, Timestamp.Max, ChunckSize, BoundingMethod.TakeFirstN));

            if (buffer.Count == 0) {
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
