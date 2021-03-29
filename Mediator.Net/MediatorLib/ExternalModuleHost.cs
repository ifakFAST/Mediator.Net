// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Ifak.Fast.Mediator
{
    public class ExternalModuleHost
    {
        public static void ConnectAndRunModule(int port, ModuleBase module) {
            string host = System.Net.IPAddress.Loopback.ToString(); // == 127.0.0.1
            ConnectAndRunModule(host, port, module);
        }

        public static void ConnectAndRunModule(string host, int port, ModuleBase module) {

            var connector = new TcpConnectorSlave();
            connector.Connect(host, port);

            try {
                SingleThreadedAsync.Run(() => Loop(connector, module));
            }
            catch (Exception exp) {
                Console.Error.WriteLine("EXCEPTION: " + exp.Message);
            }
        }

        private static void ParentAliveChecker(Process parentProcess) {
            while (true) {
                Thread.Sleep(5000);
                if (parentProcess.HasExited) {
                    Environment.Exit(0);
                    return;
                }
            }
        }

        private static async Task Loop(TcpConnectorSlave connector, ModuleBase module) {

            Process? parentProcess = null;
            using (Request request = await connector.ReceiveRequest(5000)) {
                if (request.Code != ModuleHelper.ID_ParentInfo) {
                    throw new Exception("Missing ParentInfo request (update MediatorLib.dll)");
                }
                ParentInfoMsg? info = StdJson.ObjectFromUtf8Stream<ParentInfoMsg>(request.Payload);
                if (info == null) {
                    throw new Exception("ParentInfoMsg is null");
                }
                parentProcess = Process.GetProcessById(info.PID);
                connector.SendResponseSuccess(request.RequestID, s => { });
            }

            Thread t = new Thread(() => { ParentAliveChecker(parentProcess);  });
            t.IsBackground = true;
            t.Start();

            var helper = new ModuleHelper(module, connector);
            bool run = true;

            while (run) {
                using (Request request = await connector.ReceiveRequest()) {
                    helper.ExecuteModuleRequestAsync(request);
                    bool shutdown = request.Code == ModuleHelper.ID_Shutdown;
                    bool initAbort = request.Code == ModuleHelper.ID_InitAbort;
                    run = !shutdown && !initAbort;
                }
            }

            // Wait until parent process kills us (after Run method completed)
            while (true) {
                await Task.Delay(1000);
            }
        }

        public class ModuleHelper : Notifier, ModuleThread
        {
            private readonly ModuleBase module;
            private readonly TcpConnectorSlave connector;

            private bool shutdownRequested = false;

            public ModuleHelper(ModuleBase module, TcpConnectorSlave connector) {
                this.module = module;
                this.connector = connector;
            }

            public void ExecuteModuleRequestAsync(Request request) {

                int reqID = request.RequestID;

                switch (request.Code) {

                    case ID_InitOrThrow: {
                            var msg = Deserialize<InitOrThrowMsg>(request.Payload);
                            WrapVoidCall(() => module.Init(msg.InitInfo, msg.RestoreVariableValues, this, this), reqID);
                            break;
                        }
                    case ID_InitAbort: {
                            WrapVoidCall(() => module.InitAbort(), reqID);
                            break;
                        }
                    case ID_Run: {
                            WrapVoidCallRun(() => module.Run(() => { return shutdownRequested; }), reqID);
                            break;
                        }
                    case ID_Shutdown: {
                            shutdownRequested = true;
                            break;
                        }
                    case ID_GetAllObjects: {
                            WrapCall(() => module.GetAllObjects(), SerializeArray, reqID);
                            break;
                        }
                    case ID_GetObjectsByID: {
                            var msg = Deserialize<GetObjectsByIDMsg>(request.Payload);
                            WrapCall(() => module.GetObjectsByID(msg.IDs), SerializeArray, reqID);
                            break;
                        }
                    case ID_GetMetaInfo: {
                            WrapCall(() => module.GetMetaInfo(), SerializeObject, reqID);
                            break;
                        }
                    case ID_GetObjectValuesByID: {
                            var msg = Deserialize<GetObjectValuesByIDMsg>(request.Payload);
                            WrapCall(() => module.GetObjectValuesByID(msg.ObjectIDs), SerializeArray, reqID);
                            break;
                        }
                    case ID_GetMemberValues: {
                            var msg = Deserialize<GetMemberValuesMsg>(request.Payload);
                            WrapCall(() => module.GetMemberValues(msg.Member), SerializeArray, reqID);
                            break;
                        }
                    case ID_UpdateConfig: {
                            var msg = Deserialize<UpdateConfigMsg>(request.Payload);
                            WrapCall(() => module.UpdateConfig(msg.Origin, msg.UpdateOrDeleteObjects, msg.UpdateOrDeleteMembers, msg.AddArrayElements), SerializeObject, reqID);
                            break;
                        }
                    case ID_ReadVariables: {
                            var msg = Deserialize<ReadVariablesMsg>(request.Payload);
                            WrapCall(() => module.ReadVariables(msg.Origin, msg.Variables, msg.Timeout), SerializeObject, reqID);
                            break;
                        }
                    case ID_WriteVariables: {
                            var msg = Deserialize<WriteVariablesMsg>(request.Payload);
                            WrapCall(() => module.WriteVariables(msg.Origin, msg.Values, msg.Timeout), SerializeObject, reqID);
                            break;
                        }
                    case ID_OnMethodCall: {
                            var msg = Deserialize<OnMethodCallMsg>(request.Payload);
                            WrapCall(() => module.OnMethodCall(msg.Origin, msg.Method, msg.Parameters), SerializeObject, reqID);
                            break;
                        }
                    case ID_Browse: {
                            var msg = Deserialize<BrowseMsg>(request.Payload);
                            WrapCall(() => module.BrowseObjectMemberValues(msg.Member, msg.ContinueID), SerializeObject, reqID);
                            break;
                        }
                    default:
                        throw new Exception("Unexpected op code " + request.Code);
                }
            }

            private void WrapCall<T>(Func<Task<T>> call, Action<T, Stream> serializer, int reqID) {
                try {
                    Task<T> task = call();
                    var tnext = task.ContinueOnMainThread(t => {
                        if (t.IsFaulted) {
                            Console.Error.WriteLine(MakeStr(t.Exception));
                            Console.Error.Flush();
                            connector.SendResponseError(reqID, t.Exception.InnerMost());
                        }
                        else {
                            T res = t.Result;
                            connector.SendResponseSuccess(reqID, stream => serializer(res, stream));
                        }
                    });
                }
                catch (Exception exp) {
                    Console.Error.WriteLine(MakeStr(exp));
                    Console.Error.Flush();
                    connector.SendResponseError(reqID, exp.InnerMost());
                }
            }

            private void WrapVoidCall(Func<Task> call, int reqID) {
                try {
                    Task task = call();
                    var tnext = task.ContinueOnMainThread(t => {
                        if (t.IsFaulted) {
                            Console.Error.WriteLine(MakeStr(t.Exception));
                            Console.Error.Flush();
                            connector.SendResponseError(reqID, t.Exception.InnerMost());
                        }
                        else {
                            connector.SendResponseSuccess(reqID, s => { });
                        }
                    });
                }
                catch (Exception exp) {
                    Console.Error.WriteLine(MakeStr(exp));
                    Console.Error.Flush();
                    connector.SendResponseError(reqID, exp.InnerMost());
                }
            }

            private void WrapVoidCallRun(Func<Task> call, int reqID) {
                try {
                    Task task = call();
                    var tnext = task.ContinueOnMainThread(t => {
                        try {
                            if (t.IsFaulted) {
                                Console.Error.WriteLine(MakeStr(t.Exception));
                                Console.Error.Flush();
                                connector.SendResponseError(reqID, t.Exception.InnerMost());
                            }
                            else {
                                connector.SendResponseSuccess(reqID, s => { });
                            }
                        }
                        finally {
                            connector.Close();
                        }
                    });
                }
                catch (Exception exp) {
                    Console.Error.WriteLine(MakeStr(exp));
                    Console.Error.Flush();
                    connector.SendResponseError(reqID, exp.InnerMost());
                }
            }

            private static string MakeStr(Exception exp) {
                exp = exp.InnerMost();
                return exp.Message + "\n" + exp.StackTrace;
            }

            private static T Deserialize<T>(MemoryStream stream) => StdJson.ObjectFromUtf8Stream<T>(stream) ?? throw new Exception("Unexpected null value");

            private static readonly byte[] EmptyArray = new byte[] { (byte)'[', (byte)']' };

            private void SerializeArray<T>(T[] array, Stream output) {
                if (array == null || array.Length == 0) {
                    output.WriteByte((byte)'[');
                    output.WriteByte((byte)']');
                }
                else {
                    StdJson.ObjectToStream(array, output);
                }
            }

            private void SerializeObject<T>(T obj, Stream output) where T: notnull {
                StdJson.ObjectToStream(obj, output);
            }

            //private byte[] SerializeArray<T>(T[] array) {
            //    if (array == null || array.Length == 0) return EmptyArray;
            //    return StdJson.ObjectToBytes(array);
            //}

            //private byte[] SerializeObject<T>(T obj) {
            //    return StdJson.ObjectToBytes(obj);
            //}

            public const byte ID_ParentInfo = 99;
            public const byte ID_InitOrThrow = 1;
            public const byte ID_InitAbort = 2;
            public const byte ID_Run = 3;
            public const byte ID_Shutdown = 4;
            public const byte ID_GetAllObjects = 5;
            public const byte ID_GetObjectsByID = 6;
            public const byte ID_GetMetaInfo = 7;
            public const byte ID_GetObjectValuesByID = 8;
            public const byte ID_GetMemberValues = 9;
            public const byte ID_UpdateConfig = 10;
            public const byte ID_ReadVariables = 11;
            public const byte ID_WriteVariables = 12;
            public const byte ID_OnMethodCall = 13;
            public const byte ID_Browse = 14;

            public void Notify_VariableValuesChanged(List<VariableValue> values) {
                if (values == null || values.Count == 0) return;
                connector.SendEvent(ID_Event_VariableValuesChanged, s => BinSeri.VariableValue_Serializer.Serialize(s, values, BinSeri.Common.CurrentBinaryVersion));
            }

            public void Notify_ConfigChanged(List<ObjectRef> changedObjects) {
                if (changedObjects == null || changedObjects.Count == 0) return;
                connector.SendEvent(ID_Event_ConfigChanged, s => StdJson.ObjectToStream(changedObjects, s));
            }

            public void Notify_AlarmOrEvent(AlarmOrEventInfo eventInfo) {
                connector.SendEvent(ID_Event_AlarmOrEvent, s => StdJson.ObjectToStream(eventInfo, s));
            }

            private SingleThreadedAsync.SingleThreadSynchronizationContext syncContext = (SynchronizationContext.Current as SingleThreadedAsync.SingleThreadSynchronizationContext)!;


            public void Post(Action action) {
                syncContext.Post(delegate (object state) { action(); }, null);
            }

            public void Post<T>(Action<T> action, T parameter) {
                syncContext.Post(delegate (object state) { action(parameter); }, null);
            }

            public void Post<T1, T2>(Action<T1,T2> action, T1 parameter1, T2 parameter2) {
                syncContext.Post(delegate (object state) { action(parameter1, parameter2); }, null);
            }

            public void Post<T1, T2, T3>(Action<T1, T2, T3> action, T1 parameter1, T2 parameter2, T3 parameter3) {
                syncContext.Post(delegate (object state) { action(parameter1, parameter2, parameter3); }, null);
            }

            public const byte ID_Event_VariableValuesChanged = 1;
            public const byte ID_Event_ConfigChanged = 2;
            public const byte ID_Event_AlarmOrEvent = 3;
        }
    }
}
