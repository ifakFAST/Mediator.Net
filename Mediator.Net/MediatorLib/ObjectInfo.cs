// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator
{
    public sealed class ObjectInfo : IEquatable<ObjectInfo>
    {
        public ObjectInfo() { }

        public ObjectInfo(ObjectRef id, string name, string classNameFull, string classNameShort, MemberRefIdx? parent = null, Variable[]? variables = null, LocationRef? location = null) {
            ID = id;
            Name = name ?? throw new ArgumentNullException(nameof(name), nameof(name) + " may not be null");
            ClassNameFull = classNameFull ?? throw new ArgumentNullException(nameof(classNameFull), nameof(classNameFull) + " may not be null");
            ClassNameShort = classNameShort ?? throw new ArgumentNullException(nameof(classNameShort), nameof(classNameShort) + " may not be null");
            Variables = variables ?? [];
            Parent = parent;
            Location = location;
        }

        public ObjectRef ID { get; set; }

        public string Name { get; set; } = "";

        public string ClassNameFull { get; set; } = "";

        public string ClassNameShort { get; set; } = "";

        public LocationRef? Location { get; set; } = null;

        public MemberRefIdx? Parent { get; set; } = null;

        public Variable[] Variables { get; set; } = [];

        public override string ToString() => Name + " " + ID.ToString() + " (" + ClassNameFull + ")";

        public bool Equals(ObjectInfo? other) {
            if (other is null) return false;
            if (ReferenceEquals(other, this)) return true;
            return
                ID == other.ID &&
                Name == other.Name &&
                ClassNameFull == other.ClassNameFull &&
                ClassNameShort == other.ClassNameShort &&
                Equals(Parent, other.Parent) &&
                Equals(Location, other.Location) &&
                Arrays.Equals(Variables, other.Variables);
        }

        public override bool Equals(object obj) => Equals(obj as ObjectInfo);

        public override int GetHashCode() => ID.GetHashCode();

        public static bool operator==(ObjectInfo? lhs, ObjectInfo? rhs) {
            if (lhs is null) return rhs is null;
            return lhs.Equals(rhs);
        }

        public static bool operator!=(ObjectInfo? lhs, ObjectInfo? rhs) => !(lhs == rhs);
    }

    public sealed class Variable : IEquatable<Variable>
    {
        public Variable() { }

        public Variable(string name, DataType type, int dimension = 1, bool remember = true, bool writable = false, bool syncReadable = false) :
            this(name, type, DataValue.FromDataType(type, dimension), History.None, dimension, remember, writable, syncReadable) {
        }

        public Variable(string name, DataType type, DataValue defaultValue, History history, int dimension = 1, bool remember = true, bool writable = false, bool syncReadable = false) :
            this(name, type, defaultValue, "", "", dimension, [], remember, history, writable, syncReadable) {
        }

        public Variable(string name, DataType type, DataValue defaultValue, string unit, string typeConstraints, int dimension, string[] dimensionNames, bool remember, History history, bool writable = false, bool syncReadable = false) {
            if (name == null) throw new ArgumentNullException(nameof(name), nameof(name) + " may not be null");
            if (dimension < 0) throw new ArgumentException(nameof(dimension) + " may not be negative", nameof(dimension));

            if (dimensionNames != null && dimensionNames.Length > 0) {
                if (dimensionNames.Length != dimension) throw new ArgumentException(nameof(dimensionNames) + ".Length must equal " + nameof(dimension), nameof(dimensionNames));
            }

            Name = name;
            Type = type;
            DefaultValue = defaultValue;
            TypeConstraints = typeConstraints ?? "";
            Unit = unit;
            Dimension = dimension;
            DimensionNames = dimensionNames ?? [];
            Remember = remember;
            History = history;
            Writable = writable;
            SyncReadable = syncReadable;
        }

        public string Name { get; set; } = "";

        public DataType Type { get; set; } = DataType.Float64;

        public bool Writable { get; set; } = false;

        /// <summary>
        /// If True, then it is possible to use Connection.ReadVariablesSync to get a 'fresh' value for this variable.
        /// </summary>
        public bool SyncReadable { get; set; } = false;

        public string Unit { get; set; } = "";

        public DataValue DefaultValue { get; set; }

        /// <summary>
        /// Can be used to more precisely specify the Type, e.g. by providing the name of the struct or enumeration
        /// </summary>
        public string TypeConstraints { get; set; } = "";

        /// <summary>
        /// 0 := var array; 1 := scalar; N := array with exactly N entries
        /// </summary>
        public int Dimension { get; set; } = 1;

        /// <summary>
        /// Can be used to provide descriptive names to the individual dimensions, when <see cref="Dimension"/> > 1
        /// </summary>
        public string[] DimensionNames { get; set; } = [];

        /// <summary>
        /// If true, then the last value of the variable will be restored on next <see cref="ModuleBase.Init(ModuleInitInfo, VariableValue[], Notifier, ModuleThread)"/>
        /// </summary>
        public bool Remember { get; set; } = true;

        public History History { get; set; }

        public bool IsNumeric => Type.IsNumeric();

        public override string ToString() => Name ?? "";

        public bool Equals(Variable? other) {
            if (other is null) return false;
            if (ReferenceEquals(other, this)) return true;
            return
                Name == other.Name &&
                Type == other.Type &&
                Writable == other.Writable &&
                SyncReadable == other.SyncReadable &&
                DefaultValue == other.DefaultValue &&
                TypeConstraints == other.TypeConstraints &&
                Dimension == other.Dimension &&
                Arrays.Equals(DimensionNames, other.DimensionNames) &&
                Remember == other.Remember &&
                History.Equals(other.History);
        }

        public override bool Equals(object obj) => Equals(obj as Variable);

        public override int GetHashCode() => (Name ?? "").GetHashCode();

        public static bool operator ==(Variable? lhs, Variable? rhs) {
            if (lhs is null) return rhs is null;
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Variable? lhs, Variable? rhs) => !(lhs == rhs);
    }

    public struct History(HistoryMode mode, Duration? interval = null, Duration? offset = null) : IEquatable<History>, IXmlSerializable
    {
        public static History None => new();
        public static History Complete => new(HistoryMode.Complete);
        public static History ValueOrQualityChanged => new(HistoryMode.ValueOrQualityChanged);
        public static History IntervalDefault(Duration interval, Duration? offset = null) => new(HistoryMode.Interval, interval, offset);
        public static History IntervalExact(Duration interval, Duration? offset = null) => new(HistoryMode.IntervalExact, interval, offset);
        public static History IntervalOrChanged(Duration interval, Duration? offset = null) => new(HistoryMode.IntervalOrChanged, interval, offset);
        public static History IntervalExactOrChanged(Duration interval, Duration? offset = null) => new(HistoryMode.IntervalExactOrChanged, interval, offset);

        public HistoryMode Mode { get; set; } = mode;
        public Duration? Interval { get; set; } = interval;
        public Duration? Offset { get; set; } = offset;

        public readonly bool Equals(History other) => Mode == other.Mode && Interval == other.Interval && Offset == other.Offset;

        public readonly override bool Equals(object obj) {
            if (obj is History h) {
                return Equals(h);
            }
            return false;
        }

        public static bool operator ==(History lhs, History rhs) => lhs.Equals(rhs);

        public static bool operator !=(History lhs, History rhs) => !(lhs.Equals(rhs));

        public readonly override string ToString() => Mode.ToString();

        public readonly override int GetHashCode() => Mode.GetHashCode();

        public readonly XmlSchema? GetSchema() => null;

        public readonly bool ShouldSerializeInterval() => Interval.HasValue;

        public readonly bool ShouldSerializeOffset() => Offset.HasValue;

        public void ReadXml(XmlReader reader) {
            string m = reader["mode"];
            Mode = m switch {
                "None" => HistoryMode.None,
                "Complete" => HistoryMode.Complete,
                "ValueOrQualityChanged" => HistoryMode.ValueOrQualityChanged,
                "Interval" => HistoryMode.Interval,
                "IntervalExact" => HistoryMode.IntervalExact,
                "IntervalOrChanged" => HistoryMode.IntervalOrChanged,
                "IntervalExactOrChanged" => HistoryMode.IntervalExactOrChanged,
                _ => throw new Exception("Unknown HistoryMode: " + m),
            };
            string intv = reader["interval"];
            if (intv != null) {
                Interval = Duration.Parse(intv);
            }
            string off = reader["offset"];
            if (off != null) {
                Offset = Duration.Parse(off);
            }
            reader.Read();
        }

        public readonly void WriteXml(XmlWriter writer) {
            writer.WriteAttributeString("mode", Mode.ToString());
            bool modeInterval = Mode == HistoryMode.Interval || Mode == HistoryMode.IntervalExact || Mode == HistoryMode.IntervalOrChanged || Mode == HistoryMode.IntervalExactOrChanged;
            if (Interval.HasValue && modeInterval) {
                writer.WriteAttributeString("interval", Interval.Value.ToString());
            }
            if (Offset.HasValue && modeInterval) {
                writer.WriteAttributeString("offset", Offset.Value.ToString());
            }
        }

        public readonly void ValidateOrThrow() {
            bool modeInterval = Mode == HistoryMode.Interval || Mode == HistoryMode.IntervalExact || Mode == HistoryMode.IntervalOrChanged || Mode == HistoryMode.IntervalExactOrChanged;
            if (modeInterval) {
                if (!Interval.HasValue) throw new Exception($"Missing interval value for history mode {Mode}");
                if (Interval.Value.TotalMilliseconds == 0) throw new Exception($"Interval value must be non zero for history mode {Mode}");
            }
        }

        public readonly History Normalize() {
            switch (Mode) {
                case HistoryMode.None:
                case HistoryMode.Complete:
                case HistoryMode.ValueOrQualityChanged:
                    return new History(Mode);
                default:
                    return this;
            }
        }
    }

    public enum HistoryMode
    {
        None,
        Complete,
        ValueOrQualityChanged,
        Interval, // save if t_last < t_now and t_now is on or over latest interval bound
        IntervalExact,
        IntervalOrChanged,
        IntervalExactOrChanged,
    }

    public struct ObjectValue(ObjectRef obj, DataValue value) : IEquatable<ObjectValue>
    {
        public static ObjectValue Make(ObjectRef obj, DataValue value) {
            return new ObjectValue(obj, value);
        }

        public static ObjectValue Make(string moduleID, string localObjectID, DataValue value) {
            return new ObjectValue(ObjectRef.Make(moduleID, localObjectID), value);
        }

        public ObjectRef Object { get; set; } = obj;
        public DataValue Value { get; set; } = value;

        public readonly T? ToObject<T>() => Value.Object<T>();

        public readonly override string ToString() => Object.ToString() + "=" + Value.ToString();

        public readonly bool Equals(ObjectValue other) => Object == other.Object && Value == other.Value;

        public readonly override bool Equals(object obj) {
            if (obj is ObjectValue ov) {
                return Equals(ov);
            }
            return false;
        }

        public static bool operator ==(ObjectValue lhs, ObjectValue rhs) => lhs.Equals(rhs);

        public static bool operator !=(ObjectValue lhs, ObjectValue rhs) => !(lhs.Equals(rhs));

        public readonly override int GetHashCode() => Object.GetHashCode();
    }

    public struct VariableRef(ObjectRef obj, string variableName) : IEquatable<VariableRef>, IXmlSerializable
    {
        public static VariableRef Make(string moduleID, string localObjectID, string variableName) {
            return new VariableRef(ObjectRef.Make(moduleID, localObjectID), variableName);
        }

        public static VariableRef Make(ObjectRef obj, string variableName) {
            return new VariableRef(obj, variableName);
        }

        public ObjectRef Object { get; set; } = obj;
        public string Name { get; set; } = variableName ?? throw new ArgumentNullException(nameof(variableName), nameof(variableName) + " may not be null");

        public readonly bool Equals(VariableRef other) => Object == other.Object && Name == other.Name;

        public readonly override bool Equals(object obj) {
            if (obj is VariableRef vr) {
                return Equals(vr);
            }
            return false;
        }

        public static bool operator ==(VariableRef lhs, VariableRef rhs) => lhs.Equals(rhs);

        public static bool operator !=(VariableRef lhs, VariableRef rhs) => !(lhs.Equals(rhs));

        public readonly override string ToString() => Name == null ? "" : Object.ToString() + "." + Name;

        public readonly override int GetHashCode() => Object.GetHashCode() * (Name ?? "").GetHashCode();

        public readonly XmlSchema? GetSchema() => null;

        public void ReadXml(XmlReader reader) {
            Object = ObjectRef.FromEncodedString(reader["object"]);
            Name = reader["name"];
            reader.Read();
        }

        public readonly void WriteXml(XmlWriter writer) {
            writer.WriteAttributeString("object", Object.ToEncodedString());
            writer.WriteAttributeString("name", Name ?? "");
        }
    }

    public struct VariableValue(VariableRef variable, VTQ value) : IEquatable<VariableValue>
    {
        public static VariableValue Make(VariableRef varRef, VTQ value) {
            return new VariableValue(varRef, value);
        }

        public static VariableValue Make(ObjectRef obj, string variable, VTQ value) {
            return new VariableValue(VariableRef.Make(obj, variable), value);
        }

        public static VariableValue Make(string moduleID, string localObjectID, string variable, VTQ value) {
            return new VariableValue(VariableRef.Make(ObjectRef.Make(moduleID, localObjectID), variable), value);
        }

        public VariableRef Variable { get; set; } = variable;
        public VTQ Value { get; set; } = value;

        public readonly override string ToString() => Variable.ToString() + "=" + Value.ToString();

        public readonly bool Equals(VariableValue other) => Variable == other.Variable && Value == other.Value;

        public readonly override bool Equals(object obj) {
            if (obj is VariableValue vv) {
                return Equals(vv);
            }
            return false;
        }

        public static bool operator ==(VariableValue lhs, VariableValue rhs) => lhs.Equals(rhs);

        public static bool operator !=(VariableValue lhs, VariableValue rhs) => !(lhs.Equals(rhs));

        public readonly override int GetHashCode() => Variable.GetHashCode();
    }

    public struct MemberRef(ObjectRef obj, string memberName) : IEquatable<MemberRef>
    {
        public static MemberRef Make(string moduleID, string localObjectID, string memberName) {
            return new MemberRef(ObjectRef.Make(moduleID, localObjectID), memberName);
        }

        public static MemberRef Make(ObjectRef obj, string memberName) {
            return new MemberRef(obj, memberName);
        }

        public ObjectRef Object { get; set; } = obj;
        public string Name { get; set; } = memberName ?? throw new ArgumentNullException(nameof(memberName), nameof(memberName) + " may not be null");

        public readonly override string ToString() => Object.ToString() + "." + Name;

        public readonly bool Equals(MemberRef other) => Object == other.Object && Name == other.Name;

        public readonly override bool Equals(object obj) {
            if (obj is MemberRef mref) {
                return Equals(mref);
            }
            return false;
        }

        public static bool operator ==(MemberRef lhs, MemberRef rhs) => lhs.Equals(rhs);

        public static bool operator !=(MemberRef lhs, MemberRef rhs) => !(lhs.Equals(rhs));

        public readonly override int GetHashCode() => Object.GetHashCode() * (Name ?? "").GetHashCode();
    }

    public struct MemberRefIdx(ObjectRef obj, string memberName, int index) : IEquatable<MemberRefIdx>
    {
        public static MemberRefIdx Make(string moduleID, string localObjectID, string memberName, int index) {
            return new MemberRefIdx(ObjectRef.Make(moduleID, localObjectID), memberName, index);
        }

        public static MemberRefIdx Make(ObjectRef obj, string memberName, int index) {
            return new MemberRefIdx(obj, memberName, index);
        }

        public ObjectRef Object { get; set; } = obj;
        public string Name { get; set; } = memberName ?? throw new ArgumentNullException(nameof(memberName), nameof(memberName) + " may not be null");
        public int Index { get; set; } = index;

        public readonly MemberRef ToMemberRef() => MemberRef.Make(Object, Name);

        public readonly override string ToString() => Object.ToString() + "." + Name + "[" + Index + "]";

        public readonly bool Equals(MemberRefIdx other) => Object == other.Object && Name == other.Name && Index == other.Index;

        public readonly override bool Equals(object obj) {
            if (obj is MemberRefIdx mri) {
                return Equals(mri);
            }
            return false;
        }

        public static bool operator ==(MemberRefIdx lhs, MemberRefIdx rhs) => lhs.Equals(rhs);

        public static bool operator !=(MemberRefIdx lhs, MemberRefIdx rhs) => !(lhs.Equals(rhs));

        public readonly override int GetHashCode() => Object.GetHashCode() * (Name ?? "").GetHashCode() + Index;
    }

    public struct MemberValue(MemberRef member, DataValue value) : IEquatable<MemberValue>
    {
        public static MemberValue Make(MemberRef member, DataValue value) {
            return new MemberValue(member, value);
        }

        public static MemberValue Make(ObjectRef obj, string memberName, DataValue value) {
            return new MemberValue(MemberRef.Make(obj, memberName), value);
        }

        public static MemberValue Make(string moduleID, string localObjectID, string memberName, DataValue value) {
            return new MemberValue(MemberRef.Make(ObjectRef.Make(moduleID, localObjectID), memberName), value);
        }

        public MemberRef Member { get; set; } = member;
        public DataValue Value { get; set; } = value;

        public readonly override string ToString() => Member.ToString() + "=" + Value.ToString();

        public readonly bool Equals(MemberValue other) => Member == other.Member && Value == other.Value;

        public readonly override bool Equals(object obj) {
            if (obj is MemberValue mv) {
                return Equals(mv);
            }
            return false;
        }

        public static bool operator ==(MemberValue lhs, MemberValue rhs) => lhs.Equals(rhs);

        public static bool operator !=(MemberValue lhs, MemberValue rhs) => !(lhs.Equals(rhs));

        public readonly override int GetHashCode() => Member.GetHashCode();
    }

    public struct AddArrayElement : IEquatable<AddArrayElement>
    {
        public static AddArrayElement Make(ObjectRef obj, string arrayMemberName, DataValue valueToAdd) {
            return new AddArrayElement(MemberRef.Make(obj, arrayMemberName), valueToAdd);
        }

        public static AddArrayElement Make(string moduleID, string localObjectID, string arrayMemberName, DataValue valueToAdd) {
            return new AddArrayElement(MemberRef.Make(ObjectRef.Make(moduleID, localObjectID), arrayMemberName), valueToAdd);
        }

        public static AddArrayElement Make(MemberRef arrayMember, DataValue valueToAdd) {
            return new AddArrayElement(arrayMember, valueToAdd);
        }

        public AddArrayElement(MemberRef arrayMember, DataValue valueToAdd) {
            if (valueToAdd.IsEmpty) throw new ArgumentException(nameof(AddArrayElement) + ": " + nameof(valueToAdd) + " must not be empty", nameof(valueToAdd));
            ArrayMember = arrayMember;
            ValueToAdd = valueToAdd;
        }

        public MemberRef ArrayMember { get; set; }
        public DataValue ValueToAdd { get; set; }

        public readonly override string ToString() => ArrayMember.ToString() + "+=" + ValueToAdd.ToString();

        public readonly bool Equals(AddArrayElement other) => ArrayMember == other.ArrayMember && ValueToAdd == other.ValueToAdd;

        public readonly override bool Equals(object obj) {
            if (obj is AddArrayElement aae) {
                return Equals(aae);
            }
            return false;
        }

        public static bool operator ==(AddArrayElement lhs, AddArrayElement rhs) => lhs.Equals(rhs);

        public static bool operator !=(AddArrayElement lhs, AddArrayElement rhs) => !(lhs.Equals(rhs));

        public readonly override int GetHashCode() => ArrayMember.GetHashCode();
    }
}
