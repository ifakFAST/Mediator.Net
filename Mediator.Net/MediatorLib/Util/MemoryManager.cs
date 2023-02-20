// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Ifak.Fast.Mediator.Util
{
    using System;
    using System.IO;
    using System.Threading;

    public static class MemoryManager
    {
        // The two classes RecyclableMemoryStreamManager and RecyclableMemoryStream
        // are copies from the library "Microsoft.IO.RecyclableMemoryStream" version 2.3.1+ (a0b79e7092e2f1a565e9a43b41b84b586aad4ba5).
        // Some events have been disabled that are not used. Some minor performance improvements
        // have been made.

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
        private static long warnCounter = 0;
        private static Timestamp lastWarnTime = Timestamp.Now;

        private static void Res_StreamCreated(object sender, string tag) {
#if DEBUG
            const int Warn_Limit = 12;
#else
            const int Warn_Limit = 100;
#endif
            int count = Interlocked.Add(ref streamCounter, 1);
            if (count > Warn_Limit) {
                if (warnCounter <= 6) {
                    lastWarnTime = Timestamp.Now;
                    Interlocked.Add(ref warnCounter, 1);
                    Console.Error.WriteLine($"More than {Warn_Limit} non-disposed MemoryStreams created: {count} (Tag: {tag})");
                }
                else if ((Timestamp.Now - lastWarnTime) > Duration.FromHours(6)) {
                    warnCounter = 0;
                }
            }
        }

        private static void Res_StreamDisposed(object sender, string tag) {
            Interlocked.Add(ref streamCounter, -1);
        }
    }

    internal sealed class DebugMemStream : MemoryStream
    {
        private const int InitialCapacity = 32 * 1024;
        private bool disposed;
        private readonly string context;

        public string Context => context;

        internal DebugMemStream(string context) : base(InitialCapacity) {
            this.disposed = false;
            this.context = context;
        }

        protected override void Dispose(bool disposing) {

            if (disposed) {
                Console.Error.WriteLine($"Multi Dispose!!! Context: {context}");
                Console.Error.WriteLine(Environment.StackTrace);
                return;
            }
            disposed = true;
        }

        public override void Close() {
            Dispose(true);
        }
    }
}



