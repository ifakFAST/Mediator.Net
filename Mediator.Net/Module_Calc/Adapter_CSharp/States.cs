// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public class State : StateFloat64
    {
        public State(string name, string unit = "", double? defaultValue = 0.0, bool needHighPrecision = false) :
            base(name: name, unit: unit, defaultValue: defaultValue, needHighPrecision: needHighPrecision) {
        }
    }

    public class StateFloat64 : StateBase {

        public double? DefaultValue { get; private set; }
        public double Value {
            get {
                return ValueOrNull ?? throw new Exception($"State {ID}: Value is null");
            }
            set {
                ValueOrNull = value;
            }
        }
        public double? ValueOrNull { get; set; }
        public static implicit operator double(StateFloat64 d) => d.Value;
        public bool NeedHighPrecision { get; set; }

        public StateFloat64(string name, string unit = "", double? defaultValue = 0.0, bool needHighPrecision = false) :
            base(name: name, unit: unit, type: DataType.Float64, dimension: 1, defaultValue: defaultValue.HasValue ? DataValue.FromDouble(defaultValue.Value) : DataValue.Empty) {
            DefaultValue = defaultValue;
            ValueOrNull = defaultValue;
            NeedHighPrecision = needHighPrecision;
        }

        internal override DataValue GetValue() {
            if (!ValueOrNull.HasValue) return DataValue.Empty;
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
            try {
                ValueOrNull = v.IsEmpty ? (double?)null : v.GetDouble();
            }
            catch (Exception) {
                ValueOrNull = DefaultValue;
            }
        }
    }

    public class StateFloat64Array : StateBase {

        public double[] DefaultValue { get; private set; }
        public double[] Value { get; set; }
        public static implicit operator double[](StateFloat64Array d) => d.Value;
        public bool NeedHighPrecision { get; set; }

        public StateFloat64Array(string name, double[] defaultValue, int dimension = 0, bool needHighPrecision = false) :
            base(name: name, unit: "", type: DataType.Float64, dimension: dimension, defaultValue: DataValue.FromDoubleArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("StateFloat64Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("StateFloat64Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
            Value = defaultValue;
            NeedHighPrecision = needHighPrecision;
        }

        internal override DataValue GetValue() {
            if (Value == null) return DataValue.Empty;
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
            try {
                Value = v.GetDoubleArray();
            }
            catch(Exception) {
                Value = DefaultValue;
            }
        }
    }

    public class StateFloat32Array : StateBase
    {
        public float[] DefaultValue { get; private set; }
        public float[] Value { get; set; }
        public static implicit operator float[](StateFloat32Array d) => d.Value;

        public StateFloat32Array(string name, float[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float32, dimension: dimension, defaultValue: DataValue.FromFloatArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("StateFloat32Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("StateFloat32Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() => DataValue.FromFloatArray(Value);

        internal override void SetValueFromDataValue(DataValue v) {
            try {
                Value = v.GetFloatArray();
            }
            catch (Exception) {
                Value = DefaultValue;
            }
        }
    }

    public class StateStruct<T> : StateBase where T : struct {

        public T? DefaultValue { get; private set; }

        public T Value {
            get {
                return ValueOrNull ?? throw new Exception($"State {ID}: Value is null");
            }
            set {
                ValueOrNull = value;
            }
        }
        public T? ValueOrNull { get; set; }
        public static implicit operator T(StateStruct<T> d) => d.Value;

        public StateStruct(string name, T? defaultValue) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
            ValueOrNull = defaultValue;
        }

        internal override DataValue GetValue() {
            return DataValue.FromObject(ValueOrNull);
        }

        internal override void SetValueFromDataValue(DataValue v) {
            try {
                ValueOrNull = v.Object<T?>();
            }
            catch (Exception) {
                ValueOrNull = DefaultValue;
            }
        }
    }

    public class StateClass<T> : StateBase where T : class {

        public T DefaultValue { get; private set; }
        public T Value { get; set; }
        public static implicit operator T(StateClass<T> d) => d.Value;

        public StateClass(string name, T defaultValue) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() {
            return DataValue.FromObject(Value);
        }

        internal override void SetValueFromDataValue(DataValue v) {
            try {
                Value = v.Object<T>();
            }
            catch (Exception) {
                Value = DefaultValue;
            }
        }
    }

    public class StateStructArray<T> : StateBase where T : struct {

        public T[] DefaultValue { get; private set; }
        public T[] Value { get; set; }
        public static implicit operator T[](StateStructArray<T> d) => d.Value;

        public StateStructArray(string name, T[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("StateStructArray: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("StateStructArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() {
            return DataValue.FromObject(Value);
        }

        internal override void SetValueFromDataValue(DataValue v) {
            try {
                Value = v.Object<T[]>();
            }
            catch (Exception) {
                Value = DefaultValue;
            }
        }
    }

    public class StateClassArray<T> : StateBase where T : class {

        public T[] DefaultValue { get; private set; }
        public T[] Value { get; set; }
        public static implicit operator T[](StateClassArray<T> d) => d.Value;

        public StateClassArray(string name, T[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("StateClassArray: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("StateClassArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
            Value = defaultValue;
        }

        internal override DataValue GetValue() {
            return DataValue.FromObject(Value);
        }

        internal override void SetValueFromDataValue(DataValue v) {
            try {
                Value = v.Object<T[]>();
            }
            catch (Exception) {
                Value = DefaultValue;
            }
        }
    }
}
