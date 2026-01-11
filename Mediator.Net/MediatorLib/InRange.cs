// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Ifak.Fast.Mediator {

    public static class InRangeExtension {

        public static int InRange(this int v, int min, int max) {
            if (v < min) { return min; }
            if (v > max) { return max; }
            return v;
        }

        public static long InRange(this long v, long min, long max) {
            if (v < min) { return min; }
            if (v > max) { return max; }
            return v;
        }
    }
}