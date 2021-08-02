// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    public class Output : OutputFloat64
    {
        public Output(string name, string unit = "", int? roundDigits = 6) :
            base(name: name, unit: unit, roundDigits: roundDigits) {
        }
    }

    public class OutputFloat64 : OutputBase {

        public int? RoundDigits { get; set; }

        public OutputFloat64(string name, string unit = "", int? roundDigits = 6) :
            base(name: name, unit: unit, type: DataType.Float64, dimension: 1) {
            RoundDigits = roundDigits;
        }

        public double? Value {
            set {
                if (!value.HasValue) {
                    VTQ = VTQ.WithValue(DataValue.Empty);
                }
                else {
                    double v = value.Value;
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
    }

    public class OutputFloat64Array : OutputBase {

        public OutputFloat64Array(string name, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float64, dimension: dimension) {
            if (dimension < 0) throw new ArgumentException("OutputFloat64Array: dimension must be >= 0");
        }

        public double[] Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromDoubleArray(value));
            }
        }
    }

    public class OutputFloat32Array : OutputBase {

        public OutputFloat32Array(string name, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Float32, dimension: dimension) {
            if (dimension < 0) throw new ArgumentException("OutputFloat32Array: dimension must be >= 0");
        }

        public float[] Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromFloatArray(value));
            }
        }
    }

    public class OutputStruct<T>: OutputBase where T: struct {

        public OutputStruct(string name) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1) {
        }

        public T? Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputClass<T> : OutputBase where T : class {

        public OutputClass(string name) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1) {
        }

        public T Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputString : OutputBase
    {
        public OutputString(string name) :
            base(name: name, unit: "", type: DataType.String, dimension: 1) {
        }

        public string Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromString(value));
            }
        }
    }

    public class OutputStruct : OutputBase {

        public OutputStruct(string name) :
            base(name: name, unit: "", type: DataType.Struct, dimension: 1) {
        }

        public object Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputStructArray<T> : OutputBase where T : struct {

        public OutputStructArray(string name, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension) {
            if (dimension < 0) throw new ArgumentException("OutputStructArray: dimension must be >= 0");
        }

        public T[] Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputClassArray<T> : OutputBase where T : class {

        public OutputClassArray(string name, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension) {
            if (dimension < 0) throw new ArgumentException("OutputClassArray: dimension must be >= 0");
        }

        public T[] Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }

    public class OutputStructArray : OutputBase {

        public OutputStructArray(string name, int dimension = 0) :
            base(name: name, unit: "", type: DataType.Struct, dimension: dimension) {
            if (dimension < 0) throw new ArgumentException("OutputStructArray: dimension must be >= 0");
        }

        public Array Value {
            set {
                VTQ = VTQ.WithValue(DataValue.FromObject(value));
            }
        }
    }
}
