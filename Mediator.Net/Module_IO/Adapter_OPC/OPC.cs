// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Ifak.Fast.Mediator.IO.Adapter_OPC
{
    [Identify("OPC")]
    public class OPC : ExternalAdapter
    {
        public override bool SupportsScheduledReading => true;

        protected override string GetCommand(Mediator.Config config) {

            string baseDir = Path.GetDirectoryName(GetType().Assembly.Location) ?? "";

            string local = Path.Combine(baseDir, @"OPC\OPC_Adapter.exe");
            if (File.Exists(local)) {
                return local;
            }
            else {
                string path = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\..\OPC\Adapter\bin\Release\OPC_Adapter.exe"));
                if (!File.Exists(path)) {
                    Console.Error.WriteLine("File not found: " + path);
                }
                return path;
            }
        }

        protected override string GetArgs(Mediator.Config config) {
            return "{PORT}";
        }
    }
}
