// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Ifak.Fast.Mediator
{
    public interface IVTQ
    {
        DataValue V { get; set; }
        Timestamp T { get; set; }
        Quality Q { get; set; }
    }

    public struct VTQ : IVTQ, IComparable<VTQ>, IEquatable<VTQ>
    {
        public DataValue V { get; set; }
        public Timestamp T { get; set; }
        public Quality   Q { get; set; }

        public VTQ(Timestamp time, Quality quality, DataValue value) {
            T = time;
            Q = quality;
            V = value;
        }

        public static VTQ Make(VTTQ vttq) => new VTQ(vttq.T, vttq.Q, vttq.V);

        public static VTQ Make(DataValue value, Timestamp time, Quality quality) => new VTQ(time, quality, value);

        public static VTQ Make(double value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromDouble(value));

        public static VTQ Make(float value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromFloat(value));

        public static VTQ Make(int value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromInt(value));

        public static VTQ Make(long value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromLong(value));

        public static VTQ Make(bool value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromBool(value));

        public static VTQ Make(string value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromString(value));

        public static VTQ Make(Duration value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromDuration(value));

        public static VTQ Make(LocalDateTime value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromLocalDateTime(value));

        public static VTQ Make(LocalDate value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromLocalDate(value));

        public static VTQ Make(LocalTime value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromLocalTime(value));

        public static VTQ Make(Timestamp value, Timestamp time, Quality quality) => new VTQ(time, quality, DataValue.FromTimestamp(value));

        public override string ToString() => $"{T} {Q} {V}";

        public override int GetHashCode() => (int)T.JavaTicks;

        public int CompareTo(VTQ other) => T.CompareTo(other.T);

        public override bool Equals(object obj) {
            if (obj is VTQ) {
                return Equals((VTQ)obj);
            }
            return false;
        }

        public VTQ WithQuality(Quality quality) => new VTQ(T, quality, V);

        public VTQ WithValue(DataValue value) => new VTQ(T, Q, value);

        public VTQ WithTime(Timestamp time) => new VTQ(time, Q, V);

        public bool Equals(VTQ other) => T == other.T && V == other.V && Q == other.Q;

        public static bool operator ==(VTQ lhs, VTQ rhs) => lhs.Equals(rhs);

        public static bool operator !=(VTQ lhs, VTQ rhs) => !(lhs.Equals(rhs));
    }


    public struct VTTQ : IVTQ, IComparable<VTTQ>, IEquatable<VTTQ>
    {
        public DataValue V { get; set; }
        public Timestamp T { get; set; }
        public Timestamp T_DB { get; set; }
        public Quality Q { get; set; }

        public VTTQ(Timestamp time, Timestamp timeDB, Quality quality, DataValue value) {
            T = time;
            T_DB = timeDB;
            Q = quality;
            V = value;
        }

        public static VTTQ Make(DataValue value, Timestamp time, Timestamp timeDB, Quality quality) => new VTTQ(time, timeDB, quality, value);

        public override string ToString() => $"{T} {Q} {V}";

        public override int GetHashCode() => (int)T.JavaTicks;

        public int CompareTo(VTTQ other) => T.CompareTo(other.T);

        public override bool Equals(object obj) {
            if (obj is VTTQ) {
                return Equals((VTTQ)obj);
            }
            return false;
        }

        public VTQ ToVTQ() => VTQ.Make(this);

        public bool Equals(VTTQ other) => T == other.T && T_DB == other.T_DB && V == other.V && Q == other.Q;

        public static bool operator ==(VTTQ lhs, VTTQ rhs) => lhs.Equals(rhs);

        public static bool operator !=(VTTQ lhs, VTTQ rhs) => !(lhs.Equals(rhs));
    }

    public enum Quality {
        Bad = 0,
        Good = 1,
        Uncertain = 2,
    }
}
