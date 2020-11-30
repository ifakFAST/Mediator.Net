// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public class Output : OutputFloat64
    {
        public Output(string name, string unit = "", double defaultValue = 0.0, int? roundDigits = 6) :
            base(name: name, unit: unit, defaultValue: defaultValue, roundDigits: roundDigits) {
        }
    }

    public class OutputFloat64 : OutputBase {

        public double DefaultValue { get; private set; }
        public static implicit operator double(OutputFloat64 d) => d.Value;
        public int? RoundDigits { get; set; }

        public OutputFloat64(string name, string unit = "", double defaultValue = 0.0, int? roundDigits = 6) :
            base(name: name, unit: unit, type: DataType.Float64, dimension: 1, defaultValue: DataValue.FromDouble(defaultValue)) {
            RoundDigits = roundDigits;
            DefaultValue = defaultValue;
        }

        public double Value {
            get => VTQ.V.GetDouble();
            set {
                double v = value;
                if (RoundDigits.HasValue) {
                    try {
                        v = Math.Round(v, RoundDigits.Value);
                    }
                    catch (Exception) { }
                }
                VTQ = VTQ.WithValue(DataValue.FromDouble(v));
            }
        }
    }

    public class OutputFloat64Array : OutputBase {

        public double[] DefaultValue { get; private set; }
        public static implicit operator double[](OutputFloat64Array d) => d.Value;

        public OutputFloat64Array(string name, double[] defaultValue = null, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float64, dimension: dimension, defaultValue: DataValue.FromDoubleArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("OutputFloat64Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("OutputFloat64Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }

        public double[] Value {
            get => VTQ.V.GetDoubleArray();
            set {
                VTQ = VTQ.WithValue(DataValue.FromDoubleArray(value));
            }
        }
    }

    public class OutputFloat32Array : OutputBase {

        public float[] DefaultValue { get; private set; }
        public static implicit operator float[](OutputFloat32Array d) => d.Value;

        public OutputFloat32Array(string name, float[] defaultValue = null, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float32, dimension: dimension, defaultValue: DataValue.FromFloatArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("OutputFloat32Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("OutputFloat32Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }

        public float[] Value {
            get => VTQ.V.GetFloatArray();
            set {
                VTQ = VTQ.WithValue(DataValue.FromFloatArray(value));
            }
        }
    }

    public class OutputStruct<T>: OutputBase {

        public T DefaultValue { get; private set; }
        public static implicit operator T(OutputStruct<T> d) => d.Value;

        public OutputStruct(string name, T defaultValue = default(T)) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
        }

        public T Value {
            get => VTQ.V.Object<T>();
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputStruct : OutputBase {

        public DataValue DefaultValue { get; private set; }
        public static implicit operator DataValue(OutputStruct d) => d.Value;

        public OutputStruct(string name, DataValue defaultValue = default(DataValue)) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: defaultValue) {
            DefaultValue = defaultValue;
        }

        public DataValue Value {
            get => VTQ.V;
            set {
                VTQ = VTQ.WithValue(value);
            }
        }
    }

    public class OutputStructArray<T> : OutputBase {

        public T[] DefaultValue { get; private set; }
        public static implicit operator T[](OutputStructArray<T> d) => d.Value;

        public OutputStructArray(string name, T[] defaultValue = null, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("OutputStructArray: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("OutputStructArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }

        public T[] Value {
            get => VTQ.V.Object<T[]>();
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputStructArray : OutputBase {

        public DataValue DefaultValue { get; private set; }
        public static implicit operator DataValue(OutputStructArray d) => d.Value;

        public OutputStructArray(string name, DataValue defaultValue = default(DataValue), int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: defaultValue) {
            if (dimension < 0) throw new ArgumentException("OutputStructArray: dimension must be >= 0");
            if (defaultValue.NonEmpty && !defaultValue.IsArray) throw new ArgumentException("OutputStructArray: defaultValue is not an array");
            if (dimension != 0 && defaultValue.NonEmpty && defaultValue.ArrayLength != dimension) throw new ArgumentException("OutputStructArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }

        public DataValue Value {
            get => VTQ.V;
            set {
                VTQ = VTQ.WithValue(value);
            }
        }
    }
}
