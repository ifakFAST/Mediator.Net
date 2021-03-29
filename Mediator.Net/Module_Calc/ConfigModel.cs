// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;

namespace Ifak.Fast.Mediator.Calc.Config
{
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

        public void Normalize() {
            RootFolder.Normalize();
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

        public void Normalize() {
            if (History.HasValue) {
                History = History.Value.Normalize();
            }
            foreach (var folder in Folders) {
                folder.Normalize();
            }
            foreach (var signal in Signals) {
                signal.Normalize();
            }
            foreach (var calc in Calculations) {
                calc.Normalize();
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

        [XmlAttribute("dimension")]
        public int Dimension { get; set; } = 1;

        public DataValue GetDefaultValue() => DataValue.FromDataType(Type, Dimension);

        // SignalType, e.g. Measurement, KPI, Setpoint ?

        public LocationRef? Location { get; set; } = null; // e.g. Influent/North Side

        public string Comment { get; set; } = "";

        public VariableRef? ValueSource { get; set; } = null;

        public History? History { get; set; } = null;

        public bool ShouldSerializeValueSource() => ValueSource.HasValue;
        public bool ShouldSerializeHistory() => History.HasValue;
        public bool ShouldSerializeComment() => !string.IsNullOrEmpty(Comment);
        public bool ShouldSerializeLocation() => Location.HasValue;
        public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);
        public bool ShouldSerializeDimension() => Dimension != 1;
        public bool ShouldSerializeType() => Type != DataType.Float64;
        protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

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
                    typeConstraints: "",
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

        [XmlAttribute("enabled")]
        public bool Enabled { get; set; } = false;

        public History? History { get; set; } = null;

        public bool WindowVisible { get; set; } = false;

        public Duration Cycle { get; set; } = Duration.FromSeconds(10);

        public Duration Offset { get; set; } = Duration.FromSeconds(0);

        public double RealTimeScale { get; set; } = 1;

        public string Definition { get; set; } = ""; // e.g. C# code, SIMBA project file name

        public List<Input> Inputs { get; set; } = new List<Input>();

        public List<Output> Outputs { get; set; } = new List<Output>();

        public List<State> States { get; set; } = new List<State>();

        public bool ShouldSerializeOffset() => Offset.TotalMilliseconds != 0;

        public Calc.Calculation ToCalculation() {
            return new Calc.Calculation() {
                ID = ID,
                Name = Name,
                Cycle = Cycle,
                Offset = Offset,
                Definition = Definition,
                RealTimeScale = RealTimeScale,
                WindowVisible = WindowVisible,
            };
        }

        //protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

        //    History history = new History(HistoryMode.None);

        //    if (History.HasValue) {
        //        history = History.Value;
        //    }
        //    else {
        //        foreach (IModelObject obj in parents) {
        //            if (obj is Folder && (obj as Folder).History.HasValue) {
        //                history = (obj as Folder).History.Value;
        //                break;
        //            }
        //        }
        //    }

        //    var varLastRunDuration = new Variable() {
        //        Name = "Duration",
        //        Type = DataType.Float64,
        //        Dimension = 1,
        //        DefaultValue = DataValue.FromDouble(0),
        //        Remember = true,
        //        History = history
        //    };

        //    return new Variable[] {
        //        varLastRunDuration,
        //    };
        //}

        public bool ShouldSerializeStates() => States.Count > 0;

        public bool ShouldSerializeHistory() => History.HasValue;

        public bool ShouldSerializeDefinition() => !string.IsNullOrEmpty(Definition);

        public bool ShouldSerializeWindowVisible() => WindowVisible;

        public bool ShouldSerializeRealTimeScale() => RealTimeScale != 1.0;

        public void Normalize() {
            if (History.HasValue) {
                History = History.Value.Normalize();
            }
        }
    }

    public class Input : ModelObject
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        internal const string ID_Separator = ".In.";

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var calculation = (Calculation)parents.First();
            return calculation.ID + ID_Separator + ID;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        protected override string GetDisplayName(IEnumerable<IModelObject> parents) {
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

        public DataValue GetDefaultValue() => Constant.HasValue ? Constant.Value : DataValue.FromDataType(Type, Dimension);

        protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

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
                Remember = true,
                History = history
            };

            return new Variable[] {
                variable
            };
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

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var calculation = (Calculation)parents.First();
            return calculation.ID + ID_Separator + ID;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        protected override string GetDisplayName(IEnumerable<IModelObject> parents) {
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

        protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

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

        protected override string GetID(IEnumerable<IModelObject> parents) {
            var calculation = (Calculation)parents.First();
            return calculation.ID + ID_Separator + ID;
        }

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        protected override string GetDisplayName(IEnumerable<IModelObject> parents) {
            var calculation = (Calculation)parents.First();
            return calculation.Name + ".State." + Name;
        }

        [XmlAttribute("type")]
        public DataType Type { get; set; } = DataType.Float64;

        [XmlAttribute("dimension")]
        public int Dimension { get; set; } = 1;

        [XmlAttribute("unit")]
        public string Unit { get; set; } = "";

        public DataValue GetDefaultValue() => DataValue.FromDataType(Type, Dimension);

        protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

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
                Remember = true,
                History = history
            };

            return new Variable[] {
                variable
            };
        }

        public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);

        public bool ShouldSerializeDimension() => Dimension != 1;

        public bool ShouldSerializeType() => Type != DataType.Float64;
    }
}
