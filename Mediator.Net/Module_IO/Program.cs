// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Ifak.Fast.Mediator.IO
{
    class Program
    {
        static void Main(string[] args) {

            if (args.Length < 1) {
                Console.Error.WriteLine("Missing argument: port");
                return;
            }

            int port = int.Parse(args[0]);

            // Required to suppress premature shutdown when
            // pressing CTRL+C in parent Mediator console window:
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
            };

            var module = new Module();
            module.fLoadAdaptersFromAssembly = LoadTypesFromAssemblyFile;

            ExternalModuleHost.ConnectAndRunModule("localhost", port, module);
            Console.WriteLine("Terminated.");
        }

        private static Type[] LoadTypesFromAssemblyFile(string fileName) {
            try {
                Type baseClass = typeof(AdapterBase);

                var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                        fileName,
                        sharedTypes: new Type[] { baseClass });

                return loader.LoadDefaultAssembly()
                    .GetExportedTypes()
                    .Where(t => t.IsSubclassOf(baseClass) && !t.IsAbstract)
                    .ToArray();
            }
            catch (Exception exp) {
                Console.Error.WriteLine($"Failed to load adapter types from assembly '{fileName}': {exp.Message}");
                Console.Error.Flush();
                return new Type[0];
            }
        }
    }
}
