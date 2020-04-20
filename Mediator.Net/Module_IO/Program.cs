// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

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
            ExternalModuleHost.ConnectAndRunModule(port, module);
            Console.WriteLine("Terminated.");
        }
    }
}
