// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;

namespace Ifak.Fast.Mediator.Calc.Config;

[XmlRoot(Namespace = "Module_Calc", ElementName = "Calc_Model")]
public class Calc_Model : ModelObject
{
    [XmlIgnore]
    public string ID { get; set; } = "Root";

    [XmlIgnore]
    public string Name { get; set; } = "Calc_Model";

    public Folder RootFolder { get; set; } = new Folder("RootFolder", "RootFolder");

    public List<Calculation> GetAllCalculations() {
        return RootFolder == null ? new List<Calculation>() : RootFolder.GetAllCalculations();
    }

    public void Normalize(IEnumerable<AdapterInfo> adapters) {
        RootFolder.Normalize(adapters);
    }
}

public class Folder : ModelObject
{
    public Folder() { }

    public Folder(string id, string name) {
        ID = id;
        Name = name;
    }

    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    public History? History { get; set; } = null;

    public List<Folder> Folders { get; set; } = new List<Folder>();

    public List<Signal> Signals { get; set; } = new List<Signal>();

    public List<Calculation> Calculations { get; set; } = new List<Calculation>();

    public List<Calculation> GetAllCalculations() {
        var res = new List<Calculation>();
        foreach (Folder f in Folders)
            res.AddRange(f.GetAllCalculations());
        res.AddRange(Calculations);
        return res;
    }

    public bool ShouldSerializeHistory() => History.HasValue;
    public bool ShouldSerializeFolders() => Folders != null && Folders.Count > 0;
    public bool ShouldSerializeSignals() => Signals != null && Signals.Count > 0;
    public bool ShouldSerializeCalculations() => Calculations != null && Calculations.Count > 0;

    public void Normalize(IEnumerable<AdapterInfo> adapters) {
        if (History.HasValue) {
            History = History.Value.Normalize();
        }
        foreach (var folder in Folders) {
            folder.Normalize(adapters);
        }
        foreach (var signal in Signals) {
            signal.Normalize();
        }
        foreach (var calc in Calculations) {
            calc.Normalize(adapters);
        }
        if (string.IsNullOrWhiteSpace(Name)) {
            Name = ID;
        }
    }
}

public class Signal : ModelObject
{
    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    [XmlAttribute("unit")]
    public string Unit { get; set; } = "";

    [XmlAttribute("type")]
    public DataType Type { get; set; } = DataType.Float64;

    [XmlAttribute("typeConstraints")]
    public string TypeConstraints { get; set; } = "";

    [XmlAttribute("dimension")]
    public int Dimension { get; set; } = 1;

    public DataValue GetDefaultValue() => DataValue.FromDataType(Type, Dimension);

    // SignalType, e.g. Measurement, KPI, Setpoint ?

    public LocationRef? Location { get; set; } = null; // e.g. Influent/North Side

    public string Comment { get; set; } = "";

    //public VariableRef? ValueSource { get; set; } = null;

    public History? History { get; set; } = null;

    //public bool ShouldSerializeValueSource() => ValueSource.HasValue;
    public bool ShouldSerializeHistory() => History.HasValue;
    public bool ShouldSerializeComment() => !string.IsNullOrEmpty(Comment);
    public bool ShouldSerializeLocation() => Location.HasValue;
    public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);
    public bool ShouldSerializeDimension() => Dimension != 1;
    public bool ShouldSerializeType() => Type != DataType.Float64;
    public bool ShouldSerializeTypeConstraints() => !string.IsNullOrEmpty(TypeConstraints);
    protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

        History history;

        if (History.HasValue) {
            history = History.Value;
        }
        else {
            history = new History(HistoryMode.None);
            foreach (IModelObject obj in parents) {
                if (obj is Folder folder && folder.History.HasValue) {
                    history = folder.History.Value;
                    break;
                }
            }
        }

        var variable = new Variable(
                name: "Value",
                type: Type,
                defaultValue: GetDefaultValue(),
                unit: Unit,
                typeConstraints: TypeConstraints,
                dimension: Dimension,
                dimensionNames: new string[0],
                remember: true,
                history: history);

        return new Variable[] { variable };
    }

    public void Normalize() {
        if (History.HasValue) {
            History = History.Value.Normalize();
        }
        if (string.IsNullOrWhiteSpace(Name)) {
            Name = ID;
        }
    }
}

public enum RunMode {
    Continuous,
    Triggered,
    InputDriven,
}

public enum InitErrorResponse {
    Fail,
    Retry,
    Stop,
}

public enum HistoryScope
{
    All,
    ExcludeInputs,
    ExcludeStates,
    ExcludeInputsAndStates,
}

public static class HistoryScopeExtensions
{
    public static bool ExcludeInputs(this HistoryScope scope) {
        return scope == HistoryScope.ExcludeInputs || scope == HistoryScope.ExcludeInputsAndStates;
    }

    public static bool ExcludeStates(this HistoryScope scope) {
        return scope == HistoryScope.ExcludeStates || scope == HistoryScope.ExcludeInputsAndStates;
    }
}

public class Calculation : ModelObject
{
    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    [XmlAttribute("type")]
    public string Type { get; set; } = ""; // e.g. C#, SIMBA, ...

    [XmlAttribute("subtype")]
    public string Subtype { get; set; } = ""; // e.g. Control, OnlineSimulation, ...

    [XmlAttribute("enabled")]
    public bool Enabled { get; set; } = false;

    public RunMode RunMode { get; set; } = RunMode.Continuous;

    public Duration? MaxInputAge { get; set; } = null; // if input is older than this, it will be replaced with null

    public InitErrorResponse InitErrorResponse { get; set; } = InitErrorResponse.Retry;

    public HistoryScope HistoryScope { get; set; } = HistoryScope.All;

    public bool EnableOutputVarWrite { get; set; } = true;

    public History? History { get; set; } = null;

    public bool WindowVisible { get; set; } = false;

    public Timestamp? InitialStartTime { get; set; } = null; // only used when RunMode == InputDriven

    public Duration Cycle { get; set; } = Duration.FromSeconds(10);

    public Duration Offset { get; set; } = Duration.FromSeconds(0);

    public bool IgnoreOffsetForTimestamps { get; set; } = false;

    public double RealTimeScale { get; set; } = 1;

    public string Definition { get; set; } = ""; // e.g. C# code, SIMBA project file name

    public List<Input> Inputs { get; set; } = new List<Input>();

    public List<Output> Outputs { get; set; } = new List<Output>();

    public List<State> States { get; set; } = new List<State>();

    public bool ShouldSerializeMaxInputAge() => MaxInputAge.HasValue;
    public bool ShouldSerializeOffset() => Offset.TotalMilliseconds != 0;
    public bool ShouldSerializeIgnoreOffsetForTimestamps() => IgnoreOffsetForTimestamps;
    public bool ShouldSerializeRunMode() => RunMode != RunMode.Continuous;
    public bool ShouldSerializeInitErrorResponse() => InitErrorResponse != InitErrorResponse.Retry;
    public bool ShouldSerializeHistoryScope() => HistoryScope != HistoryScope.All;

    public Calc.Calculation ToCalculation() {
        return new Calc.Calculation() {
            ID = ID,
            Name = Name,
            Cycle = Cycle,
            Offset = Offset,
            Definition = Definition,
            RealTimeScale = RealTimeScale,
            WindowVisible = WindowVisible,
            Subtype = Subtype,
        };
    }

    protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

        History history = new History(HistoryMode.None);

        if (History.HasValue) {
            history = History.Value;
        }
        else {
            foreach (IModelObject obj in parents) {
                if (obj is Folder f && f.History.HasValue) {
                    history = f.History.Value;
                    break;
                }
            }
        }

        var varLastRunDuration = new Variable() {
            Name = "LastRunDuration",
            Type = DataType.Float64,
            Unit = "ms",
            Dimension = 1,
            DefaultValue = DataValue.FromDouble(0),
            Remember = true,
            History = history
        };

        var varLastRunTime = new Variable() {
            Name = "LastRunTimestamp",
            Type = DataType.Timestamp,
            Dimension = 1,
            DefaultValue = DataValue.Empty,
            Remember = true,
            History = history
        };

        return [
            varLastRunDuration,
            varLastRunTime
        ];
    }

    public bool ShouldSerializeStates() => States.Count > 0;

    public bool ShouldSerializeHistory() => History.HasValue;

    public bool ShouldSerializeDefinition() => !string.IsNullOrEmpty(Definition);

    public bool ShouldSerializeWindowVisible() => WindowVisible;

    public bool ShouldSerializeRealTimeScale() => RealTimeScale != 1.0;

    public bool ShouldSerializeSubtype() => !string.IsNullOrEmpty(Subtype);

    public bool ShouldSerializeInitialStartTime() => InitialStartTime.HasValue;

    public void Normalize(IEnumerable<AdapterInfo> adapters) {
        if (History.HasValue) {
            History = History.Value.Normalize();
        }

        AdapterInfo? adapterFromType = adapters.FirstOrDefault(x => x.Type == Type);
        if (adapterFromType != null) {
            string[] subtypes = adapterFromType.Subtypes;
            if (subtypes.Length == 0) {
                this.Subtype = "";
            }
            else {
                if (this.Subtype == "") {
                    this.Subtype = subtypes[0];
                }
            }
        }
    }
}

public class Input : ModelObject
{
    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    internal const string ID_Separator = ".In.";

    protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
        var calculation = (Calculation)parents.First();
        return calculation.ID + ID_Separator + ID;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    protected override string GetDisplayName(IReadOnlyCollection<IModelObject> parents) {
        var calculation = (Calculation)parents.First();
        return calculation.Name + ".In." + Name;
    }

    [XmlAttribute("type")]
    public DataType Type { get; set; } = DataType.Float64;

    [XmlAttribute("dimension")]
    public int Dimension { get; set; } = 1;

    [XmlAttribute("unit")]
    public string Unit { get; set; } = "";

    public VariableRef? Variable { get; set; }

    public DataValue? Constant { get; set; } // if defined, its value will be used instead of Variable

    public DataValue GetDefaultValue() => Constant ?? DataValue.FromDataType(Type, Dimension);

    protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

        History history = History.None;
        foreach (IModelObject obj in parents) {
            if (obj is Folder folder && folder.History.HasValue) {
                history = folder.History.Value;
                break;
            }
            else if (obj is Calculation calcu && calcu.History.HasValue) {
                history = calcu.History.Value;
                break;
            }
        }

        Calculation calc = (Calculation)parents.First();
        if (calc.HistoryScope.ExcludeInputs()) {
            history = History.None;
        }

        var variable = new Variable() {
            Name = "Value",
            Type = Type,
            Dimension = Dimension,
            DefaultValue = GetDefaultValue(),
            Unit = Unit,
            Remember = true,
            History = history
        };

        return [
            variable
        ];
    }

    public bool ShouldSerializeVariable() => Variable.HasValue;
    public bool ShouldSerializeConstant() => Constant.HasValue;

    public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);

    public bool ShouldSerializeDimension() => Dimension != 1;

    public bool ShouldSerializeType() => Type != DataType.Float64;
}

public class Output : ModelObject
{
    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    internal const string ID_Separator = ".Out.";

    protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
        var calculation = (Calculation)parents.First();
        return calculation.ID + ID_Separator + ID;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    protected override string GetDisplayName(IReadOnlyCollection<IModelObject> parents) {
        var calculation = (Calculation)parents.First();
        return calculation.Name + ".Out." + Name;
    }

    [XmlAttribute("type")]
    public DataType Type { get; set; } = DataType.Float64;

    [XmlAttribute("dimension")]
    public int Dimension { get; set; } = 1;

    [XmlAttribute("unit")]
    public string Unit { get; set; } = "";

    public VariableRef? Variable { get; set; }

    public DataValue GetDefaultValue() => DataValue.FromDataType(Type, Dimension);

    protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

        History history = new History(HistoryMode.None);
        foreach (IModelObject obj in parents) {
            if (obj is Folder folder && folder.History.HasValue) {
                history = folder.History.Value;
                break;
            }
            else if (obj is Calculation calcu && calcu.History.HasValue) {
                history = calcu.History.Value;
                break;
            }
        }

        var variable = new Variable() {
            Name = "Value",
            Type = Type,
            Dimension = Dimension,
            DefaultValue = GetDefaultValue(),
            Unit = Unit,
            Remember = true,
            History = history
        };

        return new Variable[] {
            variable
        };
    }

    public bool ShouldSerializeVariable() => Variable.HasValue;

    public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);

    public bool ShouldSerializeDimension() => Dimension != 1;

    public bool ShouldSerializeType() => Type != DataType.Float64;
}

public class State : ModelObject
{
    [XmlAttribute("id")]
    public string ID { get; set; } = Guid.NewGuid().ToString();

    internal const string ID_Separator = ".State.";

    protected override string GetID(IReadOnlyCollection<IModelObject> parents) {
        var calculation = (Calculation)parents.First();
        return calculation.ID + ID_Separator + ID;
    }

    [XmlAttribute("name")]
    public string Name { get; set; } = "";

    protected override string GetDisplayName(IReadOnlyCollection<IModelObject> parents) {
        var calculation = (Calculation)parents.First();
        return calculation.Name + ".State." + Name;
    }

    [XmlAttribute("type")]
    public DataType Type { get; set; } = DataType.Float64;

    [XmlAttribute("dimension")]
    public int Dimension { get; set; } = 1;

    [XmlAttribute("unit")]
    public string Unit { get; set; } = "";

    [XmlAttribute("default")]
    public string? DefaultValue { get; set; } = null;

    public DataValue GetDefaultValue() => DefaultValue switch {
        null => DataValue.FromDataType(Type, Dimension),
        _    => DataValue.FromJSON(DefaultValue)
    };

    protected override Variable[] GetVariablesOrNull(IReadOnlyCollection<IModelObject> parents) {

        History history = new History(HistoryMode.None);
        foreach (IModelObject obj in parents) {
            if (obj is Folder folder && folder.History.HasValue) {
                history = folder.History.Value;
                break;
            }
            else if (obj is Calculation calcu && calcu.History.HasValue) {
                history = calcu.History.Value;
                break;
            }
        }

        Calculation calc = (Calculation)parents.First();
        if (calc.HistoryScope.ExcludeStates()) {
            history = History.None;
        }

        var variable = new Variable() {
            Name = "Value",
            Type = Type,
            Dimension = Dimension,
            DefaultValue = GetDefaultValue(),
            Unit = Unit,
            Remember = true,
            History = history
        };

        return [
            variable
        ];
    }

    public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);

    public bool ShouldSerializeDimension() => Dimension != 1;

    public bool ShouldSerializeType() => Type != DataType.Float64;

    public bool ShouldSerializeDefaultValue() => DefaultValue != null && DefaultValue != DataValue.FromDataType(Type, Dimension).JSON;
}
