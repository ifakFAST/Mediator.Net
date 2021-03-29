// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using NLog;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    public class SingleThreadModule : ModuleBase
    {
        private static Logger logger = LogManager.GetLogger("SingleThreadModule");

        private readonly ModuleBase module;
        private readonly string moduleName;
        private readonly AsyncQueue<WorkItem> queue = new AsyncQueue<WorkItem>();
        private bool isStarted = false;

        public SingleThreadModule(ModuleBase wrapped, string moduleName) {
            this.module = wrapped;
            this.moduleName = moduleName;
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
                hasTerminated = true;
                logger.Warn($"Runner failed for module {moduleName}: " + exp.Message);
                if (runPromise != null) {
                    runPromise.TrySetException(exp);
                }
                else if (initPromise != null) {
                    initPromise.TrySetException(exp);
                }
            }
        }

        private TaskCompletionSource<bool>? initPromise = null;

        public override Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread? moduleThread) {
            // moduleThread is null here will be set later!
            CheckStarted();
            var promise = new TaskCompletionSource<bool>();
            initPromise = promise;
            queue.Post(new WorkItem(MethodID.Init, promise, info, restoreVariableValues, notifier));
            return promise.Task;
        }

        public override Task InitAbort() {
            if (!isStarted) throw new Exception("InitAbort requires prior Init!");
            var promise = new TaskCompletionSource<bool>();
            queue.Post(new WorkItem(MethodID.InitAbort, promise));
            return promise.Task;
        }

        private TaskCompletionSource<bool>? runPromise = null;

        public override Task Run(Func<bool> shutdown) {
            if (!isStarted) throw new Exception("Run requires prior Init!");
            var promise = new TaskCompletionSource<bool>();
            runPromise = promise;
            initPromise = null;
            queue.Post(new WorkItem(MethodID.Run, promise, shutdown));
            return promise.Task;
        }

        public override Task<ObjectInfo[]> GetAllObjects() {
            if (!isStarted) throw new Exception("GetAllObjects requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<ObjectInfo[]>();
            queue.Post(new WorkItem(MethodID.GetAllObjects, promise));
            return promise.Task;
        }

        public override Task<MemberValue[]> GetMemberValues(MemberRef[] member) {
            if (!isStarted) throw new Exception("GetMemberValues requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<MemberValue[]>();
            queue.Post(new WorkItem(MethodID.GetMemberValues, promise, member));
            return promise.Task;
        }

        public override Task<MetaInfos> GetMetaInfo() {
            if (!isStarted) throw new Exception("GetMetaInfo requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<MetaInfos>();
            queue.Post(new WorkItem(MethodID.GetMetaInfo, promise));
            return promise.Task;
        }

        public override Task<ObjectInfo[]> GetObjectsByID(ObjectRef[] ids) {
            if (!isStarted) throw new Exception("GetObjectsByID requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<ObjectInfo[]>();
            queue.Post(new WorkItem(MethodID.GetObjectsByID, promise, ids));
            return promise.Task;
        }

        public override Task<ObjectValue[]> GetObjectValuesByID(ObjectRef[] objectIDs) {
            if (!isStarted) throw new Exception("GetObjectValuesByID requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<ObjectValue[]>();
            queue.Post(new WorkItem(MethodID.GetObjectValuesByID, promise, objectIDs));
            return promise.Task;
        }

        public override Task<Result> UpdateConfig(Origin origin, ObjectValue[]? updateOrDeleteObjects, MemberValue[]? updateOrDeleteMembers, AddArrayElement[]? addArrayElements) {
            if (!isStarted) throw new Exception("UpdateConfig requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<Result>();
            queue.Post(new WorkItem(MethodID.UpdateConfig, promise, origin, updateOrDeleteObjects, updateOrDeleteMembers, addArrayElements));
            return promise.Task;
        }

        public override Task<VTQ[]> ReadVariables(Origin origin, VariableRef[] variables, Duration? timeout) {
            if (!isStarted) throw new Exception("ReadVariables requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<VTQ[]>();
            queue.Post(new WorkItem(MethodID.ReadVariables, promise, origin, variables, timeout));
            return promise.Task;
        }

        public override Task<WriteResult> WriteVariables(Origin origin, VariableValue[] values, Duration? timeout) {
            if (!isStarted) throw new Exception("WriteVariables requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<WriteResult>();
            queue.Post(new WorkItem(MethodID.WriteVariables, promise, origin, values, timeout));
            return promise.Task;
        }

        public override Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {
            if (!isStarted) throw new Exception("OnMethodCall requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<Result<DataValue>>();
            queue.Post(new WorkItem(MethodID.OnMethodCall, promise, origin, methodName, parameters));
            return promise.Task;
        }

        public override Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {
            if (!isStarted) throw new Exception("BrowseObjectMemberValues requires prior Init!");
            if (hasTerminated) throw new Exception("Module terminated.");
            var promise = new TaskCompletionSource<BrowseResult>();
            queue.Post(new WorkItem(MethodID.Browse, promise, member, continueID));
            return promise.Task;
        }

        private volatile bool hasTerminated = false;

        private async Task Runner() {

            var moduleThread = new TheModuleThread();

            while (!hasTerminated || queue.Count > 0) {

                WorkItem it = await queue.ReceiveAsync();

                switch (it.Methode) {

                    case MethodID.Init: {

                            var promise = (TaskCompletionSource<bool>)it.Promise!;
                            try {
                                await module.Init((ModuleInitInfo)it.Param1!, (VariableValue[])it.Param2!, (Notifier)it.Param3!, moduleThread);
                                promise.SetResult(true);
                            }
                            catch (Exception exp) {
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.InitAbort: {
                            var promise = (TaskCompletionSource<bool>)it.Promise!;
                            try {
                                await module.InitAbort();
                                hasTerminated = true;
                                promise.SetResult(true);
                            }
                            catch (Exception exp) {
                                hasTerminated = true;
                                promise.SetException(exp);
                            }
                            break;
                        }

                    case MethodID.Run: {

                            var promise = (TaskCompletionSource<bool>)it.Promise!;
                            try {
                                Task t = module.Run((Func<bool>)it.Param1!);
                                Task ignored = t.ContinueOnMainThread(x => {
                                    hasTerminated = true;
                                    if (x.IsFaulted) {
                                        promise.SetException(x.Exception!);
                                    }
                                    else {
                                        promise.SetResult(true);
                                    }
                                    queue.Post(new WorkItem(MethodID.NoOp, null));
                                });
                            }
                            catch (Exception exp) {
                                hasTerminated = true;
                                promise.SetException(exp);
                                queue.Post(new WorkItem(MethodID.NoOp, null));
                            }
                            break;
                        }

                    case MethodID.GetAllObjects: {

                            var promise = (TaskCompletionSource<ObjectInfo[]>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.GetAllObjects();
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.GetMemberValues: {

                            var promise = (TaskCompletionSource<MemberValue[]>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.GetMemberValues((MemberRef[])it.Param1!);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.GetMetaInfo: {

                            var promise = (TaskCompletionSource<MetaInfos>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.GetMetaInfo();
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.GetObjectsByID: {

                            var promise = (TaskCompletionSource<ObjectInfo[]>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.GetObjectsByID((ObjectRef[])it.Param1!);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.GetObjectValuesByID: {

                            var promise = (TaskCompletionSource<ObjectValue[]>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.GetObjectValuesByID((ObjectRef[])it.Param1!);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.UpdateConfig: {

                            var promise = (TaskCompletionSource<Result>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.UpdateConfig((Origin)it.Param1!, (ObjectValue[]?)it.Param2, (MemberValue[]?)it.Param3, (AddArrayElement[]?)it.Param4);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.ReadVariables: {

                            var promise = (TaskCompletionSource<VTQ[]>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.ReadVariables((Origin)it.Param1!, (VariableRef[])it.Param2!, (Duration?)it.Param3!);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.WriteVariables: {

                            var promise = (TaskCompletionSource<WriteResult>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.WriteVariables((Origin)it.Param1!, (VariableValue[])it.Param2!, (Duration?)it.Param3);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.OnMethodCall: {

                            var promise = (TaskCompletionSource<Result<DataValue>>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.OnMethodCall((Origin)it.Param1!, (string)it.Param2!, (NamedValue[])it.Param3!);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.Browse: {

                            var promise = (TaskCompletionSource<BrowseResult>)it.Promise!;
                            if (hasTerminated) {
                                promise.SetException(new Exception("Module terminated."));
                            }
                            else {
                                try {
                                    var res = await module.BrowseObjectMemberValues((MemberRef)it.Param1!, (int?)it.Param2);
                                    promise.SetResult(res);
                                }
                                catch (Exception exp) {
                                    promise.SetException(exp);
                                }
                            }
                            break;
                        }

                    case MethodID.NoOp:
                        break;
                }
            } // while
        }

        private class WorkItem
        {
            public WorkItem(MethodID methode, object? promise, object? param1 = null, object? param2 = null, object? param3 = null, object? param4 = null) {
                Methode = methode;
                Promise = promise;
                Param1 = param1;
                Param2 = param2;
                Param3 = param3;
                Param4 = param4;
            }

            public MethodID Methode { get; set; }
            public object? Promise { get; set; }
            public object? Param1 { get; set; }
            public object? Param2 { get; set; }
            public object? Param3 { get; set; }
            public object? Param4 { get; set; }
        }

        enum MethodID
        {
            Init, InitAbort, Run, GetAllObjects, GetObjectsByID, GetMetaInfo, GetObjectValuesByID, GetMemberValues, UpdateConfig, ReadVariables, WriteVariables, OnMethodCall, Browse, NoOp
        }

        private class TheModuleThread : ModuleThread
        {
            private readonly SynchronizationContext syncContext = SynchronizationContext.Current!;

            public void Post(Action action) {
                syncContext.Post(delegate (object? state) { action(); }, null);
            }

            public void Post<T>(Action<T> action, T parameter) {
                syncContext.Post(delegate (object? state) { action(parameter); }, null);
            }

            public void Post<T1, T2>(Action<T1, T2> action, T1 parameter1, T2 parameter2) {
                syncContext.Post(delegate (object? state) { action(parameter1, parameter2); }, null);
            }

            public void Post<T1, T2, T3>(Action<T1, T2, T3> action, T1 parameter1, T2 parameter2, T3 parameter3) {
                syncContext.Post(delegate (object? state) { action(parameter1, parameter2, parameter3); }, null);
            }
        }
    }
}
