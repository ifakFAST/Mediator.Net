// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NLog;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator
{
    public class ExternalModule : ModuleBase
    {
        private Process process = null;
        private TcpConnectorMaster connection = null;
        private Logger logger = LogManager.GetLogger("ExternalModule");
        private Task taskReceive;
        private Notifier notifier;
        private string moduleName = "?";

        public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {

            this.moduleName = info.ModuleName;
            this.notifier = notifier;
            logger = LogManager.GetLogger(info.ModuleName);

            var config = info.GetConfigReader();

            string cmd = config.GetString("ExternalCommand");
            string args = config.GetString("ExternalArgs");

            const string portPlaceHolder = "{PORT}";
            if (!args.Contains(portPlaceHolder)) throw new Exception("Missing port placeholder in args parameter: {PORT}");

            var server = TcpConnectorServer.ListenOnFreePort();
            int port = server.Port;

            args = args.Replace(portPlaceHolder, port.ToString());

            const string releaseDebugPlaceHolder = "{RELEASE_OR_DEBUG}";
            if (args.Contains(releaseDebugPlaceHolder)) {
#if DEBUG
                args = args.Replace(releaseDebugPlaceHolder, "Debug");
#else
                args = args.Replace(releaseDebugPlaceHolder, "Release");
#endif
            }

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

                var initMsg = new InitOrThrowMsg() {
                    InitInfo = info,
                    RestoreVariableValues = restoreVariableValues
                };

                Task tInit = SendVoidRequest(initMsg);

                taskReceive = connection.ReceiveAndDistribute(onEvent);

                Task t = await Task.WhenAny(tInit, taskReceive);

                if (t != tInit) {
                    if (process.HasExited)
                        throw new Exception("Module process terminated during Init call.");
                    else
                        throw new Exception("TCP connection broken to Module process during Init call.");
                }

                await tInit;
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

        public async override Task InitAbort() {

            if (process == null) return; // abort already happened in Init()

            var taskAbort = SendVoidRequest(new InitAbortMsg());

            try {

                Timestamp tStart = Timestamp.Now;
                const int timeout = 12;

                while (!taskAbort.IsCompleted) {

                    if (process.HasExited) {
                        logger.Warn("External module terminated unexpectedly during init abort.");
                        break;
                    }

                    if (Timestamp.Now - tStart > Duration.FromSeconds(timeout)) {
                        logger.Warn($"Module did not return from InitAbort within {timeout} seconds. Killing process...");
                        break;
                    }

                    await Task.WhenAny(taskAbort, Task.Delay(2000));

                    if (!taskAbort.IsCompleted) {
                        long secondsUntilTimeout = (tStart.AddSeconds(timeout) - Timestamp.Now).TotalMilliseconds / 1000;
                        logger.Info("Waiting for InitAbort completion (timeout in {0} seconds)...", secondsUntilTimeout);
                    }
                }
            }
            finally {
                connection.Close("Init abort");
                StopProcess(process);
                process = null;
            }
        }

        private void onEvent(Event evt) {
            switch (evt.Code) {
                case ExternalModuleHost.ModuleHelper.ID_Event_VariableValuesChanged:
                    var values = StdJson.ObjectFromUtf8Stream<VariableValue[]>(evt.Payload);
                    notifier.Notify_VariableValuesChanged(values);
                    break;

                case ExternalModuleHost.ModuleHelper.ID_Event_ConfigChanged:
                    var objects = StdJson.ObjectFromUtf8Stream<ObjectRef[]>(evt.Payload);
                    notifier.Notify_ConfigChanged(objects);
                    break;

                case ExternalModuleHost.ModuleHelper.ID_Event_AlarmOrEvent:
                    var ae = StdJson.ObjectFromUtf8Stream<AlarmOrEventInfo>(evt.Payload);
                    notifier.Notify_AlarmOrEvent(ae);
                    break;

                default:
                    logger.Error("Unknown event code: " + evt.Code);
                    break;
            }
        }

        public override async Task Run(Func<bool> shutdown) {

            string closeReason = "Module shutdown";

            try {

                var taskRun = SendVoidRequest(new RunMsg());

                while (!shutdown() && !taskRun.IsCompleted && !taskReceive.IsCompleted) {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }

                if (taskRun.IsCompleted) {
                    closeReason = $"External module {moduleName} returned from Run unexpectedly.";
                    logger.Warn(closeReason);
                }
                else if (taskReceive.IsFaulted || process.HasExited) {
                    Thread.Sleep(500); // no need for async wait here
                    closeReason = $"External module {moduleName} terminated unexpectedly.";
                    throw new Exception(closeReason);
                }
                else {

                    var taskShutdown = SendVoidRequest(new ShutdownMsg());

                    Timestamp tStart = Timestamp.Now;
                    const int timeout = 20;

                    while (!taskRun.IsCompleted) {

                        if (taskShutdown.IsFaulted || process.HasExited) {
                            logger.Warn("External module terminated unexpectedly during shutdown.");
                            break;
                        }

                        if (Timestamp.Now - tStart > Duration.FromSeconds(timeout)) {
                            logger.Warn($"Module did not return from Run within {timeout} seconds. Killing process...");
                            break;
                        }

                        await Task.WhenAny(taskRun, Task.Delay(2000));

                        if (!taskRun.IsCompleted) {
                            long secondsUntilTimeout = (tStart.AddSeconds(timeout) - Timestamp.Now).TotalMilliseconds / 1000;
                            logger.Info("Waiting for Run completion (timeout in {0} seconds)...", secondsUntilTimeout);
                        }
                    }
                }
            }
            finally {
                connection.Close(closeReason);
                StopProcess(process);
                process = null;
            }
        }

        #region Methods

        public override async Task<ObjectInfo[]> GetAllObjects() {
            var msg = new GetAllObjectsMsg();
            return await SendRequest<ObjectInfo[]>(msg);
        }

        public override async Task<MemberValue[]> GetMemberValues(MemberRef[] member) {
            var msg = new GetMemberValuesMsg() {
                Member = member
            };
            return await SendRequest<MemberValue[]>(msg);
        }

        public override async Task<MetaInfos> GetMetaInfo() {
            var msg = new GetMetaInfoMsg();
            return await SendRequest<MetaInfos>(msg);
        }

        public override async Task<ObjectInfo[]> GetObjectsByID(ObjectRef[] ids) {
            var msg = new GetObjectsByIDMsg() {
                IDs = ids
            };
            return await SendRequest<ObjectInfo[]>(msg);
        }

        public override async Task<ObjectValue[]> GetObjectValuesByID(ObjectRef[] objectIDs) {
            var msg = new GetObjectValuesByIDMsg() {
                ObjectIDs = objectIDs
            };
            return await SendRequest<ObjectValue[]>(msg);
        }

        public override async Task<VTQ[]> ReadVariables(Origin origin, VariableRef[] variables, Duration? timeout) {
            var msg = new ReadVariablesMsg() {
                Origin = origin,
                Variables = variables,
                Timeout = timeout
            };
            return await SendRequest<VTQ[]>(msg);
        }

        public override async Task<Result> UpdateConfig(Origin origin, ObjectValue[] updateOrDeleteObjects, MemberValue[] updateOrDeleteMembers, AddArrayElement[] addArrayElements) {
            var msg = new UpdateConfigMsg() {
               Origin = origin,
               UpdateOrDeleteObjects = updateOrDeleteObjects,
               UpdateOrDeleteMembers = updateOrDeleteMembers,
               AddArrayElements = addArrayElements
            };
            return await SendRequest<Result>(msg);
        }

        public override async Task<WriteResult> WriteVariables(Origin origin, VariableValue[] values, Duration? timeout) {
            var msg = new WriteVariablesMsg() {
                Origin = origin,
                Values = values,
                Timeout = timeout
            };
            return await SendRequest<WriteResult>(msg);
        }

        public override async Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {
            var msg = new OnMethodCallMsg() {
                Origin = origin,
                Method = methodName,
                Parameters = parameters
            };
            return await SendRequest<Result<DataValue>>(msg);
        }

        public override async Task<BrowseResult> BrowseObjectMemberValues(MemberRef member, int? continueID = null) {
            var msg = new BrowseMsg() {
                Member = member,
                ContinueID = continueID
            };
            return await SendRequest<BrowseResult>(msg);
        }

        #endregion

        #region Helper

        private async Task<T> SendRequest<T>(ModuleMsg requestMsg) {
            using (Response res = await connection.SendRequest(requestMsg.GetMessageCode(), stream => StdJson.ObjectToStream(requestMsg, stream))) {
                if (res.Success) {
                    return StdJson.ObjectFromUtf8Stream<T>(res.SuccessPayload);
                }
                else {
                    throw new Exception(res.ErrorMsg);
                }
            }
        }

        private async Task SendVoidRequest(ModuleMsg requestMsg) {
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
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.OutputDataReceived += OnReceivedOutput;
            process.ErrorDataReceived += OnReceivedError;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }

        private void OnReceivedOutput(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                logger.Info(e.Data);
            }
        }

        private void OnReceivedError(object sender, DataReceivedEventArgs e) {
            if (!string.IsNullOrEmpty(e.Data)) {
                logger.Error(e.Data);
            }
        }

        private void StopProcess(Process p) {
            if (p == null || p.HasExited) return;
            try {
                p.Kill();
            }
            catch (Exception exp) {
                logger.Warn("StopProcess: " + exp.Message);
            }
        }

        #endregion
    }
}
