﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Json;
using Ifak.Fast.Json.Converters;
using Ifak.Fast.Json.Linq;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    public class StdJson
    {
        private static readonly JsonSerializerSettings settingsDefault = new JsonSerializerSettings {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new JsonConverter[] { new StringEnumConverter() }
        };

        private static readonly JsonSerializerSettings settingsIdented = new JsonSerializerSettings {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Ignore,
            Converters = new JsonConverter[] { new StringEnumConverter() },
            Formatting = Formatting.Indented
        };

        private static readonly JsonSerializerSettings settingsNull = new JsonSerializerSettings {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Include,
            Converters = new JsonConverter[] { new StringEnumConverter() }
        };

        private static readonly JsonSerializerSettings settingsIdentedNull = new JsonSerializerSettings {
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Include,
            Converters = new JsonConverter[] { new StringEnumConverter() },
            Formatting = Formatting.Indented
        };

        private static JsonSerializerSettings GetSettings(bool indented, bool ignoreNull) {
            if (!indented && ignoreNull) return settingsDefault;
            if (!indented && !ignoreNull) return settingsNull;
            if (ignoreNull) return settingsIdented;
            return settingsIdentedNull;
        }

        public static string ObjectToString(object value, bool indented = false, bool ignoreNullValues = false) {
            return JsonConvert.SerializeObject(value, GetSettings(indented, ignoreNullValues));
        }

        public static string ValueToString(bool value) => JsonConvert.ToString(value);

        public static string ValueToString(bool[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(char value) => JsonConvert.ToString(value);

        public static string ValueToString(char[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(Enum value) => ValueToString(value.ToString());

        public static string ValueToString(Enum[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(int value) => JsonConvert.ToString(value);

        public static string ValueToString(int[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(short value) => JsonConvert.ToString(value);

        public static string ValueToString(short[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(ushort value) => JsonConvert.ToString(value);

        public static string ValueToString(ushort[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(uint value) => JsonConvert.ToString(value);

        public static string ValueToString(uint[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(long value) => JsonConvert.ToString(value);

        public static string ValueToString(long[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(ulong value) => JsonConvert.ToString(value);

        public static string ValueToString(ulong[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(float value) => JsonConvert.ToString(value);

        public static string ValueToString(float[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(double value) => JsonConvert.ToString(value);

        public static string ValueToString(double[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(decimal value) => JsonConvert.ToString(value);

        public static string ValueToString(decimal[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(byte value) => JsonConvert.ToString(value);

        public static string ValueToString(byte[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(sbyte value) => JsonConvert.ToString(value);

        public static string ValueToString(sbyte[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(Guid value) => JsonConvert.ToString(value);

        public static string ValueToString(Guid[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(TimeSpan value) => JsonConvert.ToString(value);

        public static string ValueToString(TimeSpan[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(Uri value) => JsonConvert.ToString(value);

        public static string ValueToString(Uri[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(string value) => JsonConvert.ToString(value);

        public static string ValueToString(string[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(ObjectRef value) => JsonConvert.ToString(value.ToEncodedString());

        public static string ValueToString(ObjectRef[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(LocalDate value) => JsonConvert.ToString(value.ToString());

        public static string ValueToString(LocalDate[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(LocalTime value) => JsonConvert.ToString(value.ToString());

        public static string ValueToString(LocalTime[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(LocalDateTime value) => JsonConvert.ToString(value.ToString());

        public static string ValueToString(LocalDateTime[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(Timestamp value) => JsonConvert.ToString(value.ToString());

        public static string ValueToString(Timestamp[] array) => ArrayToString(array, ValueToString);

        public static string ValueToString(Duration value) => JsonConvert.ToString(value.ToString());

        public static string ValueToString(Duration[] array) => ArrayToString(array, ValueToString);

        private static string ArrayToString<T>(T[] array, Func<T, string> f) {
            if (array == null || array.Length == 0) return "[]";
            var res = new StringBuilder(256);
            res.Append('[');
            int Last = array.Length - 1;
            for (int i = 0; i <= Last; ++i) {
                res.Append(f(array[i]));
                if (i < Last) {
                    res.Append(',');
                }
            }
            res.Append(']');
            return res.ToString();
        }

        public static void ObjectToWriter(object value, TextWriter writer, bool indented = false, bool ignoreNullValues = false) {
            var serializer = JsonSerializer.CreateDefault(GetSettings(indented, ignoreNullValues));
            serializer.Serialize(writer, value);
        }

        public static byte[] ObjectToBytes(object value, bool indented = false, bool ignoreNullValues = false) {
            var buffer = new MemoryStream(512);
            ObjectToStream(value, buffer, indented, ignoreNullValues);
            return buffer.ToArray();
        }

        private readonly static Encoding UTF8_NoBOM = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public static void ObjectToStream(object value, Stream streamOut, bool indented = false, bool ignoreNullValues = false) {
            using (var writer = new StreamWriter(streamOut, UTF8_NoBOM, 1024, leaveOpen: true)) {
                ObjectToWriter(value, writer, indented, ignoreNullValues);
            }
        }

        public static bool ObjectsDeepEqual(object a, object b) {
            using (MemoryStream bufA = Util.MemoryManager.GetMemoryStream("ObjectEqualsA"),
                                bufB = Util.MemoryManager.GetMemoryStream("ObjectEqualsB")) {

                ObjectToStream(a, bufA);
                ObjectToStream(b, bufB);

                if (bufA.Position != bufB.Position) return false;
                bufA.Seek(0, SeekOrigin.Begin);
                bufB.Seek(0, SeekOrigin.Begin);

                int len = (int)bufA.Length;
                for (int i = 0; i < len; ++i) {
                    if (bufA.ReadByte() != bufB.ReadByte()) return false;
                }
            }
            return true;
        }

        public static bool ToBool(string s) {
            bool res = false;
            if (bool.TryParse(s, out res)) return res;
            double v = 0.0;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v != 0.0;
            return bool.Parse(s);
        }

        public static bool[] ToBoolArray(string s) => ToPrimitiveArray(s, ToBool);

        public static byte ToByte(string s) => byte.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static byte[] ToByteArray(string s) => ToPrimitiveArray(s, ToByte);

        public static sbyte ToSByte(string s) => sbyte.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static sbyte[] ToSByteArray(string s) => ToPrimitiveArray(s, ToSByte);

        public static short ToShort(string s) => short.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static short[] ToShortArray(string s) => ToPrimitiveArray(s, ToShort);

        public static ushort ToUShort(string s) => ushort.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static ushort[] ToUShortArray(string s) => ToPrimitiveArray(s, ToUShort);

        public static int ToInt(string s) => int.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static int[] ToIntArray(string s) => ToPrimitiveArray(s, ToInt);

        public static uint ToUInt(string s) => uint.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static uint[] ToUIntArray(string s) => ToPrimitiveArray(s, ToUInt);

        public static long ToLong(string s) => long.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static long[] ToLongArray(string s) => ToPrimitiveArray(s, ToLong);

        public static ulong ToULong(string s) => ulong.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static ulong[] ToULongArray(string s) => ToPrimitiveArray(s, ToULong);

        public static float ToFloat(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static float[] ToFloatArray(string s) => ToPrimitiveArray(s, ToFloat);

        public static double ToDouble(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        public static double[] ToDoubleArray(string s) => ToPrimitiveArray(s, ToDouble);

        public static decimal ToDecimal(string s) => decimal.Parse(s, CultureInfo.InvariantCulture);

        public static decimal[] ToDecimalArray(string s) => ToPrimitiveArray(s, ToDecimal);


        private static T[] ToPrimitiveArray<T>(string str, Func<string,T> parseNumber) where T : struct {

            int countSeparators = 0;
            for (int i = 0; i < str.Length; ++i) {
                if (str[i] == ',') countSeparators += 1;
            }

            if (countSeparators == 0 && str == "[]") {
                return new T[0];
            }

            T[] result = new T[countSeparators + 1];
            int pos = 0;

            const int S_SearchArrayStart = 0;
            const int S_Value = 1;
            const int S_End = 2;
            int state = S_SearchArrayStart;

            var buffer = new StringBuilder();

            for (int i = 0; i < str.Length; ++i) {
                char c = str[i];
                bool whitespace = Char.IsWhiteSpace(c);

                switch (state) {

                    case S_SearchArrayStart:

                        if (c == '[') {
                            state = S_Value;
                        }
                        else if (!whitespace) {
                            throw new FormatException("Start of array not found");
                        }
                        break;

                    case S_Value:

                        if (c == ',') {
                            result[pos++] = parseNumber(buffer.ToString());
                            buffer.Clear();
                        }
                        else if (c == ']') {
                            if (buffer.Length > 0) {
                                result[pos++] = parseNumber(buffer.ToString());
                                buffer.Clear();
                            }
                            else {
                                if (pos == 0) {
                                    result = new T[0];
                                }
                                else {
                                    throw new FormatException("Invalid array");
                                }
                            }
                            state = S_End;
                        }
                        else {
                            if (!whitespace || buffer.Length > 0) {
                                buffer.Append(c);
                            }
                        }
                        break;

                    case S_End:

                        if (!whitespace) {
                            throw new FormatException($"Unexpected character '{c}' after array end");
                        }
                        break;
                }
            }
            if (state != S_End) throw new FormatException("Not a valid array");
            return result;
        }

        public static T ObjectFromString<T>(string json) {
            return JsonConvert.DeserializeObject<T>(json, settingsDefault);
        }

        public static object ObjectFromString(Type t, string json) {
            return JsonConvert.DeserializeObject(json, t, settingsDefault);
        }

        public static T ObjectFromReader<T>(TextReader reader) {
            var serializer = JsonSerializer.CreateDefault(settingsDefault);
            return (T)serializer.Deserialize(reader, typeof(T));
        }

        public static T ObjectFromUtf8Stream<T>(Stream stream) {
            using (var reader = new StreamReader(stream, Encoding.UTF8)) {
                return StdJson.ObjectFromReader<T>(reader);
            }
        }

        public static T ObjectFromJToken<T>(JToken jToken) {
            return jToken.ToObject<T>();
        }

        public static JToken JTokenFromReader(TextReader reader) {
            using (var jReader = new JsonTextReader(reader)) {
                jReader.DateParseHandling = DateParseHandling.None;
                return JToken.ReadFrom(jReader);
            }
        }

        public static async Task<JToken> JTokenFromReaderAsync(TextReader reader) {
            using (var jReader = new JsonTextReader(reader)) {
                jReader.DateParseHandling = DateParseHandling.None;
                return await JToken.ReadFromAsync(jReader);
            }
        }

        public static JToken JTokenFromString(string json) {
            return JTokenFromReader(new StringReader(json));
        }

        public static JObject JObjectFromReader(TextReader reader) {
            return (JObject)JTokenFromReader(reader);
        }

        public static async Task<JObject> JObjectFromReaderAsync(TextReader reader) {
            return (JObject)await JTokenFromReaderAsync(reader);
        }

        public static JObject JObjectFromString(string json) {
            return JObjectFromReader(new StringReader(json));
        }

        public static void PopulateObject(string json, object obj) {
            JsonConvert.PopulateObject(json, obj, settingsDefault);
        }
    }

    public class TimestampConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Timestamp);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = reader.Value as string;
            if (str != null) {
                return Timestamp.FromISO8601(str);
            }
            DateTime dt = (DateTime)reader.Value;
            return Timestamp.FromDateTime(dt);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            Timestamp ts = (Timestamp)value;
            writer.WriteValue(ts.ToString());
        }
    }

    public class LocalDateTimeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(LocalDateTime);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = reader.Value as string;
            if (str != null) {
                return LocalDateTime.FromISO8601(str);
            }
            DateTime dt = (DateTime)reader.Value;
            return LocalDateTime.FromDateTime(dt);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            LocalDateTime ts = (LocalDateTime)value;
            writer.WriteValue(ts.ToString());
        }
    }

    public class LocalDateConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(LocalDate);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = reader.Value as string;
            if (str != null) {
                return LocalDate.FromISO8601(str);
            }
            DateTime dt = (DateTime)reader.Value;
            return LocalDate.FromDateTime(dt);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            LocalDate ts = (LocalDate)value;
            writer.WriteValue(ts.ToString());
        }
    }

    public class LocalTimeConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(LocalTime);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = reader.Value as string;
            if (str != null) {
                return LocalTime.FromISO8601(str);
            }
            DateTime dt = (DateTime)reader.Value;
            return LocalTime.FromDateTime(dt);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            LocalTime ts = (LocalTime)value;
            writer.WriteValue(ts.ToString());
        }
    }

    public class DurationConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(Duration);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = (string)reader.Value;
            return Duration.Parse(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            Duration ts = (Duration)value;
            writer.WriteValue(ts.ToString());
        }
    }

    public class ObjectRefConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(ObjectRef);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = (string)reader.Value;
            return ObjectRef.FromEncodedString(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            ObjectRef ts = (ObjectRef)value;
            writer.WriteValue(ts.ToEncodedString());
        }
    }

    public class LocationRefConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(LocationRef);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = (string)reader.Value;
            return LocationRef.FromLocationID(str ?? "");
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            LocationRef ts = (LocationRef)value;
            writer.WriteValue(ts.ToString());
        }
    }

    public class DataValueConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(DataValue);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            string str = (string)reader.Value;
            return DataValue.FromJSON(str);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            DataValue ts = (DataValue)value;
            writer.WriteValue(ts.JSON);
        }
    }
}