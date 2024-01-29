using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Calc
{
    public class ExternalAdapterHost
    {
        private static bool shutdownWasCalled = false;

        public static void ConnectAndRunAdapter(string host, int port, CalculationBase adapter) {

            var connector = new TcpConnectorSlave();
            connector.Connect(host, port);

            try {
                SingleThreadedAsync.Run(() => Loop(connector, adapter));
            }
            catch (Exception exp) {
                Console.Error.WriteLine("EXCEPTION: " + exp.Message);
            }

            if (!shutdownWasCalled) {
                try {
                    SingleThreadedAsync.Run(() => adapter.Shutdown());
                }
                catch (Exception exp) {
                    Console.Error.WriteLine("EXCEPTION: " + exp.Message);
                }
            }
        }

        private static async Task Loop(TcpConnectorSlave connector, CalculationBase adapter) {

            Process? parentProcess = null;
            using (Request request = await connector.ReceiveRequest(5000)) {
                if (request.Code != AdapterMsg.ID_ParentInfo) {
                    throw new Exception("Missing ParentInfo request");
                }
                ParentInfoMsg? info = StdJson.ObjectFromUtf8Stream<ParentInfoMsg>(request.Payload) ?? throw new Exception("ParentInfoMsg is null");
                parentProcess = Process.GetProcessById(info.PID);
                connector.SendResponseSuccess(request.RequestID, s => { });
            }

            Thread t = new(() => { ParentAliveChecker(parentProcess); });
            t.IsBackground = true;
            t.Start();

            var helper = new AdapterHelper(adapter, connector);
            bool run = true;

            while (run) {
                using Request request = await connector.ReceiveRequest();
                helper.ExecuteAdapterRequestAsync(request);
                bool shutdown = request.Code == AdapterMsg.ID_Shutdown;
                run = !shutdown;
                if (shutdown) {
                    shutdownWasCalled = true;
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

                if (HasExited(parentProcess)) {

                    Thread.Sleep(1000);

                    // if we come here, the main loop did not exit, so we kill our process
                    Process.GetCurrentProcess().Kill();

                    return;
                }
            }
        }

        private static bool HasExited(Process process) {
            try {
                return process.HasExited;
            }
            catch (Exception) {
                return true;
            }
        }

        public class AdapterHelper : AdapterCallback
        {
            private readonly CalculationBase adapter;
            private readonly TcpConnectorSlave connector;

            public AdapterHelper(CalculationBase module, TcpConnectorSlave connector) {
                this.adapter = module;
                this.connector = connector;
            }

            public void ExecuteAdapterRequestAsync(Request request) {

                int reqID = request.RequestID;

                switch (request.Code) {

                    case AdapterMsg.ID_Initialize: {
                            var msg = Deserialize<InititializeMsg>(request.Payload);
                            WrapCall(() => adapter.Initialize(msg.Parameter, this), SerializeObject, reqID);
                            break;
                        }
                    case AdapterMsg.ID_Step: {
                            var msg = Deserialize<StepMsg>(request.Payload);
                            WrapCall(() => adapter.Step(msg.Time, msg.DeltaT, msg.InputValues), SerializeObject, reqID);
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

            private void SerializeObject<T>(T obj, Stream output) where T: notnull {
                StdJson.ObjectToStream(obj, output);
            }

            public void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo) {
                connector.SendEvent(AdapterMsg.ID_Event_AlarmOrEvent, s => StdJson.ObjectToStream(eventInfo, s));
            }

            public void Notify_NeedRestart(string reason) {
				Environment.Exit(1);
            }
        }
    }
}
