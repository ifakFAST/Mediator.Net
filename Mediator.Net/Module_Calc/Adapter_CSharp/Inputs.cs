// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public class Input : InputFloat64 {

        public Input(string name, string unit = "", double? defaultValue = 0.0) :
            base(name: name, unit: unit, defaultValue: defaultValue) {
        }
    }

    public class InputFloat64 : InputBase {

        public double? DefaultValue { get; private set; }
        public double Value {
            get {
                var x = ValueOrNull;
                if (x.HasValue) return x.Value;
                if (IsNull) throw new Exception($"Input {ID}: Value is null");
                throw new Exception($"Input {ID}: Value is not a double value: {VTQ.V.JSON}");
            }
        }
        public double? ValueOrNull => VTQ.V.AsDouble();
        public bool HasValidValue => ValueOrNull.HasValue;

        public static implicit operator double(InputFloat64 d) => d.Value;

        public InputFloat64(string name, string unit = "", double? defaultValue = 0.0) :
            base(name: name, unit: unit, type: DataType.Float64, dimension: 1, defaultValue: defaultValue.HasValue ? DataValue.FromDouble(defaultValue.Value) : DataValue.Empty) {
            DefaultValue = defaultValue;
        }
    }

    public class InputFloat64Array : InputBase {

        public double[]? DefaultValue { get; private set; }
        public double[]? Value {
            get {
                try {
                    return VTQ.V.GetDoubleArray();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a double array: {VTQ.V.JSON}");
                }
            }
        }
        public static implicit operator double[]?(InputFloat64Array d) => d.Value;
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public InputFloat64Array(string name, double[]? defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float64, dimension: dimension, defaultValue: DataValue.FromDoubleArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputFloat64Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputFloat64Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }

    public class InputFloat32Array : InputBase {

        public float[]? DefaultValue { get; private set; }
        public float[]? Value {
            get {
                try {
                    return VTQ.V.GetFloatArray();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a float array: {VTQ.V.JSON}");
                }
            }
        }
        public static implicit operator float[]?(InputFloat32Array d) => d.Value;
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public InputFloat32Array(string name, float[]? defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float32, dimension: dimension, defaultValue: DataValue.FromFloatArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputFloat32Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputFloat32Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }

    public class InputStruct<T> : InputBase where T : struct {

        public T? DefaultValue { get; private set; }
        public T Value {
            get {
                T? x = ValueOrNull;
                if (x.HasValue) return x.Value;
                if (IsNull) throw new Exception($"Input {ID}: Value is null");
                throw new Exception($"Input {ID}: Value is not a {typeof(T).Name} struct value: {VTQ.V.JSON}");
            }
        }
        public T? ValueOrNull {
            get {
                try {
                    return VTQ.V.Object<T?>();
                }
                catch (Exception) {
                    return null;
                }
            }
        }
        public bool HasValidValue => ValueOrNull.HasValue;
        public static implicit operator T(InputStruct<T> d) => d.Value;

        public InputStruct(string name, T? defaultValue) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
        }
    }

    public class InputStructArray<T> : InputBase where T : struct {

        public T[]? DefaultValue { get; private set; }
        public T[]? Value {
            get {
                try {
                    return VTQ.V.Object<T[]>();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a {typeof(T).Name} struct array: {VTQ.V.JSON}");
                }
            }
        }
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public static implicit operator T[]?(InputStructArray<T> d) => d.Value;

        public InputStructArray(string name, T[]? defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputStructArray: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputStructArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }

    public class InputClass<T> : InputBase where T : class {

        public T? DefaultValue { get; private set; }
        public T? Value {
            get {
                try {
                    return VTQ.V.Object<T>();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a {typeof(T).Name} value: {VTQ.V.JSON}");
                }
            }
        }
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public static implicit operator T?(InputClass<T> d) => d.Value;

        public InputClass(string name, T? defaultValue) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
        }
    }

    public class InputClassArray<T> : InputBase where T : class {

        public T[]? DefaultValue { get; private set; }
        public T[]? Value {
            get {
                try {
                    return VTQ.V.Object<T[]>();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a {typeof(T).Name} array: {VTQ.V.JSON}");
                }
            }
        }
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public static implicit operator T[]?(InputClassArray<T> d) => d.Value;

        public InputClassArray(string name, T[]? defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputClassArray: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputClassArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }

    public class InputString : InputBase {

        public string? DefaultValue { get; private set; }
        public string? Value {
            get {
                try {
                    return VTQ.V.GetString();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a string value: {VTQ.V.JSON}");
                }
            }
        }
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public static implicit operator string?(InputString d) => d.Value;

        public InputString(string name, string? defaultValue) :
            base(name: name, unit: "", type: DataType.String, dimension: 1, defaultValue: DataValue.FromString(defaultValue)) {
            DefaultValue = defaultValue;
        }
    }

    public class InputTimestamp : InputBase {

        public Timestamp? DefaultValue { get; private set; }
        public Timestamp? Value {
            get {
                try {
                    return VTQ.V.GetTimestampOrNull();
                }
                catch (Exception) {
                    throw new Exception($"Input {ID}: Value is not a timestamp value: {VTQ.V.JSON}");
                }
            }
        }
        public bool HasValidValue {
            get {
                try {
                    return Value != null;
                }
                catch (Exception) {
                    return false;
                }
            }
        }
        public static implicit operator Timestamp?(InputTimestamp d) => d.Value;

        public InputTimestamp(string name, Timestamp? defaultValue) :
            base(name: name, unit: "", type: DataType.Timestamp, dimension: 1, defaultValue: defaultValue.HasValue ? DataValue.FromTimestamp(defaultValue.Value) : DataValue.Empty) {
            DefaultValue = defaultValue;
        }
    }
}
