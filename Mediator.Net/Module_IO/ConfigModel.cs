// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using System.Xml;
using System.Xml.Schema;

namespace Ifak.Fast.Mediator.IO.Config
{
    [XmlRoot(Namespace = "Module_IO", ElementName = "IO_Model")]
    public class IO_Model : ModelObject {
        [XmlIgnore]
        public string ID { get; set; } = "Root";

        [XmlIgnore]
        public string Name { get; set; } = "IO_Model";

        public Scheduling Scheduling { get; set; } = new Scheduling(SchedulingMode.Interval, Duration.FromSeconds(5));
        public History History { get; set; } = History.IntervalDefault(Duration.FromMinutes(1));

        public List<Folder> Folders { get; set; } = new List<Folder>();
        public List<Adapter> Adapters { get; set; } = new List<Adapter>();

        public bool ShouldSerializeFolders() => Folders.Count > 0;

        public List<Adapter> GetAllAdapters() {
            var res = new List<Adapter>();
            foreach (Folder f in Folders)
                res.AddRange(f.GetAllAdapters());
            res.AddRange(Adapters);
            return res;
        }

        public void ValidateOrThrow() {
            Scheduling.ValidateOrThrow();
            History.ValidateOrThrow();
            foreach (var adapter in GetAllAdapters()) {
                adapter.ValidateOrThrow();
            }
        }

        public void Normalize() {
            Scheduling = Scheduling.Normalize();
            History = History.Normalize();
            foreach (var adapter in GetAllAdapters()) {
                adapter.Normalize();
            }
        }
    }

    [IdPrefix("Folder")]
    public class Folder : ModelObject {

        public Folder() {}

        public Folder(string id, string name) {
            ID = id;
            Name = name;
        }

        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        public List<Folder> Folders { get; set; } = new List<Folder>();
        public List<Adapter> Adapters { get; set; } = new List<Adapter>();

        public bool ShouldSerializeFolders() => Folders.Count > 0;

        public List<Adapter> GetAllAdapters() {
            var res = new List<Adapter>();
            foreach (Folder f in Folders)
                res.AddRange(f.GetAllAdapters());
            res.AddRange(Adapters);
            return res;
        }
    }

    [IdPrefix("Adapter")]
    public class Adapter : ModelObject
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        [Browseable]
        [XmlAttribute("type")]
        public string Type { get; set; } = "";

        [Browseable]
        [XmlAttribute("address")]
        public string Address { get; set; } = "";

        [XmlAttribute("enabled")]
        public bool Enabled { get; set; } = true;

        public int? MaxFractionalDigits { get; set; } = 3;

        public Scheduling? Scheduling { get; set; } = null;
        public History? History { get; set; } = null;
        public Login? Login { get; set; } = null;
        public List<NamedValue> Config { get; set; } = new List<NamedValue>();
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<DataItem> DataItems { get; set; } = new List<DataItem>();

        public bool ShouldSerializeScheduling() { return Scheduling.HasValue; }
        public bool ShouldSerializeHistory() { return History.HasValue; }
        public bool ShouldSerializeEnabled() => !Enabled;
        public bool ShouldSerializeLogin() => Login.HasValue;
        public bool ShouldSerializeNodes() => Nodes.Count > 0;
        public bool ShouldSerializeFractionalDigits() => MaxFractionalDigits.HasValue;
        public bool ShouldSerializeConfig() => Config.Count > 0;
        public bool ShouldSerializeDataItems() => DataItems.Count > 0;

        public List<DataItem> GetAllDataItems() {
            var res = new List<DataItem>();
            foreach (Node n in Nodes)
                res.AddRange(n.GetAllDataItems());
            res.AddRange(DataItems);
            return res;
        }

        public List<Tuple<DataItem, Scheduling>> GetAllDataItemsWithScheduling(Scheduling defaultScheduling) {
            Scheduling s = Scheduling ?? defaultScheduling;
            var res = new List<Tuple<DataItem, Scheduling>>();
            foreach (Node n in Nodes)
                res.AddRange(n.GetAllDataItemsWithScheduling(s));
            res.AddRange(DataItems.Where(di => di.Read).Select(di => Tuple.Create(di, di.Scheduling ?? s)));
            return res;
        }

        public void ValidateOrThrow() {
            if (Scheduling.HasValue) Scheduling.Value.ValidateOrThrow();
            if (History.HasValue) History.Value.ValidateOrThrow();
            foreach (Node node in Nodes) {
                node.ValidateOrThrow();
            }
            foreach (DataItem di in DataItems) {
                di.ValidateOrThrow();
            }
            if (MaxFractionalDigits.HasValue) {
                int digits = MaxFractionalDigits.Value;
                if (digits < 0) {
                    throw new Exception("MaxFractionalDigits may not be smaller than 0");
                }
                if (digits > 15) {
                    throw new Exception("MaxFractionalDigits may not be gretare than 15");
                }
            }
        }

        public void Normalize() {
            if (Scheduling.HasValue) {
                Scheduling = Scheduling.Value.Normalize();
            }
            if (History.HasValue) {
                History = History.Value.Normalize();
            }
            foreach (Node node in Nodes) {
                node.Normalize();
            }
            foreach (DataItem di in DataItems) {
                di.Normalize();
            }
        }

        public IO.Adapter ToAdapter() {
            return new IO.Adapter() {
                ID = ID,
                Name = Name,
                Type = Type,
                Address = Address,
                Login = Login,
                Config = Config,
                Nodes = Nodes.Select(n => n.ToNode()).ToList(),
                DataItems = DataItems.Select(di => di.ToDataItem()).ToList()
            };
        }
    }

    [IdPrefix("Node")]
    public class Node : ModelObject
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        public Scheduling? Scheduling { get; set; } = null;
        public History? History { get; set; } = null;
        public List<NamedValue> Config { get; set; } = new List<NamedValue>();
        public List<Node> Nodes { get; set; } = new List<Node>();
        public List<DataItem> DataItems { get; set; } = new List<DataItem>();

        public List<DataItem> GetAllDataItems() {
            var res = new List<DataItem>();
            foreach (Node n in Nodes)
                res.AddRange(n.GetAllDataItems());
            res.AddRange(DataItems);
            return res;
        }

        public List<Tuple<DataItem, Scheduling>> GetAllDataItemsWithScheduling(Scheduling defaultScheduling) {
            Scheduling s = Scheduling ?? defaultScheduling;
            var res = new List<Tuple<DataItem, Scheduling>>();
            foreach (Node n in Nodes)
                res.AddRange(n.GetAllDataItemsWithScheduling(s));
            res.AddRange(DataItems.Where(di => di.Read).Select(di => Tuple.Create(di, di.Scheduling ?? s)));
            return res;
        }

        public void ValidateOrThrow() {
            if (Scheduling.HasValue) Scheduling.Value.ValidateOrThrow();
            if (History.HasValue) History.Value.ValidateOrThrow();
            foreach (Node node in Nodes) {
                node.ValidateOrThrow();
            }
            foreach (DataItem di in DataItems) {
                di.ValidateOrThrow();
            }
        }

        public void Normalize() {
            if (Scheduling.HasValue) {
                Scheduling = Scheduling.Value.Normalize();
            }
            if (History.HasValue) {
                History = History.Value.Normalize();
            }
            foreach (Node node in Nodes) {
                node.Normalize();
            }
            foreach (DataItem di in DataItems) {
                di.Normalize();
            }
        }

        public bool ShouldSerializeScheduling() => Scheduling.HasValue;
        public bool ShouldSerializeHistory() => History.HasValue;
        public bool ShouldSerializeConfig() => Config.Count > 0;
        public bool ShouldSerializeDataItems() => DataItems.Count > 0;
        public bool ShouldSerializeNodes() => Nodes.Count > 0;

        public IO.Node ToNode() {
            return new IO.Node() {
                ID = ID,
                Name = Name,
                Config = Config,
                Nodes = Nodes.Select(n => n.ToNode()).ToList(),
                DataItems = DataItems.Select(d => d.ToDataItem()).ToList()
            };
        }
    }

    [DefaultCategory("General")]
    [IdPrefix("Data")]
    public class DataItem : ModelObject
    {
        [XmlAttribute("id")]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        [XmlAttribute("name")]
        public string Name { get; set; } = "";

        [XmlAttribute("unit")]
        public string Unit { get; set; } = "";

        [XmlAttribute("type")]
        public DataType Type { get; set; } = DataType.Float64;

        [Category("Advanced")]
        [XmlAttribute("typeConstraints")]
        public string TypeConstraints { get; set; } = "";

        [XmlAttribute("dimension")]
        public int Dimension { get; set; } = 1;

        [Category("Advanced")]
        public string[] DimensionNames { get; set; } = new string[0];

        [XmlAttribute("read")]
        public bool Read { get; set; } = false;

        [XmlAttribute("write")]
        public bool Write { get; set; } = false;

        [Browseable]
        [XmlAttribute("address")]
        public string Address { get; set; } = "";

        public Scheduling? Scheduling { get; set; } = null;
        public History? History { get; set; } = null;
        //public DataValue? InitialValue { get; set; } = null;
        //public DataValue? ReplacementValue { get; set; } = null;

        [Category("Advanced")]
        public List<NamedValue> Config { get; set; } = new List<NamedValue>();

        protected override Variable[] GetVariablesOrNull(IEnumerable<IModelObject> parents) {

            History history;

            if (History.HasValue) {
                history = History.Value;
            }
            else {
                history = new History(HistoryMode.None);
                foreach (IModelObject obj in parents) {
                    if (obj is IO_Model) {
                        history = (obj as IO_Model).History;
                        break;
                    }
                    else if (obj is Adapter && (obj as Adapter).History.HasValue) {
                        history = (obj as Adapter).History.Value;
                        break;
                    }
                    else if (obj is Node && (obj as Node).History.HasValue) {
                        history = (obj as Node).History.Value;
                        break;
                    }
                }
            }

            var variable = new Variable(
                    name: "Value",
                    type: this.Type,
                    defaultValue: GetDefaultValue(),
                    typeConstraints: this.TypeConstraints,
                    dimension: this.Dimension,
                    dimensionNames: this.DimensionNames,
                    remember: true,
                    history: history);

            return new Variable[] { variable };
        }

        public DataValue GetDefaultValue() => /*InitialValue.HasValue ? this.InitialValue.Value :*/ DataValue.FromDataType(Type, Dimension);

        public bool ShouldSerializeTypeConstraints() => !string.IsNullOrEmpty(TypeConstraints);
        public bool ShouldSerializeDimensionNames() => DimensionNames != null && DimensionNames.Length > 0;
        public bool ShouldSerializeScheduling() => Scheduling.HasValue;
        public bool ShouldSerializeHistory() => History.HasValue;
        //public bool ShouldSerializeInitialValue() => InitialValue.HasValue;
        //public bool ShouldSerializeReplacementValue() => ReplacementValue.HasValue;
        public bool ShouldSerializeConfig() => Config.Count > 0;
        public bool ShouldSerializeRead() => Read;
        public bool ShouldSerializeWrite() => Write;
        public bool ShouldSerializeUnit() => !string.IsNullOrEmpty(Unit);
        public bool ShouldSerializeAddress() => !string.IsNullOrEmpty(Address);
        public bool ShouldSerializeDimension() => Dimension != 1;
        public bool ShouldSerializeType() => Type != DataType.Float64;

        public void ValidateOrThrow() {
            if (Scheduling.HasValue) Scheduling.Value.ValidateOrThrow();
            if (History.HasValue) History.Value.ValidateOrThrow();
        }

        public void Normalize() {
            if (Scheduling.HasValue) {
                Scheduling = Scheduling.Value.Normalize();
            }
            if (History.HasValue) {
                History = History.Value.Normalize();
            }
        }

        public IO.DataItem ToDataItem() {
            return new IO.DataItem() {
                ID = ID,
                Name = Name,
                Config = Config,
                Unit = Unit,
                Type = Type,
                TypeConstraints = TypeConstraints,
                Dimension = Dimension,
                DimensionNames = DimensionNames,
                Read = Read,
                Write = Write,
                Address = Address
            };
        }
    }

    public struct Scheduling : IXmlSerializable
    {
        public Scheduling(SchedulingMode mode, Duration? interval = null, Duration? offset = null, bool useTimestampFromSource = false) {
            Mode = mode;
            Interval = interval;
            Offset = offset;
            UseTimestampFromSource = useTimestampFromSource;
        }
        public SchedulingMode Mode { get; set; }
        public Duration? Interval { get; set; } // Must be at most 24 hours, and must divide 24 hours in whole numbers, e.g. 6h, 8h, 12h but not 7h
        public Duration? Offset { get; set; }
        public bool UseTimestampFromSource { get; set; }

        public XmlSchema GetSchema() => null;

        public void ReadXml(XmlReader reader) {
            string m = reader["mode"];
            switch (m) {
                case "None": Mode = SchedulingMode.None; break;
                case "Interval": Mode = SchedulingMode.Interval; break;
            }
            string intv = reader["interval"];
            if (intv != null) {
                Interval = Duration.Parse(intv);
            }
            string off = reader["offset"];
            if (off != null) {
                Offset = Duration.Parse(off);
            }
            string useTSource = reader["useTimestampFromSource"];
            if (useTSource != null) {
                UseTimestampFromSource = bool.Parse(useTSource);
            }
            reader.Read();

            if (Mode == SchedulingMode.None) {
                Interval = null;
                Offset = null;
            }
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteAttributeString("mode", Mode.ToString());
            if (Interval.HasValue && Mode == SchedulingMode.Interval) {
                writer.WriteAttributeString("interval", Interval.Value.ToString());
            }
            if (Offset.HasValue && Mode == SchedulingMode.Interval) {
                writer.WriteAttributeString("offset", Offset.Value.ToString());
            }
            if (UseTimestampFromSource) {
                writer.WriteAttributeString("useTimestampFromSource", UseTimestampFromSource.ToString());
            }
        }

        public void ValidateOrThrow() {
            if (Mode == SchedulingMode.Interval) {
                if (!Interval.HasValue) throw new Exception("Missing interval value for scheduling mode Interval");
                if (Interval.Value.TotalMilliseconds == 0) throw new Exception("Interval value must be non zero for scheduling mode Interval");

                if (Interval.Value > Duration.FromHours(24))
                    throw new Exception("Interval may not be larger than 24 hours");

                if (Duration.FromHours(24).TotalMilliseconds % Interval.Value.TotalMilliseconds != 0)
                    throw new Exception("Interval must divide 24 hours without remainder");
            }
        }

        public Scheduling Normalize() {
            switch (Mode) {
                case SchedulingMode.None:
                    return new Scheduling(Mode, useTimestampFromSource: UseTimestampFromSource);
                case SchedulingMode.Interval:
                    return this;
                default: throw new Exception("Unknown value for Scheduling.Mode: " + Mode);
            }
        }
    }

    public enum SchedulingMode
    {
        None,
        Interval
    }
}
