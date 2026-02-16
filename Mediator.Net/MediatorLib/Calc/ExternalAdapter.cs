using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc
{
    public abstract class ExternalAdapter : CalculationBase
    {
        protected AdapterCallback? callback = null;

        private Process? process = null;
        private Task? taskReceive = null;
        private TcpConnectorMaster? connection = null;
        private bool shutdown = false;
        protected abstract string GetCommand(Config config);
        protected abstract string GetArgs(Config config);
        private string adapterName = "";
        private ModuleInitInfo? moduleInitInfo = null;

        public override async Task<InitResult> Initialize(InitParameter parameter, AdapterCallback callback) {

            this.moduleInitInfo = parameter.ModuleInitInfo;
            this.callback = callback;
            this.adapterName = parameter.Calculation.Name;

            var config = new Config(parameter.ModuleConfig);

            string cmd = GetCommand(config);
            string args = GetArgs(config);

            if (!File.Exists(cmd)) {
                throw new Exception($"External adapter command '{cmd}' not found.");
            }

            const string portPlaceHolder = "{PORT}";
            if (!args.Contains(portPlaceHolder)) throw new Exception("Missing port placeholder in args parameter: {PORT}");

            var server = TcpConnectorServer.ListenOnFreePort();
            int port = server.Port;

            args = args.Replace(portPlaceHolder, port.ToString());

            try {

                var taskConnect = server.WaitForConnect(TimeSpan.FromSeconds(60));

                process = StartProcess(cmd, args);

                while (!process.HasExited && !taskConnect.IsCompleted) {
                    await Task.Delay(TimeSpan.FromMilliseconds(50));
                }

                if (process.HasExited) {
                    throw new Exception($"Failed to start command \"{cmd}\" with arguments \"{args}\"");
                }

                connection = await taskConnect;

                var parentInfo = new ParentInfoMsg() { PID = Process.GetCurrentProcess().Id };
                Task ignored = SendVoidRequest(parentInfo);

                var initMsg = new InititializeMsg() {
                    Parameter = parameter,
                    Info = this.moduleInitInfo,
                };

                Task<InitResult> tInit = SendRequest<InitResult>(initMsg);

                taskReceive = connection.ReceiveAndDistribute(onEvent);

                Task t = await Task.WhenAny(tInit, taskReceive);

                if (t != tInit) {
                    if (process.HasExited)
                        throw new Exception("Adapter process terminated during Init call.");
                    else
                        throw new Exception("TCP connection broken to Adapter process during Init call.");
                }

                InitResult res = await tInit;

                Task ignored2 = Supervise();

                return res;
            }
            catch (Exception) {
                if (connection != null) {
                    connection.Close("Init failed.");
                }
                StopProcess(process);
                process = null;
                throw;
            }
            finally {
                server.StopListening();
            }
        }

        private async Task Supervise() {

            while (!shutdown && taskReceive != null && !taskReceive.IsCompleted && process != null && !process.HasExited) {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }

            if (shutdown || process == null) return;

            if (taskReceive == null || taskReceive.IsFaulted || process.HasExited) {
                Thread.Sleep(500); // no need for async wait here
                callback?.Notify_NeedRestart($"External adapter {adapterName} terminated unexpectedly.");
            }
        }

        public override async Task<StepResult> Step(Timestamp t, Duration dt, InputValue[] inputValues) {
            var msg = new StepMsg() {
                Time = t,
                DeltaT = dt,
                InputValues = inputValues,
            };
            return await SendRequest<StepResult>(msg);
        }

        public override async Task Shutdown() {

            shutdown = true;
            if (process == null) return;

            var taskAbort = SendVoidRequest(new ShutdownMsg());

            try {

                Timestamp tStart = Timestamp.Now;
                const int timeout = 8;

                while (!taskAbort.IsCompleted) {

                    if (process.HasExited) {
                        Console.Out.WriteLine("External adapter terminated during shutdown.");
                        break;
                    }

                    if (Timestamp.Now - tStart > Duration.FromSeconds(timeout)) {
                        Console.Out.WriteLine($"Adapter did not return from Shutdown within {timeout} seconds. Killing process...");
                        break;
                    }

                    await Task.WhenAny(taskAbort, Task.Delay(2000));

                    if (process.HasExited) {
                        Console.Out.WriteLine("External adapter terminated during shutdown.");
                        break;
                    }

                    if (!taskAbort.IsCompleted) {
                        long secondsUntilTimeout = (tStart.AddSeconds(timeout) - Timestamp.Now).TotalMilliseconds / 1000;
                        Console.Out.WriteLine("Waiting for Shutdown completion (timeout in {0} seconds)...", secondsUntilTimeout);
                    }
                }
            }
            finally {
                connection?.Close("Shutdown");
                StopProcess(process);
                process = null;
            }
        }

        private void onEvent(Event evt) {
            switch (evt.Code) {
                case AdapterMsg.ID_Event_AlarmOrEvent:
                    var alarm = StdJson.ObjectFromUtf8Stream<AdapterAlarmOrEvent>(evt.Payload);
                    if (alarm != null) {
                        callback?.Notify_AlarmOrEvent(alarm);
                    }
                    break;

                default:
                    Console.Error.WriteLine("Unknown event code: " + evt.Code);
                    break;
            }
        }

        private async Task<T> SendRequest<T>(AdapterMsg requestMsg) {
            if (connection == null) { throw new Exception("ExternalAdapter.SendRequest: connection is null"); }
            using (Response res = await connection.SendRequest(requestMsg.GetMessageCode(), stream => StdJson.ObjectToStream(requestMsg, stream))) {
                if (res.Success) {
                    return StdJson.ObjectFromUtf8Stream<T>(res.SuccessPayload!) ?? throw new Exception($"ExternalAdapter.SendRequest {requestMsg.GetType().Name}: returned result is null");
                }
                else {
                    throw new Exception(res.ErrorMsg);
                }
            }
        }

        private async Task SendVoidRequest(AdapterMsg requestMsg) {
            if (connection == null) { return; }
            using (Response res = await connection.SendRequest(requestMsg.GetMessageCode(), stream => StdJson.ObjectToStream(requestMsg, stream))) {
                if (res.Success) {
                    return;
                }
                else {
                    throw new Exception(res.ErrorMsg);
                }
            }
        }

        private Process StartProcess(string fileName, string args) {
            Process process = new Process();
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.Start();

            StartStreamReadThread(process.StandardOutput, (line) => {
                Console.Out.WriteLine(line);
            });
            StartStreamReadThread(process.StandardError, (line) => {
                Console.Error.WriteLine(line);
            });

            return process;
        }

        private static void StartStreamReadThread(StreamReader reader, Action<string> onGotLine) {
            var thread = new Thread(() => {
                while (true) {
                    string line = reader.ReadLine();
                    if (line == null) {
                        return;
                    }
                    if (line != "") {
                        onGotLine(line);
                    }
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private void StopProcess(Process? p) {
            if (p == null || p.HasExited) return;
            try {
                p.Kill();
            }
            catch (Exception exp) {
                Console.Out.WriteLine("StopProcess: " + exp.Message);
            }
        }
    }

    internal abstract class AdapterMsg
    {
        public const byte ID_Event_AlarmOrEvent = 1;

        public const byte ID_ParentInfo = 99;
        public const byte ID_Initialize = 1;
        public const byte ID_Step = 2;
        public const byte ID_Shutdown = 3;

        public abstract byte GetMessageCode();
    }

    internal class ParentInfoMsg : AdapterMsg
    {
        public int PID { get; set; }

        public override byte GetMessageCode() => ID_ParentInfo;
    }

    internal class InititializeMsg : AdapterMsg
    {
        public InitParameter Parameter { get; set; } = new InitParameter();
        public ModuleInitInfo? Info { get; set; } = null;

        public override byte GetMessageCode() => ID_Initialize;
    }

    internal class StepMsg : AdapterMsg
    {
        public Timestamp Time { get; set; }

        public Duration DeltaT { get; set; } = Duration.Zero;

        public InputValue[] InputValues { get; set; } = new InputValue[0];

        public override byte GetMessageCode() => ID_Step;
    }

    internal class ShutdownMsg : AdapterMsg
    {
        public override byte GetMessageCode() => ID_Shutdown;
    }
}
