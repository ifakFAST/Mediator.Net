using System;
using System.Collections.Generic;
using System.Text;

namespace MediatorLib_Test.Serialization
{
    public static class Util
    {
        public static string FormatDuration(string prefix, long totalTicksSeri, int repeat) {
            double ms = ((double)totalTicksSeri / (repeat - 1)) / TimeSpan.TicksPerMillisecond;
            return $"{prefix}: {ms:0.###} ms";
        }
    }
}
