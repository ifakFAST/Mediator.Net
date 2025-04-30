// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using Ifak.Fast.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace Ifak.Fast.Mediator
{
    public enum DataType
    {
        Bool,
        Byte,
        SByte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Float32,
        Float64,
        String,
        JSON,
        Guid,
        ObjectRef,
        NamedValue,
        LocationRef,
        URI,
        LocalDate,
        LocalTime,
        LocalDateTime,
        Timestamp,
        Duration,
        Enum,
        Struct,
        Timeseries // array of TimeseriesEntry objects
    }

    public static class DataTypeExtension
    {
        public static bool IsFloat(this DataType t) => DataTypeSets.Floats.Contains(t);
        public static bool IsNumeric(this DataType t) => DataTypeSets.Numerics.Contains(t);
        public static bool IsInteger(this DataType t) => DataTypeSets.Integers.Contains(t);
        public static bool IsSignedInteger(this DataType t) => DataTypeSets.SignedIntegers.Contains(t);
        public static bool IsUnsignedInteger(this DataType t) => DataTypeSets.UnsignedIntegers.Contains(t);
    }

    public static class DataTypeSets
    {
        public static readonly ReadOnlySet<DataType> Floats = new ReadOnlySet<DataType>(
            DataType.Float32,
            DataType.Float64
        );

        public static readonly ReadOnlySet<DataType> UnsignedIntegers = new ReadOnlySet<DataType>(
            DataType.Byte,
            DataType.UInt16,
            DataType.UInt32,
            DataType.UInt64
        );

        public static readonly ReadOnlySet<DataType> SignedIntegers = new ReadOnlySet<DataType>(
            DataType.SByte,
            DataType.Int16,
            DataType.Int32,
            DataType.Int64
        );

        public static readonly ReadOnlySet<DataType> Integers = new ReadOnlySet<DataType>(
           UnsignedIntegers,
           SignedIntegers
       );

        public static readonly ReadOnlySet<DataType> Numerics = new ReadOnlySet<DataType>(
            Floats,
            Integers
        );
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(LocationRefConverter))]
    public struct LocationRef : IEquatable<LocationRef>, IXmlSerializable
    {
        private string locationID;

        private LocationRef(string locationID) {
            this.locationID = locationID;
        }

        public readonly string LocationID => locationID ?? "";

        public readonly bool Equals(LocationRef other) => LocationID == other.LocationID;

        public override readonly int GetHashCode() => LocationID.GetHashCode();

        public override readonly bool Equals(object obj) {
            if (obj is LocationRef locRef) {
                return Equals(locRef);
            }
            return false;
        }

        public static bool operator ==(LocationRef a, LocationRef b) => a.Equals(b);

        public static bool operator !=(LocationRef a, LocationRef b) => !a.Equals(b);

        public override readonly string ToString() => LocationID;

        public static LocationRef FromLocationID(string locationID) {
            if (locationID == null) throw new ArgumentNullException("LocationRef.FromLocationID: parameter may not be null");
            return new LocationRef(locationID);
        }

        public readonly void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }

        public void ReadXml(XmlReader reader) {
            locationID = reader.ReadString() ?? "";
            reader.Read();
        }

        public readonly XmlSchema? GetSchema() => null;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(DurationConverter))]
    public struct Duration : IComparable<Duration>, IEquatable<Duration>, IXmlSerializable
    {
        private const long MS_PerSecond = 1000L;
        private const long MS_PerMinute = 60 * MS_PerSecond;
        private const long MS_PerHour = 60 * MS_PerMinute;
        private const long MS_PerDay = 24 * MS_PerHour;

        private long millis;

        private Duration(long millis) {
            this.millis = millis;
        }

        public static readonly Duration Zero = new Duration(0);

        public static Duration FromMilliseconds(long ms) => new Duration(ms);

        public static Duration FromSeconds(long s) => new Duration(s * MS_PerSecond);

        public static Duration FromMinutes(long min) => new Duration(min * MS_PerMinute);

        public static Duration FromHours(long h) => new Duration(h * MS_PerHour);

        public static Duration FromDays(long d) => new Duration(d * MS_PerDay);

        public static Duration FromTimeSpan(TimeSpan span) => new Duration((long)span.TotalMilliseconds);

        public TimeSpan ToTimeSpan() => TimeSpan.FromMilliseconds(millis);

        public long TotalMilliseconds => millis;

        public double TotalSeconds => ToTimeSpan().TotalSeconds;

        public double TotalMinutes => ToTimeSpan().TotalMinutes;

        public double TotalHours => ToTimeSpan().TotalHours;

        public double TotalDays => ToTimeSpan().TotalDays;

        public bool Equals(Duration other) => millis == other.millis;

        public override int GetHashCode() => (int)millis;

        public override bool Equals(object obj) {
            if (obj is Duration) {
                return Equals((Duration)obj);
            }
            return false;
        }

        public Duration Negate() => Duration.FromMilliseconds(-1 * millis);

        public Duration Abs() => Duration.FromMilliseconds(Math.Abs(millis));

        public static bool operator ==(Duration a, Duration b) => a.Equals(b);

        public static bool operator !=(Duration a, Duration b) => !a.Equals(b);

        public static bool operator <(Duration a, Duration b) => a.millis < b.millis;

        public static bool operator >(Duration a, Duration b) => a.millis > b.millis;

        public static bool operator <=(Duration a, Duration b) => a.millis <= b.millis;

        public static bool operator >=(Duration a, Duration b) => a.millis >= b.millis;

        public static Duration operator +(Duration a, Duration b) => Duration.FromMilliseconds(a.millis + b.millis);

        public override string ToString() {

            if (millis == 0) return "0 s";

            long millisAbs = Math.Abs(millis);

            if (millisAbs % MS_PerSecond > 0) {
                return millis.ToString() + " ms";
            }

            if (millisAbs % MS_PerMinute > 0) {
                return (millis / MS_PerSecond).ToString() + " s";
            }

            if (millisAbs % MS_PerHour > 0) {
                return (millis / MS_PerMinute).ToString() + " min";
            }

            if (millisAbs % MS_PerDay > 0) {
                return (millis / MS_PerHour).ToString() + " h";
            }

            return (millis / MS_PerDay).ToString() + " d";
        }

        /// <summary>
        /// Converts the string representation of a time interval to its Mediator.Duration equivalent.
        /// Input s must be in one of three formats: Duration.ToString (e.g. "12 h"),
        /// TimeSpan.ToString (e.g. "12:00:00"), or xsd:duration (ISO 8601 e.g. "PT12H")
        /// </summary>
        /// <param name="s">A string that specifies the time duration to convert.</param>
        /// <returns>A time interval that corresponds to s.</returns>
        public static Duration Parse(string s) {

            if (s == null) throw new ArgumentNullException("Duration.Parse: string may not be null");

            int len = s.Length;
            if (len == 0) throw new ArgumentException("Duration.Parse: string may not be empty");

            if (char.IsWhiteSpace(s[0]) || char.IsWhiteSpace(s[len - 1])) {
                s = s.Trim();
            }

            if (s.Length == 0) throw new ArgumentException("Duration.Parse: string may not be empty");

            if (s[0] == 'P') return Duration.FromTimeSpan(System.Xml.XmlConvert.ToTimeSpan(s));
            if (s.IndexOf(':') > 0) return Duration.FromTimeSpan(TimeSpan.Parse(s));

            int i = s.IndexOf(' ');
            if (i < 0) {
                i = s.IndexOfAny(new char[] { 'm', 's', 'h', 'd' });
                if (i <= 0) {
                    throw new ArgumentException("Duration.Parse: Invalid format of '" + s + "'");
                }
            }

            string strCount = s.Substring(0, i);
            long count;
            if (!long.TryParse(strCount, out count)) {
                throw new ArgumentException("Duration.Parse: Invalid format of '" + s + "'");
            }

            string unit;
            if (char.IsWhiteSpace(s[i]))
                unit = s.Substring(i + 1);
            else
                unit = s.Substring(i);

            if (unit.Length == 0) throw new ArgumentException("Duration.Parse: Invalid format of '" + s + "'");
            if (char.IsWhiteSpace(unit[0]))
                unit = unit.TrimStart();

            unit = unit.ToLowerInvariant();

            if (unit == "ms") return Duration.FromMilliseconds(count);
            if (unit == "s") return Duration.FromSeconds(count);
            if (unit == "min") return Duration.FromMinutes(count);
            if (unit == "m") return Duration.FromMinutes(count);
            if (unit == "h") return Duration.FromHours(count);
            if (unit == "d") return Duration.FromDays(count);

            throw new ArgumentException("Duration.Parse: Invalid format of '" + s + "'");
        }

        public static bool TryParse(string s, out Duration result) {
            try {
                result = Parse(s);
                return true;
            } catch {
                result = Duration.Zero;
                return false;
            }
        }

        public int CompareTo(Duration other) {
            long a = millis;
            long b = other.millis;
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }

        public void ReadXml(XmlReader reader) {
            millis = Duration.Parse(reader.ReadString()).millis;
            reader.Read();
        }

        public XmlSchema? GetSchema() => null;

        public static implicit operator TimeSpan(Duration d) => d.ToTimeSpan();
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(TimestampConverter))]
    public struct Timestamp : IComparable<Timestamp>, IEquatable<Timestamp>, IXmlSerializable
    {
        public static readonly Timestamp Empty = new(0);
        public static readonly Timestamp Max = FromDateTime(DateTime.MaxValue);

        private long ticks; // Java ticks (0 == 1. Jan. 1970 UTC)

        private Timestamp(long ticks) {
            this.ticks = ticks;
        }

        public readonly bool IsEmpty => ticks == 0;

        public readonly bool NonEmpty => ticks != 0;

        public static Timestamp FromComponents(int year, int month, int day) {
            var dt = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            return FromDateTime(dt);
        }

        public static Timestamp FromComponents(int year, int month, int day, int hour, int minute, int second) {
            var dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
            return FromDateTime(dt);
        }

        public static Timestamp FromComponents(int year, int month, int day, int hour, int minute, int second, int millisecond) {
            var dt = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
            return FromDateTime(dt);
        }

        public static Timestamp FromJavaTicks(long ticks) { return new Timestamp(ticks); }

        public static Timestamp FromDotNetTicks(long ticks) {
            return new Timestamp((ticks - TICKS_AT_EPOCH) / TICKS_PER_MILLISECOND);
        }

        public static Timestamp FromDateTime(DateTime dateTime) {
            return FromDotNetTicks(dateTime.ToUniversalTime().Ticks);
        }

        public static Timestamp FromLocalDateTime(LocalDateTime localDateTime) {
            return FromDotNetTicks(localDateTime.ToDateTime().ToUniversalTime().Ticks);
        }

        public static Timestamp FromISO8601(string str) {
            return FromDateTime(DateTime.Parse(str, null, DateTimeStyles.RoundtripKind));
        }

        public static Timestamp Parse(string str) {
            return FromISO8601(str);
        }

        public static bool TryParse(string s, out Timestamp result) {
            try {
                result = Parse(s);
                return true;
            } catch {
                result = Empty;
                return false;
            }
        }

        public static Timestamp Now => FromDotNetTicks(DateTime.UtcNow.Ticks);

        public readonly Timestamp AddMillis(long ms) {
            return new Timestamp(ticks + ms);
        }

        public readonly Timestamp AddSeconds(long seconds) {
            return new Timestamp(ticks + (seconds * 1000L));
        }

        public readonly Timestamp AddMinutes(long minutes) {
            return new Timestamp(ticks + (minutes * 60L * 1000L));
        }

        public readonly Timestamp AddHours(long hours) {
            return new Timestamp(ticks + (hours * 60L * 60L * 1000L));
        }

        public readonly Timestamp AddDays(long days) {
            return new Timestamp(ticks + (days * 24 * 60L * 60L * 1000L));
        }

        public readonly Timestamp AddTimeSpan(TimeSpan span) {
            return new Timestamp(ticks + span.Ticks / TimeSpan.TicksPerMillisecond);
        }

        public readonly Timestamp AddDuration(Duration duration) {
            return new Timestamp(ticks + duration.TotalMilliseconds);
        }

        public readonly Timestamp TruncateMilliseconds() => new Timestamp((ticks / 1000) * 1000);

        public readonly Timestamp TruncateSeconds() => new Timestamp((ticks / 60000) * 60000);

        public readonly Timestamp Truncate5Seconds() => new Timestamp((ticks / 5000) * 5000);

        public readonly Timestamp Truncate10Seconds() => new Timestamp((ticks / 10000) * 10000);

        public readonly Timestamp Truncate30Seconds() => new Timestamp((ticks / 30000) * 30000);

        public readonly Timestamp Truncate(Duration interval) => new Timestamp((ticks / interval.TotalMilliseconds) * interval.TotalMilliseconds);

        public readonly long JavaTicks
        {
            get { return ticks; }
        }

        public readonly long DotNetTicks
        {
            get { return (ticks * TICKS_PER_MILLISECOND) + TICKS_AT_EPOCH; }
        }

        public readonly DateTime ToDateTime() {
            return new DateTime(DotNetTicks, DateTimeKind.Utc);
        }

        public readonly DateTime ToDateTimeUnspecified() {
            return new DateTime(DotNetTicks, DateTimeKind.Unspecified);
        }

        public readonly override string ToString() {
            var d = ToDateTime();
            if (ticks % 1000 != 0)
                return d.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fffK", CultureInfo.InvariantCulture);
            else
                return d.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ssK", CultureInfo.InvariantCulture);
        }

        public readonly override int GetHashCode() {
            return (int)ticks;
        }

        public readonly override bool Equals(object obj) {
            if (obj is Timestamp timestamp) {
                return Equals(timestamp);
            }
            return false;
        }

        public readonly bool Equals(Timestamp other) => ticks == other.ticks;

        public readonly int CompareTo(Timestamp other) {
            long a = ticks;
            long b = other.ticks;
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        public static Timestamp MaxOf(Timestamp t1, Timestamp t2) {
            return t1.ticks > t2.ticks ? t1 : t2;
        }

        public static Timestamp MinOf(Timestamp t1, Timestamp t2) {
            return t1.ticks < t2.ticks ? t1 : t2;
        }

        public static bool operator ==(Timestamp a, Timestamp b) {
            return a.Equals(b);
        }

        public static bool operator !=(Timestamp a, Timestamp b) {
            return !a.Equals(b);
        }

        public static Timestamp operator +(Timestamp t, TimeSpan span) {
            return t.AddTimeSpan(span);
        }

        public static Timestamp operator +(Timestamp t, Duration duration) {
            return t.AddDuration(duration);
        }

        public static Timestamp operator -(Timestamp t, TimeSpan span) {
            return t.AddTimeSpan(span.Negate());
        }

        public static Timestamp operator -(Timestamp t, Duration duration) {
            return t.AddDuration(duration.Negate());
        }

        public static Duration operator -(Timestamp t1, Timestamp t2) {
            return Duration.FromMilliseconds(t1.ticks - t2.ticks);
        }

        public static bool operator <(Timestamp t1, Timestamp t2) {
            return t1.ticks < t2.ticks;
        }

        public static bool operator >(Timestamp t1, Timestamp t2) {
            return t1.ticks > t2.ticks;
        }

        public static bool operator <=(Timestamp t1, Timestamp t2) {
            return t1.ticks <= t2.ticks;
        }

        public static bool operator >=(Timestamp t1, Timestamp t2) {
            return t1.ticks >= t2.ticks;
        }

        private const long TICKS_AT_EPOCH = 621355968000000000L;
        private const long TICKS_PER_MILLISECOND = 10000;

        public readonly void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }

        public void ReadXml(XmlReader reader) {
            ticks = FromISO8601(reader.ReadString()).JavaTicks;
            reader.Read();
        }

        public readonly XmlSchema? GetSchema() => null;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(LocalDateTimeConverter))]
    public struct LocalDateTime : IComparable<LocalDateTime>, IEquatable<LocalDateTime>, IXmlSerializable
    {
        private long ticks; // Java ticks (0 == 1. Jan. 1970)

        private LocalDateTime(long ticks) {
            this.ticks = ticks;
        }

        public static LocalDateTime FromJavaTicks(long ticks) => new LocalDateTime(ticks);

        public static LocalDateTime FromDotNetTicks(long ticks) => new LocalDateTime((ticks - TICKS_AT_EPOCH) / TICKS_PER_MILLISECOND);

        public static LocalDateTime FromDateTime(DateTime dateTime) => FromDotNetTicks(dateTime.Ticks);

        public static LocalDateTime FromDateAndTime(LocalDate date, LocalTime time) => date + time;

        public static LocalDateTime FromComponents(int year, int month, int day) {
            var dt = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local);
            return FromDateTime(dt);
        }

        public static LocalDateTime FromComponents(int year, int month, int day, int hour, int minute, int second) {
            var dt = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Local);
            return FromDateTime(dt);
        }

        public static LocalDateTime FromComponents(int year, int month, int day, int hour, int minute, int second, int millisecond) {
            var dt = new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Local);
            return FromDateTime(dt);
        }

        public static LocalDateTime FromISO8601(string str) {
            return FromDateTime(DateTime.Parse(str, null, DateTimeStyles.RoundtripKind));
        }

        public static LocalDateTime Now => FromDotNetTicks(DateTime.Now.Ticks);

        public long DotNetTicks => (ticks * TICKS_PER_MILLISECOND) + TICKS_AT_EPOCH;

        public long JavaTicks => ticks;

        public LocalDateTime TruncateMilliseconds() => new LocalDateTime((ticks / 1000L) * 1000L);

        public LocalDateTime TruncateSeconds() => new LocalDateTime((ticks / 60000L) * 60000L);

        public DateTime ToDateTime() => new DateTime(DotNetTicks, DateTimeKind.Local);

        public LocalTime ToLocalTime() => LocalTime.FromDateTime(ToDateTime());

        public bool Equals(LocalDateTime other) => ticks == other.ticks;

        public override bool Equals(object obj) {
            if (obj is LocalDateTime) {
                return Equals((LocalDateTime)obj);
            }
            return false;
        }

        public static bool operator ==(LocalDateTime lhs, LocalDateTime rhs) => lhs.Equals(rhs);

        public static bool operator !=(LocalDateTime lhs, LocalDateTime rhs) => !(lhs.Equals(rhs));

        public static bool operator <(LocalDateTime a, LocalDateTime b) => a.ticks < b.ticks;

        public static bool operator >(LocalDateTime a, LocalDateTime b) => a.ticks > b.ticks;

        public static bool operator <=(LocalDateTime a, LocalDateTime b) => a.ticks <= b.ticks;

        public static bool operator >=(LocalDateTime a, LocalDateTime b) => a.ticks >= b.ticks;

        public static LocalDateTime operator +(LocalDateTime t, Duration duration) => t.AddDuration(duration);

        public static LocalDateTime operator -(LocalDateTime t, Duration duration) => t.AddDuration(duration.Negate());

        public LocalDateTime AddDuration(Duration duration) => new LocalDateTime(ticks + duration.TotalMilliseconds);

        public override string ToString() {
            var d = ToDateTime();
            if (ticks % 1000 != 0) return d.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff", CultureInfo.InvariantCulture);
            if (ticks % 60000 != 0) return d.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss", CultureInfo.InvariantCulture);
            return d.ToString("yyyy'-'MM'-'dd'T'HH':'mm", CultureInfo.InvariantCulture);
        }

        public override int GetHashCode() => (int)ticks;

        public int CompareTo(LocalDateTime other) {
            long a = ticks;
            long b = other.ticks;
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        private const long TICKS_AT_EPOCH = 621355968000000000L;
        private const long TICKS_PER_MILLISECOND = 10000;

        public void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }

        public void ReadXml(XmlReader reader) {
            ticks = FromISO8601(reader.ReadString()).JavaTicks;
            reader.Read();
        }

        public XmlSchema? GetSchema() => null;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(LocalDateConverter))]
    public struct LocalDate : IComparable<LocalDate>, IEquatable<LocalDate>, IXmlSerializable
    {
        private long ticks; // Java ticks (0 == 1. Jan. 1970)

        private LocalDate(long ticks) {
            this.ticks = ticks;
        }

        public static LocalDate FromJavaTicks(long ticks) => FromDotNetTicks((ticks * TICKS_PER_MILLISECOND) + TICKS_AT_EPOCH);

        public static LocalDate FromDotNetTicks(long ticks) => FromDateTime(new DateTime(ticks, DateTimeKind.Local));

        public static LocalDate FromDateTime(DateTime dateTime) => new LocalDate((dateTime.Date.Ticks - TICKS_AT_EPOCH) / TICKS_PER_MILLISECOND);

        public static LocalDate FromComponents(int year, int month, int day) {
            var dt = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Local);
            return FromDateTime(dt);
        }

        public static LocalDate FromISO8601(string str) {
            return FromDateTime(DateTime.Parse(str, null, DateTimeStyles.RoundtripKind));
        }

        public static LocalDate Today => FromDateTime(DateTime.Today);

        public long DotNetTicks => (ticks * TICKS_PER_MILLISECOND) + TICKS_AT_EPOCH;

        public long JavaTicks => ticks;

        public DateTime ToDateTime() => new DateTime(DotNetTicks, DateTimeKind.Local);

        public bool Equals(LocalDate other) => ticks == other.ticks;

        public override bool Equals(object obj) {
            if (obj is LocalDate) {
                return Equals((LocalDate)obj);
            }
            return false;
        }

        public static bool operator ==(LocalDate lhs, LocalDate rhs) => lhs.Equals(rhs);

        public static bool operator !=(LocalDate lhs, LocalDate rhs) => !(lhs.Equals(rhs));

        public static bool operator <(LocalDate a, LocalDate b) => a.ticks < b.ticks;

        public static bool operator >(LocalDate a, LocalDate b) => a.ticks > b.ticks;

        public static bool operator <=(LocalDate a, LocalDate b) => a.ticks <= b.ticks;

        public static bool operator >=(LocalDate a, LocalDate b) => a.ticks >= b.ticks;

        public static LocalDate operator +(LocalDate t, Duration duration) => t.AddDuration(duration);

        public static LocalDateTime operator +(LocalDate t, LocalTime time) => LocalDateTime.FromJavaTicks(t.JavaTicks + time.Milliseconds);

        public static LocalDate operator -(LocalDate t, Duration duration) => t.AddDuration(duration.Negate());

        public LocalDate AddDuration(Duration duration) => FromJavaTicks(ticks + duration.TotalMilliseconds);

        public override string ToString() => ToDateTime().ToString("yyyy'-'MM'-'dd", CultureInfo.InvariantCulture);

        public override int GetHashCode() => (int)ticks;

        public int CompareTo(LocalDate other) {
            long a = ticks;
            long b = other.ticks;
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        private const long TICKS_AT_EPOCH = 621355968000000000L;
        private const long TICKS_PER_MILLISECOND = 10000;

        public void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }

        public void ReadXml(XmlReader reader) {
            ticks = FromISO8601(reader.ReadString()).JavaTicks;
            reader.Read();
        }

        public XmlSchema? GetSchema() => null;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(LocalTimeConverter))]
    public struct LocalTime : IComparable<LocalTime>, IEquatable<LocalTime>, IXmlSerializable
    {
        private int msOfDay;

        private LocalTime(int msOfDay) {
            this.msOfDay = msOfDay;
        }

        public static LocalTime FromMillisecondsOfDay(int ms) => new LocalTime(ms % MS_PerDay);

        public static LocalTime FromDotNetTicksOfDay(long ticks) => FromMillisecondsOfDay((int)(ticks / TimeSpan.TicksPerMillisecond));

        public static LocalTime FromDateTime(DateTime dateTime) => FromDotNetTicksOfDay(dateTime.TimeOfDay.Ticks);

        public static LocalTime FromComponents(int hour, int minute) => FromComponents(hour, minute, 0, 0);

        public static LocalTime FromComponents(int hour, int minute, int second) => FromComponents(hour, minute, second, 0);

        public static LocalTime FromComponents(int hour, int minute, int second, int millisecond) {
            if (hour < 0 || hour > 23) throw new ArgumentException("Argument hour must be in range [0, 23]");
            if (minute < 0 || minute > 59) throw new ArgumentException("Argument minute must be in range [0, 59]");
            if (second < 0 || second > 59) throw new ArgumentException("Argument second must be in range [0, 59]");
            if (millisecond < 0 || millisecond > 999) throw new ArgumentException("Argument millisecond must be in range [0, 999]");
            return FromMillisecondsOfDay(hour * MS_PerHour + minute * MS_PerMinute + second * MS_PerSecond + millisecond);
        }

        public static LocalTime FromISO8601(string str) {
            return FromDateTime(DateTime.Parse(str, null, DateTimeStyles.RoundtripKind));
        }

        public static LocalTime Now => FromDateTime(DateTime.Now);

        public static LocalTime UtcNow => FromDateTime(DateTime.UtcNow);

        public int Milliseconds => msOfDay;

        public LocalTime TruncateMilliseconds() => new LocalTime((msOfDay / 1000) * 1000);

        public LocalTime TruncateSeconds() => new LocalTime((msOfDay / 60000) * 60000);

        public TimeSpan ToTimeSpan() => TimeSpan.FromMilliseconds(msOfDay);

        public bool Equals(LocalTime other) => msOfDay == other.msOfDay;

        public override bool Equals(object obj) {
            if (obj is LocalTime) {
                return Equals((LocalTime)obj);
            }
            return false;
        }

        public static bool operator ==(LocalTime lhs, LocalTime rhs) => lhs.Equals(rhs);

        public static bool operator !=(LocalTime lhs, LocalTime rhs) => !(lhs.Equals(rhs));

        public static bool operator <(LocalTime a, LocalTime b) => a.msOfDay < b.msOfDay;

        public static bool operator >(LocalTime a, LocalTime b) => a.msOfDay > b.msOfDay;

        public static bool operator <=(LocalTime a, LocalTime b) => a.msOfDay <= b.msOfDay;

        public static bool operator >=(LocalTime a, LocalTime b) => a.msOfDay >= b.msOfDay;

        public static LocalTime operator +(LocalTime t, TimeSpan span) => t.AddDuration(Duration.FromTimeSpan(span));

        public static LocalTime operator -(LocalTime t, TimeSpan span) => t.AddDuration(Duration.FromTimeSpan(span).Negate());

        public static LocalTime operator +(LocalTime t, Duration duration) => t.AddDuration(duration);

        public static LocalTime operator -(LocalTime t, Duration duration) => t.AddDuration(duration.Negate());

        public LocalTime AddDuration(Duration duration) {
            long ms = duration.TotalMilliseconds % MS_PerDayL;
            return new LocalTime((int)((MS_PerDayL + msOfDay + ms) % MS_PerDayL));
        }

        public override string ToString() {
            var dt = new DateTime(msOfDay * TimeSpan.TicksPerMillisecond, DateTimeKind.Local);
            if (msOfDay % 1000 != 0) return dt.ToString("HH':'mm':'ss'.'fff", CultureInfo.InvariantCulture);
            if (msOfDay % 60000 != 0) return dt.ToString("HH':'mm':'ss", CultureInfo.InvariantCulture);
            return dt.ToString("HH':'mm", CultureInfo.InvariantCulture);
        }

        public override int GetHashCode() => msOfDay;

        public int CompareTo(LocalTime other) {
            long a = msOfDay;
            long b = other.msOfDay;
            if (a < b) return -1;
            if (a > b) return 1;
            return 0;
        }

        private const int MS_PerSecond = 1000;
        private const int MS_PerMinute = 60 * MS_PerSecond;
        private const int MS_PerHour = 60 * MS_PerMinute;
        private const int MS_PerDay = 24 * MS_PerHour;
        private const long MS_PerDayL = 24L * MS_PerHour;

        public void WriteXml(XmlWriter writer) {
            writer.WriteString(ToString());
        }

        public void ReadXml(XmlReader reader) {
            msOfDay = FromISO8601(reader.ReadString()).Milliseconds;
            reader.Read();
        }

        public XmlSchema? GetSchema() => null;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    [JsonConverter(typeof(ObjectRefConverter))]
    public struct ObjectRef : IEquatable<ObjectRef>, IXmlSerializable
    {
        private string moduleID;
        private string localObjectID;

        public string ModuleID => moduleID ?? "";

        public string LocalObjectID => localObjectID ?? "";

        public static ObjectRef Make(string moduleID, string localObjectID) {
            return new ObjectRef(moduleID, localObjectID);
        }

        public static ObjectRef FromEncodedString(string encodedObjectRef) {
            int i = encodedObjectRef.IndexOf(':');
            if (i < 1) throw new ArgumentException("Separator not found in: " + encodedObjectRef, nameof(encodedObjectRef));
            string m = encodedObjectRef.Substring(0, i);
            string o = encodedObjectRef.Substring(i + 1);
            return new ObjectRef(m, o);
        }

        private ObjectRef(string moduleID, string localObjectID) {
            this.moduleID = moduleID ?? throw new ArgumentNullException(nameof(moduleID), nameof(moduleID) + " may not be null!");
            this.localObjectID = localObjectID ?? throw new ArgumentNullException(nameof(localObjectID), nameof(localObjectID) + " may not be null!");
        }

        public bool Equals(ObjectRef other) => LocalObjectID == other.LocalObjectID && ModuleID == other.ModuleID;

        public override bool Equals(object obj) {
            if (obj is ObjectRef) {
                return Equals((ObjectRef)obj);
            }
            return false;
        }

        public static bool operator ==(ObjectRef lhs, ObjectRef rhs) => lhs.Equals(rhs);

        public static bool operator !=(ObjectRef lhs, ObjectRef rhs) => !(lhs.Equals(rhs));

        public string ToEncodedString() {
            string m = ModuleID;
            string o = LocalObjectID;
            if (m.Length == 0 && o.Length == 0) return "";
            return m + ":" + o;
        }

        public override string ToString() => ToEncodedString();

        public override int GetHashCode() => ModuleID.GetHashCode() * 31 + LocalObjectID.GetHashCode();

        public void WriteXml(XmlWriter writer) {
            writer.WriteString(ToEncodedString());
        }

        public void ReadXml(XmlReader reader) {
            ObjectRef x = FromEncodedString(reader.ReadString());
            moduleID = x.moduleID;
            localObjectID = x.localObjectID;
            reader.Read();
        }

        public XmlSchema? GetSchema() => null;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public struct NamedValue : IEquatable<NamedValue>, IXmlSerializable
    {
        private string name;
        private string value;

        [XmlAttribute("name")]
        public string Name {
            get
            {
                return this.name ?? "";
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                this.name = value;
            }
        }

        [XmlAttribute("value")]
        public string Value
        {
            get
            {
                return this.value ?? "";
            }
            set
            {
                if (value == null) throw new ArgumentNullException("value");
                this.value = value;
            }
        }

        public XmlSchema? GetSchema() => null;

        public void ReadXml(XmlReader reader) {
            name = reader["name"];
            value = reader["value"];
            bool isEmpty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (!isEmpty) {
                string v = (reader.ReadContentAsString() ?? "");
                value = v.Trim();
                reader.ReadEndElement();
            }
        }

        public void WriteXml(XmlWriter writer) {
            writer.WriteAttributeString("name", Name);
            string v = Value;
            bool newLines = v.Contains('\n') || v.Contains('\r');
            if (newLines) {
                writer.WriteString(v);
            }
            else {
                writer.WriteAttributeString("value", v);
            }
        }

        public NamedValue(string name, string value) {
            if (name == null) throw new ArgumentNullException("name");
            if (value == null) throw new ArgumentNullException("value");
            this.name = name;
            this.value = value;
        }

        public NamedValue WithValue(string value) {
            if (value == null) throw new ArgumentNullException("value");
            return new NamedValue(Name, value);
        }

        public bool Equals(NamedValue other) => Name == other.Name && Value == other.Value;

        public override bool Equals(object obj) {
            if (obj is NamedValue) {
                return Equals((NamedValue)obj);
            }
            return false;
        }

        public static bool operator ==(NamedValue lhs, NamedValue rhs) => lhs.Equals(rhs);

        public static bool operator !=(NamedValue lhs, NamedValue rhs) => !(lhs.Equals(rhs));

        public override string ToString() => Name + "=" + Value;

        public override int GetHashCode() => Name.GetHashCode() ^ Value.GetHashCode();
    }

    public struct Result : IEquatable<Result> {

        private Result(bool ok, string? error) {
            IsOK = ok;
            Error = error;
        }

        public static Result OK => new Result(true, null);

        public static Result Failure(string errMsg) => new Result(false, errMsg ?? "?");

        public static Result FromResults(IEnumerable<Result> results) {
            if (results.All(r => r.IsOK)) return Result.OK;
            return Result.Failure(string.Join("; ", results.Where(r => r.Failed()).Select(r => r.Error ?? "?")));
        }

        public bool IsOK { get; set; }
        public string? Error { get; set; } // only used when IsOK == false

        public readonly bool Failed() => !IsOK;

        public readonly bool Equals(Result other) => IsOK == other.IsOK && Error == other.Error;

        public override readonly bool Equals(object obj) {
            if (obj is Result result) {
                return Equals(result);
            }
            return false;
        }

        public static bool operator ==(Result lhs, Result rhs) => lhs.Equals(rhs);

        public static bool operator !=(Result lhs, Result rhs) => !(lhs.Equals(rhs));

        public override readonly string ToString() => IsOK ? "OK" : "Failed: " + Error;

        public override readonly int GetHashCode() => ToString().GetHashCode();
    }

    public struct Result<T> : IEquatable<Result<T>>
    {
        private Result(bool ok, T? value, string? error) {
            IsOK = ok;
            Value = value;
            Error = error;
        }

        public static Result<T> OK(T value) => new Result<T>(true, value, null);

        public static Result<T> Failure(string errMsg) => new Result<T>(false, default, errMsg ?? "?");

        public bool IsOK { get; set; }
        public string? Error { get; set; } // only used when IsOK == false
        public T? Value { get; set; }      // only used when IsOK == true

        public bool Failed() => !IsOK;

        public bool Equals(Result<T> other) => IsOK == other.IsOK && ((Failed() && Error == other.Error) || (IsOK && Value!.Equals(other.Value)));

        public override bool Equals(object obj) {
            if (obj is Result<T>) {
                return Equals((Result<T>)obj);
            }
            return false;
        }

        public static bool operator ==(Result<T> lhs, Result<T> rhs) => lhs.Equals(rhs);

        public static bool operator !=(Result<T> lhs, Result<T> rhs) => !(lhs.Equals(rhs));

        public override string ToString() => IsOK ? "" + Value : "Failed: " + Error;

        public override int GetHashCode() => ToString().GetHashCode();
    }

    [JsonConverter(typeof(TimeseriesEntryConverter))]
    public struct TimeseriesEntry : IEquatable<TimeseriesEntry>, IComparable {

        public Timestamp Time { get; set; }
        public DataValue Value { get; set; }

        public TimeseriesEntry(Timestamp time, double value) {
            this.Time = time;
            this.Value = DataValue.FromDouble(value);
        }

        public TimeseriesEntry(Timestamp time, double? value) {
            this.Time = time;
            this.Value = value.HasValue ? DataValue.FromDouble(value.Value) : DataValue.Empty;
        }

        public TimeseriesEntry(Timestamp time, DataValue value) {
            this.Time = time;
            this.Value = value;
        }

        public readonly bool Equals(TimeseriesEntry other) {
            return Time == other.Time && Value == other.Value;
        }

        public static bool operator ==(TimeseriesEntry lhs, TimeseriesEntry rhs) => lhs.Equals(rhs);

        public static bool operator !=(TimeseriesEntry lhs, TimeseriesEntry rhs) => !(lhs.Equals(rhs));

        public override readonly int GetHashCode() => Time.GetHashCode() ^ Value.GetHashCode();

        public override readonly string ToString() => Time + "=" + Value;

        public override readonly bool Equals(object obj) {
            if (obj is TimeseriesEntry te) {
                return Equals(te);
            }
            return false;
        }

        public readonly int CompareTo(object obj) {
            return Time.CompareTo(((TimeseriesEntry)obj).Time);
        }
    }
}
