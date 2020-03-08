// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.IO
{
    public static class ListExtension
    {
        public static void RemoveAll<T>(this List<T> list, IEnumerable<T> removeItems) {
            foreach (T item in removeItems) {
                list.Remove(item);
            }
        }
    }

    public static class EnumerableExtension
    {
        public static IEnumerable<U> SelectIgnoreException<T, U>(this IEnumerable<T> list, Func<T, U> f) {
            U u;
            foreach (var item in list) {
                try {
                    u = f(item);
                }
                catch (Exception) {
                    continue;
                }
                yield return u;
            }
        }

        public static int FindIndexOrThrow<T>(this IEnumerable<T> list, Predicate<T> f) {
            int i = 0;
            foreach (var item in list) {
                if (f(item)) return i;
                i++;
            }
            throw new Exception("Element not found");
        }
    }
}
