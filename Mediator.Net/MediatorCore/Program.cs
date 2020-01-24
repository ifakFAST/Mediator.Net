// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using NLog;
using NLog.LayoutRenderers;
using System;
using System.IO;
using System.Threading;

namespace Ifak.Fast.Mediator
{
    class Program
    {
        static void Main(string[] args) {

            if (args.Length > 1 && args[0] == "encrypt") {
                string text = args[1];
                string encrypted = SimpleEncryption.Encrypt(text);
                Console.WriteLine("Encrypted = " + encrypted);
                return;
            }

            string configFileName = "";
            string title = "";
            string logDir = "";
            string logName = "";
            string fileStartComplete = "";
            bool clearDBs = false;

            Parser.Default.ParseArguments<Options>(args).WithParsed(o => {
                configFileName = o.ConfigFileName;
                title = o.Title;
                logDir = o.LogDir;
                logName = o.LogName;
                fileStartComplete = o.FileStartComplete;
                clearDBs = o.ClearDBs;
            });

            string fullLogDir = Path.GetFullPath(logDir);
            LayoutRenderer.Register("log-output-dir", (logEvent) => fullLogDir);
            LayoutRenderer.Register("log-file-name", (logEvent) => logName);

            string workingDir = Directory.GetCurrentDirectory();
            Console.Title = $"{title} - {workingDir}";

            Logger logger = LogManager.GetLogger("Mediator.Prog");

            if (!File.Exists(configFileName)) {

                const string oldConfigFileName = "config.xml";
                if (configFileName == Options.DefaultConfigName && File.Exists(oldConfigFileName)) {
                    configFileName = oldConfigFileName;
                    logger.Info($"Using old configuration file name \"{oldConfigFileName}\". Consider renaming file to \"{Options.DefaultConfigName}\".");
                }
                else {
                    logger.Error($"Main configuration file \"{configFileName}\" not found in {workingDir}");
                    return;
                }
            }

            logger.Info($"Starting {title}...");

            var core = new MediatorCore();

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
                core.RequestShutdown();
                logger.Info($"{title} terminate requested...");
            };

            Thread t = new Thread(() => { DoReader(logger, core, title); });
            t.IsBackground = true;
            t.Start();

            try {
                SingleThreadedAsync.Run(() => core.Run(configFileName, clearDBs, fileStartComplete));
            }
            catch (Exception exp) {
                logger.Error(exp.GetBaseException(), exp.Message);
            }

            logger.Info($"{title} terminated.");
        }

        private static void DoReader(Logger logger, MediatorCore core, string title) {

            while (true) {
                string line = Console.In.ReadLine();
                if (line == "Stop") {
                    core.RequestShutdown();
                    logger.Info($"{title} terminate requested...");
                    return;
                }
            }
        }
    }

    public class Options
    {
        internal const string DefaultConfigName = "AppConfig.xml";

        [Option('c', "config", Required = false, HelpText = "Configuration file name", Default = DefaultConfigName)]
        public string ConfigFileName { get; set; }

        [Option('t', "title", Required = false, HelpText = "Console window title", Default = "Mediator")]
        public string Title { get; set; }

        [Option('l', "logdir", Required = false, HelpText = "Log file directory", Default = ".")]
        public string LogDir { get; set; }

        [Option('n', "logname", Required = false, HelpText = "Name of the log file", Default = "LogFile")]
        public string LogName { get; set; }

        [Option('d', "clearDBs", Required = false, HelpText = "Delete contents of historian databases, e.g. for clean testing", Default = false)]
        public bool ClearDBs { get; set; }

        [Option('f', "filestartcomplete", Required = false, HelpText = "Name of the file that is written after successful start.", Default = "")]
        public string FileStartComplete { get; set; }
    }
}
