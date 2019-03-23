// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Util
{
    public class ReadOnlySet<T> : IReadOnlyCollection<T>, ISet<T>
    {
        private readonly ISet<T> theSet;
        private readonly List<T> list;

        public ReadOnlySet(params T[] items) {
            theSet = new HashSet<T>(items);
            list = new List<T>(items);
        }

        public ReadOnlySet(params IEnumerable<T>[] items) {
            theSet = new HashSet<T>();
            list = new List<T>();
            foreach (var it in items) {
                theSet.UnionWith(it);
                list.AddRange(it);
            }
        }

        public ReadOnlySet(IEnumerable<T> collection) {
            theSet = new HashSet<T>(collection);
            list = new List<T>(collection);
        }

        public IEnumerator<T> GetEnumerator() {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)list).GetEnumerator();
        }

        void ICollection<T>.Add(T item) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public void UnionWith(IEnumerable<T> other) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public void IntersectWith(IEnumerable<T> other) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public void ExceptWith(IEnumerable<T> other) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public void SymmetricExceptWith(IEnumerable<T> other) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public bool IsSubsetOf(IEnumerable<T> other) {
            return theSet.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other) {
            return theSet.IsSupersetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other) {
            return theSet.IsProperSupersetOf(other);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other) {
            return theSet.IsProperSubsetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other) {
            return theSet.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other) {
            return theSet.SetEquals(other);
        }

        public bool Add(T item) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public void Clear() {
            throw new NotSupportedException("Set is a read only set.");
        }

        public bool Contains(T item) {
            return theSet.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            list.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item) {
            throw new NotSupportedException("Set is a read only set.");
        }

        public int Count
        {
            get { return list.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }
    }
}
