using System;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Util
{
    public static class FileWrite
    {
        public static void WriteFileOrThrow(string file, string content, Encoding encoding, int maxRetry = 5, int retryDelayFactor = 50) {

            if (!EnsureFileExists(file, maxRetry, retryDelayFactor)) {
                throw new IOException($"Creating file '{file}' failed.");
            }

            int retryCount = 0;

            while (true) {

                try {

                    string fileTmp = file + ".tmp";

                    File.Delete(fileTmp);

                    File.WriteAllText(fileTmp, content, encoding);

                    File.Replace(
                        sourceFileName: fileTmp,
                        destinationFileName: file,
                        destinationBackupFileName: null);

                    return;
                }
                catch (Exception exp) {
                    retryCount += 1;
                    if (retryCount > maxRetry) {
                        throw new IOException($"Writing file '{file}' failed: {exp.Message}");
                    }
                    Thread.Sleep(retryCount * retryDelayFactor);
                }
            }
        }

        public static bool EnsureFileExists(string file, int maxRetry = 5, int retryDelayFactor = 50) {

            int retryCount = 0;

            while (true) {

                try {

                    if (!File.Exists(file)) {

                        string dir = Path.GetDirectoryName(file);
                        if (!Directory.Exists(dir)) {
                            Directory.CreateDirectory(dir);
                        }

                        File.WriteAllText(file, "", Encoding.UTF8);
                    }

                    return true;
                }
                catch (Exception) {
                    retryCount += 1;
                    if (retryCount > maxRetry) {
                        return false;
                    }
                    Thread.Sleep(retryCount * retryDelayFactor);
                }
            }
        }

    }

    public class FileStore
    {
        private readonly string fileName;
        private readonly BlockingCollection<WorkItem> queue = new BlockingCollection<WorkItem>(new ConcurrentQueue<WorkItem>(), 1000);
        private bool terminated = false;

        public FileStore(string fileName) {
            this.fileName = fileName;

            Thread thread = new Thread(TheThread);
            thread.IsBackground = true;
            thread.Start();
        }

        private void TheThread() {

            while (true) {

                WorkItem it = queue.Take();

                if (it.IsTerminateRequest) {
                    terminated = true;
                    return;
                }

                try {
                    FileWrite.WriteFileOrThrow(fileName, it.Content, Encoding.UTF8, maxRetry: 5, retryDelayFactor: 50);
                    it.Promise.SetResult(true);
                }
                catch (Exception exp) {
                    it.Promise.SetException(exp);
                }
            }
        }

        public Task Save(string content) {
            var promise = new TaskCompletionSource<bool>();
            if (CheckPrecondition(promise)) {
                queue.Add(new WorkItem(promise, terminate: false, content));
            }
            return promise.Task;
        }

        public Task Terminate() {
            if (terminated) return Task.FromResult(true);
            var promise = new TaskCompletionSource<bool>();
            queue.Add(new WorkItem(promise, terminate: true, content: ""));
            queue.CompleteAdding();
            return promise.Task;
        }

        private bool CheckPrecondition(TaskCompletionSource<bool> promise) {
            if (terminated) {
                promise.SetException(new Exception("FileStore terminated"));
                return false;
            }
            return true;
        }

        private class WorkItem
        {
            public TaskCompletionSource<bool> Promise { get; private set; }
            public bool IsTerminateRequest { get; private set; }
            public string Content { get; private set; }

            public WorkItem(TaskCompletionSource<bool> promise, bool terminate, string content) {
                Promise = promise;
                IsTerminateRequest = terminate;
                Content = content;
            }
        }
    }

}
