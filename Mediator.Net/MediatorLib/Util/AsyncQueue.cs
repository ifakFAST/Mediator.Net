﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Util
{
    public class AsyncQueue<T> {

        private readonly Queue<T> buffer = new Queue<T>();
        private readonly object sync = new object();

        private TaskCompletionSource<T>? promise = null;

        public void Post(T item) {
            lock (sync) {
                if (promise != null) {
                    var p = promise;
                    promise = null;
                    p.TrySetResult(item);
                }
                else {
                    buffer.Enqueue(item);
                }
            }
        }

        public void Post(T item, Func<T, bool> skipPost) {
            lock (sync) {
                if (promise != null) {
                    var p = promise;
                    promise = null;
                    p.TrySetResult(item);
                }
                else {

                    foreach (T it in buffer) {
                        bool skip = skipPost(it);
                        if (skip) { return; }
                    }

                    buffer.Enqueue(item);
                }
            }
        }

        public Task<T> ReceiveAsync() {
            lock (sync) {
                if (buffer.Count > 0) {
                    return Task.FromResult(buffer.Dequeue());
                }
                else {
                    promise = new TaskCompletionSource<T>();
                    return promise.Task;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (sync) {
                    return buffer.Count;
                }
            }
        }
    }
}
