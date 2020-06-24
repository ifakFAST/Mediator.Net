// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator
{
    [Ifak.Fast.Json.JsonConverter(typeof(DataValueConverter))]
    public struct DataValue : IEquatable<DataValue>, IXmlSerializable
    {
        private string json;

        private DataValue(string s) {
            json = s;
        }

        public bool Equals(DataValue other) => json == other.json;

        public override bool Equals(object obj) {
            if (obj is DataValue) {
                return Equals((DataValue)obj);
            }
            return false;
        }

        public static bool operator ==(DataValue lhs, DataValue rhs) => lhs.Equals(rhs);

        public static bool operator !=(DataValue lhs, DataValue rhs) => !(lhs.Equals(rhs));

        public bool IsEmpty => json == null;

        public bool NonEmpty => json != null;

        public string JSON => IsEmpty ? "null" : json;

        public override string ToString() => JSON;

        public override int GetHashCode() => IsEmpty ? 0 : json.GetHashCode();

        public static readonly DataValue Empty = new DataValue(null);

        public static DataValue FromEnum(Enum v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromEnumArray(Enum[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromByte(byte v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromByteArray(byte[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromSByte(sbyte v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromSByteArray(sbyte[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromShort(short v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromShortArray(short[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromUShort(ushort v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromUShortArray(ushort[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromString(string v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromStringArray(string[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromFloat(float v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromFloatArray(float[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromDouble(double v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromDoubleArray(double[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromDecimal(decimal v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromDecimalArray(decimal[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLong(long v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLongArray(long[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromULong(ulong v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromULongArray(ulong[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromInt(int v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromIntArray(int[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromUInt(uint v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromUIntArray(uint[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromBool(bool v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromBoolArray(bool[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromDuration(Duration v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromDurationArray(Duration[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocationRef(LocationRef v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocationRefArray(LocationRef[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromTimestamp(Timestamp v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromTimestampArray(Timestamp[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocalDateTime(LocalDateTime v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocalDateTimeArray(LocalDateTime[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocalDate(LocalDate v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocalDateArray(LocalDate[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocalTime(LocalTime v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromLocalTimeArray(LocalTime[] v) => new DataValue(StdJson.ValueToString(v));

        public static DataValue FromObject(object obj, bool indented = false) => FromJSON(StdJson.ObjectToString(obj, indented: indented));

        public static DataValue FromJSON(string json) => string.IsNullOrWhiteSpace(json) || json == "null" ? new DataValue(null) : new DataValue(json);

        public static DataValue FromDataType(DataType dt, int dimension) {
            if (dimension < 0) throw new ArgumentException(nameof(dimension) + " may not be negative", nameof(dimension));
            switch (dimension) {
                case 0: return new DataValue("[]");
                case 1: return FromDataType(dt);
                default:
                    DataValue v = FromDataType(dt);
                    var res = new System.Text.StringBuilder(256);
                    res.Append('[');
                    int Last = dimension - 1;
                    for (int i = 0; i <= Last; ++i) {
                        res.Append(v.JSON);
                        if (i < Last) {
                            res.Append(", ");
                        }
                    }
                    res.Append(']');
                    return new DataValue(res.ToString());
            }
        }

        private static DataValue FromDataType(DataType dt) {
            switch (dt) {
                case DataType.Bool:
                    return new DataValue("false");

                case DataType.Byte:
                case DataType.SByte:
                case DataType.Int16:
                case DataType.UInt16:
                case DataType.Int32:
                case DataType.UInt32:
                case DataType.Int64:
                case DataType.UInt64:
                    return new DataValue("0");

                case DataType.Float32:
                case DataType.Float64:
                    return new DataValue("0.0");

                case DataType.String:
                case DataType.ObjectRef:
                case DataType.LocationRef:
                case DataType.URI:
                case DataType.Enum:
                    return new DataValue("\"\"");

                case DataType.JSON:
                    return new DataValue("\"0.0\"");

                case DataType.Guid:
                    return new DataValue("\"00000000-0000-0000-0000-00000000000\"");

                case DataType.NamedValue:
                    return new DataValue("{ \"Name\": \"\", \"Value\": \"\"}");

                case DataType.LocalDate:
                    return FromString(LocalDate.FromJavaTicks(0).ToString());

                case DataType.LocalTime:
                    return FromString(LocalTime.FromMillisecondsOfDay(0).ToString());

                case DataType.LocalDateTime:
                    return FromString(LocalDateTime.FromJavaTicks(0).ToString());

                case DataType.Timestamp:
                    return FromString(Timestamp.FromJavaTicks(0).ToString());

                case DataType.Duration:
                    return FromString(Duration.FromSeconds(0).ToString());

                case DataType.Struct:
                    return new DataValue("{}");
            }
            return new DataValue(null);
        }

        public object GetValue(DataType dt, int dimension) {
            if (dimension == 1) {
                switch (dt) {
                    case DataType.Bool: return this.GetBool();
                    case DataType.Byte: return this.GetByte();
                    case DataType.SByte: return this.GetSByte();
                    case DataType.Int16: return this.GetShort();
                    case DataType.UInt16: return this.GetUShort();
                    case DataType.Int32: return this.GetInt();
                    case DataType.UInt32: return this.GetUInt();
                    case DataType.Int64: return this.GetLong();
                    case DataType.UInt64: return this.GetULong();
                    case DataType.Float32: return this.GetFloat();
                    case DataType.Float64: return this.GetDouble();
                    case DataType.String: return this.GetString();
                    case DataType.ObjectRef: return ObjectRef.FromEncodedString(this.GetString());
                    case DataType.LocationRef: return LocationRef.FromLocationID(this.GetString());
                    case DataType.URI: return new Uri(this.GetString());
                    case DataType.Enum: return this.GetString();
                    case DataType.JSON: return this;
                    case DataType.Guid: return Guid.Parse(this.GetString());
                    case DataType.NamedValue: return this.Object<NamedValue>();
                    case DataType.LocalDate: return LocalDate.FromISO8601(this.GetString());
                    case DataType.LocalTime: return LocalTime.FromISO8601(this.GetString());
                    case DataType.LocalDateTime: return LocalDateTime.FromISO8601(this.GetString());
                    case DataType.Timestamp: return Timestamp.FromISO8601(this.GetString());
                    case DataType.Duration: return Duration.Parse(this.GetString());
                    case DataType.Struct: return json;
                }
            }
            else {
                switch (dt) {
                    case DataType.Bool: return this.GetBoolArray();
                    case DataType.Byte: return this.GetByteArray();
                    case DataType.SByte: return this.GetSByteArray();
                    case DataType.Int16: return this.GetShortArray();
                    case DataType.UInt16: return this.GetUShortArray();
                    case DataType.Int32: return this.GetIntArray();
                    case DataType.UInt32: return this.GetUIntArray();
                    case DataType.Int64: return this.GetLongArray();
                    case DataType.UInt64: return this.GetULongArray();
                    case DataType.Float32: return this.GetFloatArray();
                    case DataType.Float64: return this.GetDoubleArray();
                    case DataType.String: return this.GetStringArray();
                    case DataType.ObjectRef: return this.GetStringArray().Select(ObjectRef.FromEncodedString).ToArray();
                    case DataType.LocationRef: return this.GetStringArray().Select(LocationRef.FromLocationID).ToArray();
                    case DataType.URI: return this.GetStringArray().Select(x => new Uri(x)).ToArray();
                    case DataType.Enum: return this.GetStringArray();
                    case DataType.JSON: return this; // TODO return DataValue[]
                    case DataType.Guid: return this.GetStringArray().Select(Guid.Parse).ToArray();
                    case DataType.NamedValue: return this.Object<NamedValue[]>();
                    case DataType.LocalDate: return this.GetStringArray().Select(LocalDate.FromISO8601).ToArray();
                    case DataType.LocalTime: return this.GetStringArray().Select(LocalTime.FromISO8601).ToArray();
                    case DataType.LocalDateTime: return this.GetStringArray().Select(LocalDateTime.FromISO8601).ToArray();
                    case DataType.Timestamp: return this.GetStringArray().Select(Timestamp.FromISO8601).ToArray();
                    case DataType.Duration: return this.GetStringArray().Select(Duration.Parse).ToArray();
                    case DataType.Struct: return json;
                }
            }
            return null;
        }

        public static DataType TypeToDataType(Type t) {
            if (t == typeof(bool)) return DataType.Bool;
            if (t == typeof(byte)) return DataType.Byte;
            if (t == typeof(sbyte)) return DataType.SByte;
            if (t == typeof(short)) return DataType.Int16;
            if (t == typeof(ushort)) return DataType.UInt16;
            if (t == typeof(int)) return DataType.Int32;
            if (t == typeof(uint)) return DataType.UInt32;
            if (t == typeof(long)) return DataType.Int64;
            if (t == typeof(ulong)) return DataType.UInt64;
            if (t == typeof(double)) return DataType.Float64;
            if (t == typeof(float)) return DataType.Float32;
            if (t == typeof(string)) return DataType.String;
            if (t == typeof(Guid)) return DataType.Guid;
            if (t == typeof(ObjectRef)) return DataType.ObjectRef;
            if (t == typeof(LocationRef)) return DataType.LocationRef;
            if (t == typeof(NamedValue)) return DataType.NamedValue;
            if (t == typeof(Uri)) return DataType.URI;
            if (t == typeof(LocalDate)) return DataType.LocalDate;
            if (t == typeof(LocalTime)) return DataType.LocalTime;
            if (t == typeof(LocalDateTime)) return DataType.LocalDateTime;
            if (t == typeof(Timestamp)) return DataType.Timestamp;
            if (t == typeof(Duration)) return DataType.Duration;
            if (t == typeof(DataValue)) return DataType.JSON;
            if (t.IsEnum) return DataType.Enum;
            return DataType.Struct;
        }

        public bool IsArray => !string.IsNullOrEmpty(json) && (json[0] == '[' || FirstNonWhitespace(json) == '[');

        public bool IsBool => json == "true" || json == "false";

        public T Object<T>() => StdJson.ObjectFromString<T>(json);

        public object Object(Type t) => StdJson.ObjectFromString(t, json);

        public void PopulateObject(object obj) {
            StdJson.PopulateObject(json, obj);
        }

        public double? AsDouble() {
            try {
                return StdJson.ToDouble(json);
            }
            catch (Exception) {
                if (json == "true") return 1;
                if (json == "false") return 0;
                if (json == null) return null;
                try {
                    return StdJson.ToDoubleArrayAcceptingFloats(json)[0];
                }
                catch (Exception) { }
            }
            return null;
        }

        public double GetDouble() => StdJson.ToDouble(json);

        public double[] GetDoubleArray() => StdJson.ToDoubleArray(json);

        public float GetFloat() => StdJson.ToFloat(json);

        public float[] GetFloatArray() => StdJson.ToFloatArray(json);

        public decimal GetDecimal() => StdJson.ToDecimal(json);

        public decimal[] GetDecimalArray() => StdJson.ToDecimalArray(json);

        public short GetShort() => StdJson.ToShort(json);

        public short[] GetShortArray() => StdJson.ToShortArray(json);

        public ushort GetUShort() => StdJson.ToUShort(json);

        public ushort[] GetUShortArray() => StdJson.ToUShortArray(json);

        public int GetInt() => StdJson.ToInt(json);

        public int[] GetIntArray() => StdJson.ToIntArray(json);

        public uint GetUInt() => StdJson.ToUInt(json);

        public uint[] GetUIntArray() => StdJson.ToUIntArray(json);

        public long GetLong() => StdJson.ToLong(json);

        public long[] GetLongArray() => StdJson.ToLongArray(json);

        public ulong GetULong() => StdJson.ToULong(json);

        public ulong[] GetULongArray() => StdJson.ToULongArray(json);

        public bool GetBool() => StdJson.ToBool(json);

        public bool[] GetBoolArray() => StdJson.ToBoolArray(json);

        public string GetString() => StdJson.ObjectFromString<string>(json);

        public string[] GetStringArray() => StdJson.ObjectFromString<string[]>(json);

        public byte GetByte() => StdJson.ToByte(json);

        public byte[] GetByteArray() => StdJson.ToByteArray(json);

        public sbyte GetSByte() => StdJson.ToSByte(json);

        public sbyte[] GetSByteArray() => StdJson.ToSByteArray(json);

        public T GetEnum<T>() => StdJson.ObjectFromString<T>(json);

        public T[] GetEnumArray<T>() => StdJson.ObjectFromString<T[]>(json);

        public Timestamp GetTimestamp() => Timestamp.FromISO8601(GetString());

        private static char FirstNonWhitespace(string str) {
            for (int i = 0; i < str.Length; ++i) {
                char c = str[i];
                if (!Char.IsWhiteSpace(c)) return c;
            }
            return ' ';
        }

        public XmlSchema GetSchema() => null;

        public void ReadXml(XmlReader reader) {
            string s = reader.ReadElementContentAsString();
            json = FromJSON(s).json;
        }

        public void WriteXml(XmlWriter writer) {
            string w = json;
            if (w != null) {
                writer.WriteString(w);
            }
        }
    }
}
