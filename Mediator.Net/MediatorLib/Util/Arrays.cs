// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;

namespace Ifak.Fast.Mediator.Util
{
    public static class Arrays
    {
        public static bool Equals<T>(T[] a, T[] b) where T : IEquatable<T> {

            if (a == null && b == null) return true;
            if (a == null || b == null) return false;

            IStructuralEquatable equ = a;
            return equ.Equals(b, StructuralComparisons.StructuralEqualityComparer);
        }
    }
}
