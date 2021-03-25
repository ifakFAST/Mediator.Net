using System;
using System.Collections.Generic;
using System.Text;

namespace Ifak.Fast.Mediator.Util
{
    public static class VersionInfo
    {
        public static string ifakFAST_Str() {
            Version v = ifakFAST();
            if (v == null) return "0";
            return v.ToString(fieldCount: 3).Trim();
        }

        public static Version ifakFAST() {
            return typeof(Timestamp).Assembly.GetName().Version;
        }
    }
}
