using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Util
{
    public static class Time
    {
        public static Timestamp GetNextNormalizedTimestamp(Duration cycle, Duration offset) {
            return GetNextNormalizedTimestamp(Timestamp.Now, cycle, offset);
        }

        public static Timestamp GetNextNormalizedTimestamp(Timestamp tStartExcluding, Duration cycle, Duration offset) {
            long cycleTicks = cycle.TotalMilliseconds;
            long offsetTicks = offset.TotalMilliseconds;
            long nowTicks = tStartExcluding.JavaTicks - offsetTicks;
            long tLast = nowTicks - (nowTicks % cycleTicks);
            long tNext = tLast + cycleTicks + offsetTicks;
            return Timestamp.FromJavaTicks(tNext);
        }

        public static bool IsNormalizedTimestamp(Timestamp t, Duration cycle, Duration offset) {
            long cycleTicks = cycle.TotalMilliseconds;
            long offsetTicks = offset.TotalMilliseconds;
            long tTicks = t.JavaTicks - offsetTicks;
            return (tTicks % cycleTicks) == 0;
        }

        public static async Task WaitUntil(Timestamp t, Func<bool> abort) {
            while (!abort() && Timestamp.Now <= t) {
                Duration sleepFull = t - Timestamp.Now;
                long sleepMS = InRange(sleepFull.TotalMilliseconds, min: 1, max: 500);
                await Task.Delay((int)sleepMS);
            }
        }

        public static Task WaitSeconds(int secondsWait, Func<bool> abort) {
            return Time.WaitUntil(Timestamp.Now + Duration.FromSeconds(secondsWait), abort);
        }

        private static long InRange(long v, long min, long max) {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
