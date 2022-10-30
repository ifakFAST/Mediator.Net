using System;
using System.Diagnostics;
using System.Globalization;

namespace Ifak.Fast.Mediator.Util {
    
    public static class StrFormatters {

        public static string ElapsedString(this TimeSpan span) {
            double ms = span.Ticks / (double)TimeSpan.TicksPerMillisecond;
            if (ms < 1000.0) {
                return ms.ToString("F2", CultureInfo.InvariantCulture) + " ms";
            }
            double seconds = ms / 1000.0;
            return seconds.ToString("F2", CultureInfo.InvariantCulture) + " s";
        }
    }
}
