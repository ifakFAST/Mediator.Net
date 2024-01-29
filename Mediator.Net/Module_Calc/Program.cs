// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;

namespace Ifak.Fast.Mediator.Calc
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
            Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) {
                e.Cancel = true;
            };

            if (args.Length == 2 && args[1] == "AdapterPython") {
                StartAdapterPython(port);
            }
            else {
                StartModuleCalc(port);
            }
        }

        private static void StartAdapterPython(int port) {
            var adapter = new Adapter_Python.PythonExternal();
            ExternalAdapterHost.ConnectAndRunAdapter("localhost", port, adapter);
        }

        private static void StartModuleCalc(int port) {

            var module = new Module();
            module.fLoadCalcTypesFromAssembly = LoadTypesFromAssemblyFile;

            ExternalModuleHost.ConnectAndRunModule(port, module);
            Console.WriteLine("Terminated.");
        }

        private static Type[] LoadTypesFromAssemblyFile(string fileName) {
            try {
                Type baseClass = typeof(CalculationBase);

                var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                        fileName,
                        sharedTypes: new Type[] { baseClass });

                return loader.LoadDefaultAssembly()
                    .GetExportedTypes()
                    .Where(t => t.IsSubclassOf(baseClass) && !t.IsAbstract)
                    .ToArray();
            }
            catch (Exception exp) {
                Console.Error.WriteLine($"Failed to load calculation types from assembly '{fileName}': {exp.Message}");
                Console.Error.Flush();
                return new Type[0];
            }
        }
    }
}
