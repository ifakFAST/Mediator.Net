// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;

namespace Ifak.Fast.Mediator.Calc;

public readonly struct LogEntry {
    public uint ID { get; init; }
    public LogLevel Level { get; init; }
    public string Line { get; init; }
}

public sealed class CalcLogBuffer(int capacity = 500)
{
    private readonly LogEntry[] buffer = new LogEntry[capacity];
    private int head = 0;
    private int count = 0;
    private uint lastUsedID = 0;
    private readonly Lock lockObj = new();

    public void AddWithTimestamp(string line, LogLevel level) {
        DateTime dt = AppTimeZone.ConvertToLocalTime(Timestamp.Now);
        string timestampedLine = $"[{dt:yyyy-MM-dd HH:mm:ss}]  {line}";
        lock (lockObj) {
            lastUsedID++;
            buffer[head] = new LogEntry {
                ID = lastUsedID,
                Line = timestampedLine,
                Level = level
            };
            head = (head + 1) % buffer.Length;
            if (count < buffer.Length) count++;
            if (lastUsedID == uint.MaxValue) {
                lastUsedID = 0; // Wrap around to prevent overflow
                Clear();
            }
        }
    }

    public IReadOnlyList<LogEntry> GetAllLines() {
        lock (lockObj) {
            return [.. EnumerateAllEntriesUnsafe()];
        }
    }

    public IReadOnlyList<LogEntry> GetLinesSince(uint sinceID) {
        lock (lockObj) {
            List<LogEntry> lines = [];
            foreach (LogEntry entry in ReverseEnumerateAllEntriesUnsafe()) {
                if (entry.ID > sinceID) {
                    lines.Add(entry);
                }
                else {
                    break;
                }
            }
            lines.Reverse();
            return lines;
        }
    }

    public void Clear() {
        lock (lockObj) {
            count = 0;
            head = 0;
            Array.Clear(buffer);
        }
    }

    private IEnumerable<LogEntry> EnumerateAllEntriesUnsafe() {
        int start = (head - count + buffer.Length) % buffer.Length;
        for (int i = 0; i < count; i++) {
            yield return buffer[(start + i) % buffer.Length];
        }
    }

    private IEnumerable<LogEntry> ReverseEnumerateAllEntriesUnsafe() {
        int start = (head - 1 + buffer.Length) % buffer.Length;
        for (int i = 0; i < count; i++) {
            yield return buffer[(start - i + buffer.Length) % buffer.Length];
        }
    }

}
