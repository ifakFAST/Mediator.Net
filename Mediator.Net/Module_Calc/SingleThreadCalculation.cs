// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc
{
    public class SingleThreadCalculation : CalculationBase
    {
        private readonly CalculationBase adapter;
        private readonly AsyncQueue<WorkItem> queue = new AsyncQueue<WorkItem>();
        private bool isStarted = false;

        public SingleThreadCalculation(CalculationBase wrapped) {
            this.adapter = wrapped;
        }

        private void CheckStarted() {
            if (!isStarted) {
                isStarted = true;
                Thread thread = new Thread(TheThread);
                thread.IsBackground = true;
                thread.Start();
            }
        }

        private void TheThread() {
            try {
                SingleThreadedAsync.Run(() => Runner());
            }
            catch (Exception exp) {
                Console.Error.WriteLine("SingleThreadCalculation: " + exp.Message);
            }
        }

        public override Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) {
            CheckStarted();
            var promise = new TaskCompletionSource<InitResult>();
            queue.Post(new WorkItem(MethodID.Init, promise, parameter, callback));
            return promise.Task;
        }

        public override Task<StepResult> Step(Timestamp t, InputValue[] inputValues) {
            if (!isStarted) throw new Exception("Step requires prior Initialize!");
            var promise = new TaskCompletionSource<StepResult>();
            queue.Post(new WorkItem(MethodID.Step, promise, t, inputValues));
            return promise.Task;
        }

        public override Task Shutdown() {
            if (isStarted) {
                var promise = new TaskCompletionSource<bool>();
                queue.Post(new WorkItem(MethodID.Shutdown, promise));
                return promise.Task;
            }
            else {
                return Task.FromResult(true);
            }
        }

        private async Task Runner() {

            while (true) {

                WorkItem it = await queue.ReceiveAsync();

                switch (it.Methode) {

                    case MethodID.Init: {

                            var promise = (TaskCompletionSource<InitResult>)it.Promise;
                            try {
                                InitResult res = await adapter.Initialize((InitParameter)it.Param1!, (AdapterCallback)it.Param2!);
                                promise.SetResult(res);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.Step: {

                            var promise = (TaskCompletionSource<StepResult>)it.Promise;
                            try {
                                var result = await adapter.Step((Timestamp)it.Param1!, (InputValue[])it.Param2!);
                                promise.SetResult(result);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.Shutdown: {
                            var promise = (TaskCompletionSource<bool>)it.Promise;
                            try {
                                await adapter.Shutdown();
                                promise.SetResult(true);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            return; // Exit loop
                        }
                }
            } // while
        }

        private class WorkItem
        {
            public WorkItem(MethodID methode, object promise, object? param1 = null, object? param2 = null, object? param3 = null) {
                Methode = methode;
                Promise = promise;
                Param1 = param1;
                Param2 = param2;
                Param3 = param3;
            }

            public MethodID Methode { get; set; }
            public object Promise { get; set; }
            public object? Param1 { get; set; }
            public object? Param2 { get; set; }
            public object? Param3 { get; set; }
        }

        private enum MethodID
        {
            Init, Step, GetIOs, Shutdown
        }
    }
}
