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
}
