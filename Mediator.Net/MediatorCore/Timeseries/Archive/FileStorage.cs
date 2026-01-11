// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ifak.Fast.Mediator.Timeseries.Archive;

public sealed class FileStorage(string baseFolder) : StorageBase {

    private static readonly HashSet<char> InvalidChars = Path.GetInvalidFileNameChars()
        .Concat(Path.GetInvalidPathChars())
        .ToHashSet();

    private readonly Dictionary<ChannelRef, string> channelFolders = [];

    private string GetChannelFolder(ChannelRef channel) {

        if (channelFolders.TryGetValue(channel, out string? folder)) {
            return folder;
        }

        string name = channel.VariableName == "Value" ? 
            Sanitize(channel.ObjectID) : 
            Sanitize($"{channel.ObjectID}_{channel.VariableName}");

        string channelFolder = Path.Combine(baseFolder, name);
        channelFolders[channel] = channelFolder;
        return channelFolder;
    }

    private static string Sanitize(string name) {
        var sb = new StringBuilder(name.Length);
        foreach (char c in name) {
            if (InvalidChars.Contains(c) || c == ' ' || c == '%') {
                sb.Append('%');
                sb.Append(((int)c).ToString("X2"));
            }
            else {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private string GetFilePath(ChannelRef channel, int dayNumber) {
        const long MillisecondsPerDay = 86400000L;
        Timestamp t = Timestamp.FromJavaTicks((long)dayNumber * MillisecondsPerDay);
        DateTime dt = t.ToDateTime(); // UTC!
        int year = dt.Year;
        int month = dt.Month;
        int day = dt.Day;
        string channelFolder = GetChannelFolder(channel);
        return Path.Combine(channelFolder, $"{year}.{month:D2}.{day:D2}.bin");
    }

    public override void WriteDayData(ChannelRef channel, int dayNumber, byte[] data) {
        string channelFolder = GetChannelFolder(channel);
        if (!Directory.Exists(channelFolder)) {
            Directory.CreateDirectory(channelFolder);
        }
        string filePath = GetFilePath(channel, dayNumber);
        Retry(() => File.WriteAllBytes(filePath, data));
    }

    public override Stream? ReadDayData(ChannelRef channel, int dayNumber) {
        string filePath = GetFilePath(channel, dayNumber);
        return RetryVal(() => {
            if (File.Exists(filePath)) {
                return File.OpenRead(filePath);
            }
            else {
                return null;
            }
        });
    }

    public override void DeleteDayData(ChannelRef channel, int startDayNumberInclusive, int endDayNumberInclusive) {
        for (int dayNumber = startDayNumberInclusive; dayNumber <= endDayNumberInclusive; dayNumber++) {
            string filePath = GetFilePath(channel, dayNumber);
            Retry(() => { 
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                }
            });
        }
    }

    public override (int dayStart, int dayEnd)? GetStoredDayNumberRange(ChannelRef channel) {
        string channelFolder = GetChannelFolder(channel);
        if (!Directory.Exists(channelFolder)) {
            return null;
        }
        var dayNumbers = new List<int>();
        foreach (var filePath in Directory.GetFiles(channelFolder, "*.bin")) {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            int? dayNumber = ParseFileNameToDayNumber(fileName);
            if (dayNumber.HasValue) {
                dayNumbers.Add(dayNumber.Value);
            }
        }
        if (dayNumbers.Count == 0) {
            return null;
        }
        return (dayNumbers.Min(), dayNumbers.Max());
    }

    private static int? ParseFileNameToDayNumber(string fileName) {
        // Filename format: yyyy.MM.dd (e.g., 2024.01.15)
        string[] parts = fileName.Split('.');
        if (parts.Length != 3) {
            return null;
        }
        if (!int.TryParse(parts[0], out int year) ||
            !int.TryParse(parts[1], out int month) ||
            !int.TryParse(parts[2], out int day)) {
            return null;
        }
        try {
            var dt = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
            Timestamp t = Timestamp.FromDateTime(dt);
            const long MillisecondsPerDay = 86400000L;
            return (int)(t.JavaTicks / MillisecondsPerDay);
        }
        catch {
            return null;
        }
    }

    private static void Retry(Action t) {
        const int N = 10;
        for (int i = 1; i <= N; ++i) {
            try {
                t();
                return;
            }
            catch (Exception) {
                if (i >= N) {
                    throw;
                }
                System.Threading.Thread.Sleep(50 * i);
            }
        }
    }

    private static T RetryVal<T>(Func<T> t) {
        const int N = 10;
        for (int i = 1; i <= N; ++i) {
            try {
                return t();
            }
            catch (Exception) {
                if (i >= N) {
                    throw;
                }
                System.Threading.Thread.Sleep(50 * i);
            }
        }
        return default!;
    }

    public override void Dispose() {
        
    }
}
