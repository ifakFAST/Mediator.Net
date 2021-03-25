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

    public class ReadManager<REF, RES>
    {
        private List<REF> refs;
        private int[] mapIdx;
        private IList<ReadRequest> readRequests;
        public VTQ[] values;

        public ReadManager(IList<ReadRequest> readRequests, Func<ReadRequest, REF> f) {
            int N = readRequests.Count;
            this.readRequests = readRequests;
            this.values = new VTQ[N];
            this.refs = new List<REF>(N);
            this.mapIdx = new int[N];
            for (int i = 0; i < N; ++i) {
                ReadRequest request = readRequests[i];
                try {
                    REF refItem = f(request);
                    mapIdx[refs.Count] = i;
                    refs.Add(refItem);
                }
                catch (Exception) {
                    values[i] = VTQ.Make(request.LastValue.V, Timestamp.Now, Quality.Bad);
                }
            }
        }

        public REF[] GetRefs() => refs.ToArray();

        public List<REF> GetRefsList() => refs;

        public ReadRequest GetReadRequest(int i) {
            int k = MapIdx(i);
            return readRequests[k];
        }

        public void SetAllResults(IList<RES> results, Func<RES, ReadRequest, VTQ> f) {
            for (int i = 0; i < results.Count; ++i) {
                RES res = results[i];
                int k = MapIdx(i);
                values[k] = f(res, readRequests[k]);
            }
        }

        public void SetSingleResult(int i, VTQ v) {
            int k = MapIdx(i);
            values[k] = v;
        }

        private int MapIdx(int i) => mapIdx[i];
    }

    public class WriteManager<REF, RES>
    {
        private List<REF> refs;
        private int[] mapIdx;
        private IList<DataItemValue> writeRequests;
        public List<FailedDataItemWrite> failures;

        public WriteManager(IList<DataItemValue> writeRequests, Func<DataItemValue, REF> f) {
            int N = writeRequests.Count;
            this.writeRequests = writeRequests;
            this.failures = new List<FailedDataItemWrite>();
            this.refs = new List<REF>(N);
            this.mapIdx = new int[N];
            for (int i = 0; i < N; ++i) {
                DataItemValue request = writeRequests[i];
                try {
                    REF refItem = f(request);
                    mapIdx[refs.Count] = i;
                    refs.Add(refItem);
                }
                catch (Exception exp) {
                    failures.Add(new FailedDataItemWrite(request.ID, exp.Message));
                }
            }
        }

        public REF[] GetRefs() => refs.ToArray();

        public List<REF> GetRefsList() => refs;

        public DataItemValue GetWriteRequest(int i) {
            int k = MapIdx(i);
            return writeRequests[k];
        }

        public void AddWriteErrors(RES[] results, Func<RES, FailedDataItemWrite> f) {
            for (int i = 0; i < results.Length; ++i) {
                RES res = results[i];
                failures.Add(f(res));
            }
        }

        public void AddSingleWriteError(FailedDataItemWrite v) {
            failures.Add(v);
        }

        private int MapIdx(int i) => mapIdx[i];

        public WriteDataItemsResult GetWriteResult() {
            if (failures.Count == 0) {
                return WriteDataItemsResult.OK;
            }
            else {
                return WriteDataItemsResult.Failure(failures.ToArray());
            }
        }
    }
}
