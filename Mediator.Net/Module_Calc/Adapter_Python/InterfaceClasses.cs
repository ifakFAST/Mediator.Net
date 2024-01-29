// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp;

namespace Ifak.Fast.Mediator.Calc.Adapter_Python;

public class PyInputBase : InputBase {

    public PyInputBase(string name, string unit, DataType type, int dimension, DataValue defaultValue) 
        : base(name, unit, type, dimension, defaultValue) {
    }

    public double GetTimestamp() {
        // secondsSinceEpoch
        return Time.JavaTicks / 1000.0;
    }
}

public class PyOutputBase : OutputBase {

    public PyOutputBase(string name, string unit, DataType type, int dimension) 
        : base(name, unit, type, dimension) {
    }

    public void SetTime(double secondsSinceEpoch) {
        DateTime dt = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        dt = dt.AddSeconds(secondsSinceEpoch);
        Time = Timestamp.FromDateTime(dt);
    }

    public void SetValue(DataValue value) {
        VTQ = VTQ.WithValue(value);
    }
}

public class PyStateBase : AbstractState {

    protected DataType Type { get; private set; }
    protected int Dimension { get; private set; }
    protected DataValue theDefaultValue { get; private set; }

    public bool IsNull => GetValue().IsEmpty;
    public bool NonNull => GetValue().NonEmpty;

    protected DataValue theValue;

    protected PyStateBase(string name, string unit, DataType type, int dimension, DataValue defaultValue) {
        Name = name;
        Unit = unit;
        Type = type;
        Dimension = dimension;
        theDefaultValue = defaultValue;
        theValue = defaultValue;
    }

    internal override DataValue GetDefaultValue() => theDefaultValue;

    internal override int GetDimension() => Dimension;

    internal override DataType GetDataType() => Type;

    internal override DataValue GetValue() => theValue;

    internal override void SetValueFromDataValue(DataValue v) {
        theValue = v;
    }
}


