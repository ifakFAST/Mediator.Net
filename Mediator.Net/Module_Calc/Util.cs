// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Calc;

public static class Utils
{
    public static void RemoveAll<T>(this List<T> list, IEnumerable<T> removeItems) {
        foreach (T item in removeItems) {
            list.Remove(item);
        }
    }

    public static long InRange(this long v, long min, long max) {
        if (v < min) return min;
        if (v > max) return max;
        return v;
    }

    public static int FindIndex<T>(this IEnumerable<T> en, Predicate<T> predicate) {
        int i = 0;
        foreach (var item in en) {
            if (predicate(item)) {
                return i;
            }
            i += 1;
        }
        return -1;
    }
}
