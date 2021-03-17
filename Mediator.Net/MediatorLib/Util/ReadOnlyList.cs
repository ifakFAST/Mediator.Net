using System.Collections;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Util
{
    public class ReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly List<T> list;

        public ReadOnlyList(params T[] items) {
            list = new List<T>(items);
        }

        public ReadOnlyList(params IEnumerable<T>[] items) {
            list = new List<T>();
            foreach (var it in items) {
                list.AddRange(it);
            }
        }

        public ReadOnlyList(IEnumerable<T> collection) {
            list = new List<T>(collection);
        }

        public IEnumerator<T> GetEnumerator() {
            return list.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable)list).GetEnumerator();
        }

        public bool Contains(T item) {
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex) {
            list.CopyTo(array, arrayIndex);
        }

        public int Count {
            get { return list.Count; }
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public T this[int index] => list[index];
    }
}
