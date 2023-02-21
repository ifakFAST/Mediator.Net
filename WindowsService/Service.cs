using System;
using System.Diagnostics;
using System.ServiceProcess;
using System.Threading;
using System.Timers;
using System.IO;

namespace WinService
{
    public partial class Service : ServiceBase
    {
        private Process process = null;
        private bool stopping = false;
        private System.Timers.Timer aTimer;

        private string WorkingDir = "";
        private string StartCmd = "";
        private string StartArgs = "";
        private string CompletedFileNameOrNull = null;

        public Service() {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) {

            Extensions.Log("Service.OnStart");

            try {
                const string FileArgs = "WinServiceArgs.txt";

                string dirExe = Path.GetDirectoryName(Extensions.GetExecutablePath());
                string dirBin = Path.GetFullPath(Path.Combine(dirExe, ".."));
                string dirBase = Path.GetFullPath(Path.Combine(dirBin, ".."));
                string fileArgs = Path.Combine(dirBase, FileArgs);

                if (!File.Exists(fileArgs)) {
                    throw new Exception("Args file does not exist: " + fileArgs);
                }

                StartCmd = Path.Combine(dirBin, "Mediator\\MediatorCore.exe");

                if (!File.Exists(StartCmd)) {
                    throw new Exception("StartCmd does not exist: " + StartCmd);
                }

                WorkingDir = dirBase;
                StartArgs = File.ReadAllText(fileArgs).Trim();

                CompletedFileNameOrNull = GetCompletedFileName(StartArgs, dirBase);
                bool HasCompleteFile = !string.IsNullOrEmpty(CompletedFileNameOrNull);

                if (HasCompleteFile) {
                    DeleteFile(CompletedFileNameOrNull);
                }

                DateTime start = DateTime.UtcNow;
                process = StartProcess();

                TimeSpan MaxWait = HasCompleteFile ? TimeSpan.FromSeconds(60) : TimeSpan.FromSeconds(10);

                while (DateTime.UtcNow - start < MaxWait) {

                    Thread.Sleep(500);

                    if (process.HasExited) {
                        throw new Exception("Error starting service!");
                    }

                    if (HasCompleteFile && File.Exists(CompletedFileNameOrNull)) {
                        Extensions.Log("Process start completed.");
                        DeleteFile(CompletedFileNameOrNull);
                        break;
                    }
                }

                const int interval = 20 * 1000;
                aTimer = new System.Timers.Timer(interval);
                aTimer.Elapsed += new ElapsedEventHandler(OnTimedEvent);
                aTimer.Interval = interval;
                aTimer.Enabled = true;
            }
            catch (Exception exp) {
                Extensions.Log("Exception in Service.OnStart: " + exp.Message);
                ExitCode = 1;
                Stop();
            }
        }

        protected override void OnStop() {
            Extensions.Log("Service.OnStop");
            DoStop();
        }

        protected override void OnShutdown() {
            Extensions.Log("Service.OnShutdown");
            DoStop();
        }

        private void DoStop() {
            stopping = true;

            if (aTimer != null) {
                aTimer.Enabled = false;
                aTimer.Close();
                aTimer = null;
            }

            StopProcess(process);
        }

        private void OnTimedEvent(object source, ElapsedEventArgs e) {

            if (stopping) return;

            if (process != null && process.HasExited) {
                Extensions.Log("Process has exited unexpectedly. Restarting.");
                process.Close();
                process = StartProcess();
            }
        }

        private Process StartProcess() {
            Process process = new Process();
            process.StartInfo.FileName = StartCmd;
            process.StartInfo.Arguments = StartArgs;
            process.StartInfo.WorkingDirectory = WorkingDir;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = false;
            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.RedirectStandardInput = true;
            process.Start();
            return process;
        }

        private static void StopProcess(Process p) {

            if (p == null || p.HasExited) return;

            try {
                p.StandardInput.WriteLine("Stop");
            }
            catch (Exception) { }

            DateTime start = DateTime.UtcNow;
            while (true) {
                Thread.Sleep(500);
                if (p.HasExited) {
                    Extensions.Log("Process has exited.\r\n");
                    return;
                }
                if (DateTime.UtcNow - start > TimeSpan.FromSeconds(30)) {
                    try {
                        Extensions.Log("Process did not exit as requested. Killing...\r\n");
                        p.Kill();
                    }
                    catch (Exception) { }
                    return;
                }
            }
        }

        public static string GetCompletedFileName(string args, string baseDir) {
            const string ParamName = "--filestartcomplete=";
            int idx = args.IndexOf(ParamName);
            if (idx < 0) return null;
            idx += ParamName.Length;
            if (idx >= args.Length) return null;
            char terminate = ' ';
            args = args + ' ';
            if (args[idx] == '"') {
                idx += 1;
                terminate = '"';
            }
            int idxEnd = args.IndexOf(terminate, idx);
            if (idxEnd <= idx) return null;
            string file = args.Substring(idx, idxEnd - idx).Trim();
            return Path.GetFullPath(Path.Combine(baseDir, file));
        }

        private static void DeleteFile(string file) {
            try {
                File.Delete(file);
            }
            catch (Exception) { }
        }
    }

}
