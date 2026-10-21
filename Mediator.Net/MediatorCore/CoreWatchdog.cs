// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using NLog;

namespace Ifak.Fast.Mediator;

// Detects and logs stalls of the Mediator core, so that a hanging core can be distinguished from
// a hanging machine in the log file. Runs on a dedicated thread (independent of the thread pool and
// of the main thread) and checks once per second:
//   - Process: the watchdog thread itself was not scheduled in time
//     => the whole process (and probably the machine) stalled, e.g. GC pause, CPU, memory paging, VM pause.
//   - ThreadPool: a work item queued to the .NET thread pool did not start in time
//     => thread pool starvation (affects Kestrel, i.e. handling of HTTP requests).
//   - MainThread: a callback posted to the core's main thread did not run in time
//     => the main thread is blocked; all client requests (e.g. from modules) are processed there.
// A thread pool or main thread stall is logged once when it exceeds the threshold and once when it is over
// (with its duration), or only once if it was over before the next check. A process stall can only be
// detected afterwards, so it is logged once when it is over.
internal sealed class CoreWatchdog(SynchronizationContext mainThread, TimeSpan stallThreshold) {
    private static readonly Logger logger = LogManager.GetLogger("Watchdog");

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly TimeSpan stallThreshold = stallThreshold;
    private readonly SynchronizationContext mainThread = mainThread;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private volatile bool running = false;

    private readonly Probe probeThreadPool = new("ThreadPool", "Thread pool did not start a work item");
    private readonly Probe probeMainThread = new("MainThread", "Main thread of the core did not respond");

    public void Start() {
        if (running) return;
        if (stallThreshold <= TimeSpan.Zero) {
            logger.Info("Watchdog disabled (WatchdogStallThreshold is 0 s).");
            return;
        }
        running = true;
        logger.Info($"Watchdog started (stall threshold: {Seconds(stallThreshold)}).");
        var thread = new Thread(Run) {
            IsBackground = true,
            Name = "CoreWatchdog",
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    public void Stop() {
        running = false;
    }

    private void Run() {

        while (running) {

            // Measure only the sleep, so that time spent below (e.g. in a blocking log write)
            // is not mistaken for a stall of the whole process:
            TimeSpan tBefore = clock.Elapsed;
            Thread.Sleep(CheckInterval);
            TimeSpan tNow = clock.Elapsed;
            TimeSpan overshoot = tNow - tBefore - CheckInterval;

            // Stop() may have been called during the sleep. Do not start new probes then, because
            // the main thread no longer runs posted callbacks after the core terminated:
            if (!running) break;

            try {
                // Do not count the time the whole process stalled as a stall of the probes, even if it is
                // below the threshold (otherwise e.g. a 4.5 s process stall plus the 1 s check interval would
                // be reported as a thread pool and main thread stall at a 5 s threshold).
                // Already reported stalls are left as they are, so that their duration stays correct:
                if (overshoot > TimeSpan.Zero) {
                    ExcludeProcessStall(probeThreadPool, overshoot);
                    ExcludeProcessStall(probeMainThread, overshoot);
                }

                if (overshoot > stallThreshold) {
                    logger.Warn($"Process: Watchdog thread was not scheduled for {Seconds(overshoot)} (process or machine stalled, e.g. GC pause, CPU overload, memory paging, VM pause). {Diagnostics()}");
                }

                CheckProbe(probeThreadPool, tNow, start: () => ThreadPool.UnsafeQueueUserWorkItem(_ => probeThreadPool.Done(clock.Elapsed), null));
                CheckProbe(probeMainThread, tNow, start: () => mainThread.Post(_ => probeMainThread.Done(clock.Elapsed), null));
            }
            catch (Exception exp) {
                if (running) {
                    logger.Warn($"Watchdog exception: {exp.Message}");
                }
            }
        }
    }

    private static void ExcludeProcessStall(Probe probe, TimeSpan stall) {
        // Also shift completed probes: their callback may have run during the process stall,
        // before the watchdog thread was scheduled again:
        if (!probe.Reported) {
            probe.Started += stall;
        }
    }

    private void CheckProbe(Probe probe, TimeSpan tNow, Action start) {

        if (probe.Pending) {
            TimeSpan waiting = tNow - probe.Started;
            if (waiting > stallThreshold && !probe.Reported) {
                probe.Reported = true;
                logger.Warn($"{probe.Name}: {probe.Description} for {Seconds(waiting)}. {Diagnostics()}");
            }
            return;
        }

        if (probe.Reported) {
            TimeSpan duration = probe.Completed - probe.Started;
            logger.Warn($"{probe.Name}: Responsive again after {Seconds(duration)}.");
            probe.Reported = false;
        }
        else {
            // The stall exceeded the threshold but was over before this check:
            TimeSpan duration = probe.Completed - probe.Started;
            if (duration > stallThreshold) {
                logger.Warn($"{probe.Name}: {probe.Description} for {Seconds(duration)} (already responsive again). {Diagnostics()}");
            }
        }

        probe.Started = tNow;
        probe.Pending = true;
        try {
            start();
        }
        catch {
            // The probe callback will never run, so do not leave the probe pending (false stall report):
            probe.Pending = false;
            throw;
        }
    }

    private static string Seconds(TimeSpan t) => t.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";

    private TimeSpan lastGcPause = GC.GetTotalPauseDuration();

    private string Diagnostics() {
        try {
            ThreadPool.GetAvailableThreads(out int availWorker, out int availIO);
            ThreadPool.GetMaxThreads(out int maxWorker, out int maxIO);

            TimeSpan gcPause = GC.GetTotalPauseDuration();
            TimeSpan gcPauseDelta = gcPause - lastGcPause;
            lastGcPause = gcPause;

            GCMemoryInfo mem = GC.GetGCMemoryInfo();
            string memLoad = mem.MemoryLoadBytes > 0 && mem.TotalAvailableMemoryBytes > 0
                ? (100.0 * mem.MemoryLoadBytes / mem.TotalAvailableMemoryBytes).ToString("0", CultureInfo.InvariantCulture) + "%"
                : "n/a";

            using Process p = Process.GetCurrentProcess();

            return $"ThreadPool: {ThreadPool.ThreadCount} threads, {ThreadPool.PendingWorkItemCount} pending items, {maxWorker - availWorker} busy workers, {maxIO - availIO} busy IO; " +
                   $"GC pause since last report: {gcPauseDelta.TotalMilliseconds:0} ms; " +
                   $"Process: working set {p.WorkingSet64 / (1024 * 1024)} MB, managed heap {GC.GetTotalMemory(false) / (1024 * 1024)} MB; " +
                   $"Machine memory load (at last GC): {memLoad}";
        }
        catch (Exception exp) {
            return $"(Diagnostics failed: {exp.Message})";
        }
    }

    private sealed class Probe(string name, string description) {
        public string Name { get; } = name;
        public string Description { get; } = description;
        public TimeSpan Started;
        public bool Reported;

        private volatile bool pending;
        private long completedTicks;

        public bool Pending {
            get => pending;
            set => pending = value;
        }

        public TimeSpan Completed => TimeSpan.FromTicks(Interlocked.Read(ref completedTicks));

        public void Done(TimeSpan t) {
            Interlocked.Exchange(ref completedTicks, t.Ticks);
            pending = false;
        }
    }
}
