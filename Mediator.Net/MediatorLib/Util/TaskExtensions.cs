// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Util
{
    public static class TaskExtensions
    {
        public static Task ContinueOnMainThread<TResult>(this Task<TResult> t, Action<Task<TResult>> continuationAction) {
            SynchronizationContext synContext = SynchronizationContext.Current;
            if (synContext != null) {
                TaskScheduler mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                return t.ContinueWith(continuationAction, mainThreadScheduler);
            }
            else {
                Console.Error.WriteLine("ContinueOnMainThread without SynchronizationContext");
                return t.ContinueWith(continuationAction);
            }
        }

        public static Task ContinueOnMainThread(this Task t, Action<Task> continuationAction) {
            SynchronizationContext synContext = SynchronizationContext.Current;
            if (synContext != null) {
                TaskScheduler mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                return t.ContinueWith(continuationAction, mainThreadScheduler);
            }
            else {
                Console.Error.WriteLine("ContinueOnMainThread without SynchronizationContext");
                return t.ContinueWith(continuationAction);
            }
        }

        public static Task<TNewResult> ContinueOnMainThread<TResult, TNewResult>(this Task<TResult> t, Func<Task<TResult>, TNewResult> continuationFunction) {
            SynchronizationContext synContext = SynchronizationContext.Current;
            if (synContext != null) {
                TaskScheduler mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
                return t.ContinueWith(continuationFunction, mainThreadScheduler);
            }
            else {
                Console.Error.WriteLine("ContinueOnMainThread without SynchronizationContext");
                return t.ContinueWith(continuationFunction);
            }
        }
    }
}
