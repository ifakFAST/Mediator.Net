// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public class Input : InputFloat64 {

        public Input(string name, string unit = "", double defaultValue = 0.0) :
            base(name: name, unit: unit, defaultValue: defaultValue) {
        }
    }

    public class InputFloat64 : InputBase {

        public double DefaultValue { get; private set; }
        public double Value => VTQ.V.GetDouble(); // AsDouble ?
        public static implicit operator double(InputFloat64 d) => d.Value;

        public InputFloat64(string name, string unit = "", double defaultValue = 0.0) :
            base(name: name, unit: unit, type: DataType.Float64, dimension: 1, defaultValue: DataValue.FromDouble(defaultValue)) {
            DefaultValue = defaultValue;
        }
    }

    public class InputFloat64Array : InputBase {

        public double[] DefaultValue { get; private set; }
        public double[] Value => VTQ.V.GetDoubleArray();
        public static implicit operator double[](InputFloat64Array d) => d.Value;

        public InputFloat64Array(string name, double[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float64, dimension: dimension, defaultValue: DataValue.FromDoubleArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputFloat64Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputFloat64Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }

    public class InputFloat32Array : InputBase {

        public float[] DefaultValue { get; private set; }
        public float[] Value => VTQ.V.GetFloatArray();
        public static implicit operator float[](InputFloat32Array d) => d.Value;

        public InputFloat32Array(string name, float[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float32, dimension: dimension, defaultValue: DataValue.FromFloatArray(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputFloat32Array: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputFloat32Array: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }

    public class InputStruct<T> : InputBase {

        public T DefaultValue { get; private set; }
        public T Value => VTQ.V.Object<T>();
        public static implicit operator T(InputStruct<T> d) => d.Value;

        public InputStruct(string name, T defaultValue) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1, defaultValue: DataValue.FromObject(defaultValue)) {
            DefaultValue = defaultValue;
        }
    }

    public class InputStructArray<T> : InputBase {

        public T[] DefaultValue { get; private set; }
        public T[] Value => VTQ.V.Object<T[]>();
        public static implicit operator T[](InputStructArray<T> d) => d.Value;

        public InputStructArray(string name, T[] defaultValue, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension, defaultValue: DataValue.FromObject(defaultValue)) {
            if (dimension < 0) throw new ArgumentException("InputStructArray: dimension must be >= 0");
            if (dimension != 0 && defaultValue != null && defaultValue.Length != dimension) throw new ArgumentException("InputStructArray: dimension != defaultValue.Length");
            DefaultValue = defaultValue;
        }
    }
}
