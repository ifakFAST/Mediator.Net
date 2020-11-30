// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public class State : StateFloat64
    {
        public State(string name, string unit = "", double defaultValue = 0.0, bool needHighPrecision = false) :
            base(name: name, unit: unit, defaultValue: defaultValue, needHighPrecision: needHighPrecision) {
        }
    }

    public class StateFloat64 : StateBase {

        public double DefaultValue { get; private set; }
        public double Value { get; set; }
        public static implicit operator double(StateFloat64 d) => d.Value;
        public bool NeedHighPrecision { get; set; }

        public StateFloat64(string name, string unit = "", double defaultValue = 0.0, bool needHighPrecision = false) :
            base(name: name, unit: unit, type: DataType.Float64, dimension: 1, defaultValue: DataValue.FromDouble(defaultValue)) {
            DefaultValue = defaultValue;
            Value = defaultValue;
            NeedHighPrecision = needHighPrecision;
        }

        internal override DataValue GetValue() {
            if (NeedHighPrecision) {
                return DataValue.FromDouble(Value);
            }
            else {
                try {
                    float f = (float)Value;
                    return DataValue.FromFloat(f);
                }
                catch (Exception) {
                    return DataValue.FromDouble(Value);
                }
            }
        }

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.GetDouble();
        }
    }

    public class StateFloat64Array : StateBase {

        public double[] DefaultValue { get; private set; }
        public double[] Value { get; set; }
        public static implicit operator double[](StateFloat64Array d) => d.Value;
        public bool NeedHighPrecision { get; set; }

        public StateFloat64Array(string name, double[] defaultValue, int dimension = 0, bool needHighPrecision = false) :
            base(name: name, unit: "", type: DataType.Float64, dimension: dimension, defaultValue: DataValue.FromDoubleArray(defaultValue)) {
            DefaultValue = defaultValue;
            Value = defaultValue;
            NeedHighPrecision = needHighPrecision;
        }

        internal override DataValue GetValue() {
            if (NeedHighPrecision) {
                return DataValue.FromDoubleArray(Value);
            }
            else {
                try {
                    double[] array = Value;
                    float[] floatArr = new float[array.Length];
                    for (int i = 0; i < array.Length; ++i) {
                        floatArr[i] = (float)array[i];
                    }
                    return DataValue.FromFloatArray(floatArr);
                }
                catch (Exception) {
                    return DataValue.FromDoubleArray(Value);
                }
            }
        }

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.GetDoubleArray();
        }
    }

    public class StateFloat32Array : StateBase
    {
        public float[] DefaultValue { get; private set; }
        public float[] Value { get; set; }
        public static implicit operator float[](StateFloat32Array d) => d.Value;

        public StateFloat32Array(string name, float[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float32, dimension: dimension, defaultValue: DataValue.FromFloatArray(defaultValue)) {
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() => DataValue.FromFloatArray(Value);

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.GetFloatArray();
        }
    }

    public class StateStruct<T> : StateBase {

        public T DefaultValue { get; private set; }
        public T Value { get; set; }
        public static implicit operator T(StateStruct<T> d) => d.Value;

        public StateStruct(string name, T defaultValue = default(T)) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() {
            return DataValue.FromObject(Value);
        }

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.Object<T>();
        }
    }

    public class StateStructArray<T> : StateBase {

        public T[] DefaultValue { get; private set; }
        public T[] Value { get; set; }
        public static implicit operator T[](StateStructArray<T> d) => d.Value;

        public StateStructArray(string name, T[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() {
            return DataValue.FromObject(Value);
        }

        internal override void SetValueFromDataValue(DataValue v) {
            Value = v.Object<T[]>();
        }
    }
}
