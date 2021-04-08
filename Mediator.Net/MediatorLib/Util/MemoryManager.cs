// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Ifak.Fast.Mediator.Util
{
    using System;
    using System.IO;
    using System.Threading;
    using Microsoft.IO;

    public static class MemoryManager
    {
        private static readonly RecyclableMemoryStreamManager manager = SetupMemoryManager();

        public static MemoryStream GetMemoryStream(string context) {
            return manager.GetStream(context);
            //return new MemoryStream(512);
        }

        private static RecyclableMemoryStreamManager SetupMemoryManager() {
            var res = new RecyclableMemoryStreamManager();
            res.StreamCreated += Res_StreamCreated;
            res.StreamDisposed += Res_StreamDisposed;
            return res;
        }

        private static int streamCounter = 0;

        private static void Res_StreamCreated() {
            const int Warn_Limit = 16;
            int count = Interlocked.Add(ref streamCounter, 1);
            if (count > Warn_Limit) {
                Console.Error.WriteLine($"More than {Warn_Limit} non-disposed MemoryStreams created: {count}");
            }
        }

        private static void Res_StreamDisposed() {
            Interlocked.Add(ref streamCounter, -1);
        }
    }
}

// The two classes RecyclableMemoryStreamManager and RecyclableMemoryStream
// are copies from the library "Microsoft.IO.RecyclableMemoryStream" version 1.2.2.
// The copy was necessary because of a problem with .GetBuffer() under .Net Core 2.0
// Some events have been disabled that are not used. Some minor performance improvements
// have been made.

