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
