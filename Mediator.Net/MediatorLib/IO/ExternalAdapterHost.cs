// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.IO
{
    public class ExternalAdapterHost
    {
        public static void ConnectAndRunAdapter(string host, int port, AdapterBase adapter) {

            var connector = new TcpConnectorSlave();
            connector.Connect(host, port);

            try {
                SingleThreadedAsync.Run(() => Loop(connector, adapter));
            }
            catch (Exception exp) {
                Console.Error.WriteLine("EXCEPTION: " + exp.Message);
            }
        }

        private static async Task Loop(TcpConnectorSlave connector, AdapterBase module) {

            Process? parentProcess = null;
            using (Request request = await connector.ReceiveRequest(5000)) {
                if (request.Code != AdapterMsg.ID_ParentInfo) {
                    throw new Exception("Missing ParentInfo request");
                }
                ParentInfoMsg? info = StdJson.ObjectFromUtf8Stream<ParentInfoMsg>(request.Payload);
                if (info == null) throw new Exception("ParentInfoMsg is null");
                parentProcess = Process.GetProcessById(info.PID);
                connector.SendResponseSuccess(request.RequestID, s => { });
            }

            Thread t = new Thread(() => { ParentAliveChecker(parentProcess); });
            t.IsBackground = true;
            t.Start();

            var helper = new AdapterHelper(module, connector);
            bool run = true;

            while (run) {
                using (Request request = await connector.ReceiveRequest()) {
                    helper.ExecuteAdapterRequestAsync(request);
                    bool shutdown = request.Code == AdapterMsg.ID_Shutdown;
                    run = !shutdown;
                }
            }

            // Wait until parent process kills us (after Shutdown method completed)
            while (true) {
                await Task.Delay(1000);
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

        public class AdapterHelper : AdapterCallback
        {
            private readonly AdapterBase adapter;
            private readonly TcpConnectorSlave connector;

            public AdapterHelper(AdapterBase module, TcpConnectorSlave connector) {
                this.adapter = module;
                this.connector = connector;
            }

            public void ExecuteAdapterRequestAsync(Request request) {

                int reqID = request.RequestID;

                switch (request.Code) {

                    case AdapterMsg.ID_Initialize: {
                            var msg = Deserialize<InititializeMsg>(request.Payload);
                            WrapCall(() => adapter.Initialize(msg.Adapter, this, msg.ItemInfos), SerializeArray, reqID);
                            break;
                        }
                    case AdapterMsg.ID_ReadDataItems: {
                            var msg = Deserialize<ReadDataItemsMsg>(request.Payload);
                            WrapCall(() => adapter.ReadDataItems(msg.Group, msg.Items, msg.Timeout), SerializeArray, reqID);
                            break;
                        }
                    case AdapterMsg.ID_WriteDataItems: {
                            var msg = Deserialize<WriteDataItemsMsg>(request.Payload);
                            WrapCall(() => adapter.WriteDataItems(msg.Group, msg.Values, msg.Timeout), SerializeObject, reqID);
                            break;
                        }
                    case AdapterMsg.ID_BrowseAdapterAddress: {
                            var msg = Deserialize<BrowseAdapterAddressMsg>(request.Payload);
                            WrapCall(() => adapter.BrowseAdapterAddress(), SerializeArray, reqID);
                            break;
                        }
                    case AdapterMsg.ID_BrowseDataItemAddress: {
                            var msg = Deserialize<BrowseDataItemAddressMsg>(request.Payload);
                            WrapCall(() => adapter.BrowseDataItemAddress(msg.IdOrNull), SerializeArray, reqID);
                            break;
                        }
                    case AdapterMsg.ID_Shutdown: {
                            var msg = Deserialize<ShutdownMsg>(request.Payload);
                            WrapVoidCall(() => adapter.Shutdown(), reqID);
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
                            Thread.Sleep(100);
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
                    Thread.Sleep(100);
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
                            Thread.Sleep(100);
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
                    Thread.Sleep(100);
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

            public void Notify_DataItemsChanged(DataItemValue[] values) {
                connector.SendEvent(AdapterMsg.ID_Event_DataItemsChanged, s => StdJson.ObjectToStream(values, s));
            }

            public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
                connector.SendEvent(AdapterMsg.ID_Event_AlarmOrEvent, s => StdJson.ObjectToStream(eventInfo, s));
            }

            public void Notify_NeedRestart(string reason) {
				Environment.Exit(1);
            }

            public void Notify_AdapterVarUpdate(AdapterVar variable, VTQ value) {
                var obj = new AdapterVarUpdate(variable, value);
                connector.SendEvent(AdapterMsg.ID_Event_AdapterVarChanged, s => StdJson.ObjectToStream(obj, s));
            }

            private void SendEvent<T>(IList<T> values, byte eventID) {
                if (values == null || values.Count == 0) return;
                connector.SendEvent(eventID, s => StdJson.ObjectToStream(values, s));
            }

            public void UpdateConfig(ConfigUpdate info) {
                connector.SendEvent(AdapterMsg.ID_Event_UpdateConfig, s => StdJson.ObjectToStream(info, s));
            }
        }

        public sealed class AdapterVarUpdate {

            public AdapterVarUpdate() {
            }

            public AdapterVarUpdate(AdapterVar variable, VTQ value) {
                Variable = variable;
                Value = value;
            }

            public AdapterVar Variable { get; set; }
            public VTQ Value { get; set; }
        }
    }
}
