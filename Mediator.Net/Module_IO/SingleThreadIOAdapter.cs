// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO
{
    public class SingleThreadIOAdapter : AdapterBase
    {
        private readonly AdapterBase adapter;
        private readonly AsyncQueue<WorkItem> queue = new AsyncQueue<WorkItem>();
        private bool isStarted = false;

        public override bool SupportsScheduledReading => adapter.SupportsScheduledReading;

        public SingleThreadIOAdapter(AdapterBase wrapped) {
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
                Console.Error.WriteLine("SingleThreadIOAdapter: " + exp.Message);
            }
        }

        public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {
            CheckStarted();
            var promise = new TaskCompletionSource<Group[]>();
            queue.Post(new WorkItem(MethodID.Init, promise, config, callback, itemInfos));
            return promise.Task;
        }

        public override void StartRunning() {
            if (!isStarted) throw new Exception("StartRunning requires prior Initialize!");
            queue.Post(new WorkItem(MethodID.StartRunning, null));
        }

        public override Task<VTQ[]> ReadDataItems(string groupID, IList<ReadRequest> items, Duration? timeout) {
            if (!isStarted) throw new Exception("ReadDataItems requires prior Initialize!");
            var promise = new TaskCompletionSource<VTQ[]>();
            queue.Post(new WorkItem(MethodID.ReadDataItems, promise, groupID, items, timeout));
            return promise.Task;
        }

        public override Task<WriteDataItemsResult> WriteDataItems(string groupID, IList<DataItemValue> values, Duration? timeout) {
            if (!isStarted) throw new Exception("WriteDataItems requires prior Initialize!");
            var promise = new TaskCompletionSource<WriteDataItemsResult>();
            queue.Post(new WorkItem(MethodID.WriteDataItems, promise, groupID, values, timeout));
            return promise.Task;
        }

        public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
            if (!isStarted) throw new Exception("BrowseDataItemAddress requires prior Initialize!");
            var promise = new TaskCompletionSource<string[]>();
            queue.Post(new WorkItem(MethodID.BrowseDataItemAddress, promise, idOrNull));
            return promise.Task;
        }

        public override Task<string[]> BrowseAdapterAddress() {
            if (!isStarted) throw new Exception("BrowseAdapterAddress requires prior Initialize!");
            var promise = new TaskCompletionSource<string[]>();
            queue.Post(new WorkItem(MethodID.BrowseAdapterAddress, promise));
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

                            var promise = (TaskCompletionSource<Group[]>)it.Promise!;
                            try {
                                var groups = await adapter.Initialize((Adapter)it.Param1!, (AdapterCallback)it.Param2!, (DataItemInfo[])it.Param3!);
                                promise.SetResult(groups);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.StartRunning: {

                            try {
                                adapter.StartRunning();
                            }
                            catch (Exception exp) {
                                Console.Error.WriteLine("Exception in adapter.StartRunning: " + exp.Message);
                            }
                            break;
                        }

                    case MethodID.ReadDataItems: {

                            var promise = (TaskCompletionSource<VTQ[]>)it.Promise!;
                            try {
                                var values = await adapter.ReadDataItems((string)it.Param1!, (IList<ReadRequest>)it.Param2!, (Duration?)it.Param3!);
                                promise.SetResult(values);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.WriteDataItems: {

                            var promise = (TaskCompletionSource<WriteDataItemsResult>)it.Promise!;
                            try {
                                WriteDataItemsResult res = await adapter.WriteDataItems((string)it.Param1!, (IList<DataItemValue>)it.Param2!, (Duration?)it.Param3!);
                                promise.SetResult(res);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.BrowseDataItemAddress: {

                            var promise = (TaskCompletionSource<string[]>)it.Promise!;
                            try {
                                string[] res = await adapter.BrowseDataItemAddress((string)it.Param1!);
                                promise.SetResult(res);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.BrowseAdapterAddress: {

                            var promise = (TaskCompletionSource<string[]>)it.Promise!;
                            try {
                                string[] res = await adapter.BrowseAdapterAddress();
                                promise.SetResult(res);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.Shutdown: {
                            var promise = (TaskCompletionSource<bool>)it.Promise!;
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
            public WorkItem(MethodID methode, object? promise, object? param1 = null, object? param2 = null, object? param3 = null) {
                Methode = methode;
                Promise = promise;
                Param1 = param1;
                Param2 = param2;
                Param3 = param3;
            }

            public MethodID Methode { get; set; }
            public object? Promise { get; set; }
            public object? Param1 { get; set; }
            public object? Param2 { get; set; }
            public object? Param3 { get; set; }
        }

        enum MethodID
        {
            Init, StartRunning, ReadDataItems, WriteDataItems, Shutdown, BrowseDataItemAddress, BrowseAdapterAddress
        }
    }
}
