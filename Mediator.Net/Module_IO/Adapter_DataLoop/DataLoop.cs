// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_DataLoop;

[Identify("DataLoop")]
public class DataLoop : AdapterBase {

    private record Item(string Address);
    private readonly Dictionary<string, Item> mapId2Item = new();

    private CsvContent content = new(Array.Empty<string>(), Array.Empty<Row>());
    private TimeSpan cycleLen = TimeSpan.Zero;
    private DateTime[] rowTimestamps = Array.Empty<DateTime>();

    private DateTime dtAnchor = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Local);
    private TimeUnit timeUnit = TimeUnit.Day;

    private string fileName = "";
    private bool running = false;

    private Adapter config = new Adapter();

    public override bool SupportsScheduledReading => true;

    public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        this.config = config;
        fileName = config.Address.Trim();

        if (fileName != "") {

            fileName = Path.GetFullPath(fileName);

            if (!File.Exists(fileName)) {
                throw new Exception($"File '{fileName}' not found");
            }
        }

        List<DataItem> dataItems = config.GetAllDataItems().Where(it => it.Read).ToList();

        var t = Timestamp.Now.TruncateMilliseconds();

        foreach (DataItem di in dataItems) {
            string address = string.IsNullOrWhiteSpace(di.Address) ? di.ID : di.Address;
            mapId2Item[di.ID] = new Item(address);
        }

        if (fileName != "") {
            UpdateValuesFromFileContent();
        }

        return Task.FromResult(Array.Empty<Group>());
    }

    public override void StartRunning() {
        if (fileName == "") { return; }
        running = true;
        Task _ = CheckForFileModification();
    }

    private async Task CheckForFileModification() {
        DateTime lastWriteTime = File.GetLastWriteTimeUtc(fileName);
        while (running) {
            await Task.Delay(2000);
            DateTime time = File.GetLastWriteTimeUtc(fileName);
            if (time != lastWriteTime) {
                lastWriteTime = time;
                try {
                    UpdateValuesFromFileContent();
                }
                catch (Exception exp) {
                    Console.Error.WriteLine($"Error reading file '{fileName}': {exp.Message}");
                }
            }
        }
    }

    private void UpdateValuesFromFileContent() {

        content = CSV.ReadFromFile(fileName, anchor: dtAnchor, unit: timeUnit);
        content.FillGaps();

        rowTimestamps = content.Rows.Select(row => row.Time).ToArray();

        if (!rowTimestamps.IsSorted()) {
            throw new Exception($"Timestamps in file '{fileName}' are not sorted");
        }

        if (content.Rows.Count < 2) {
            cycleLen = TimeSpan.Zero;
        }
        else {

            Row r0 = content.Rows.First();
            Row r1 = content.Rows.Last();
            TimeSpan diff = r1.Time - r0.Time;

            cycleLen = LargestDivisorOrMultipleOf24Hours(diff);
        }

        PrintLine($"Read {content.Rows.Count} rows from file '{fileName}'");
        PrintLine($"Cycle length: {Duration.FromTimeSpan(cycleLen)}");
        if (content.Rows.Count > 0) {
            DateTime t = MapNow2EffectiveTimeinCsv(content.Rows[0], cycleLen, out DateTime now);
            int idxRow = GetRowIdxFromTime(t);
            Row row = content.Rows[idxRow];
            PrintLine($"Effective time: Now={now} maps to t={t} in CSV. Greatest time in CSV <= t: {row.Time}");
        }
    }

    private static TimeSpan LargestDivisorOrMultipleOf24Hours(TimeSpan input) {

        const int secondsInDay = 24 * 60 * 60;
        int totalSecondsInput = (int)input.TotalSeconds;

        // Check if the total seconds of input is a divisor of a 24-hour day
        if (secondsInDay % totalSecondsInput == 0) {
            return input;
        }

        // If more than 24 hours, find the largest multiple of 24 that is smaller or equal to the input
        if (input.TotalSeconds > secondsInDay) {
            int multiple = totalSecondsInput / secondsInDay;
            return TimeSpan.FromDays(multiple);
        }

        // Divisors of 24 hours in seconds (excluding 24 hours itself)
        int[] divisorsOfADay = {
            43200, // 12 hours
            28800, // 8 hours
            21600, // 6 hours
            14400, // 4 hours
            10800, // 3 hours
            7200,  // 2 hours
            3600,  // 1 hour
            1800,  // 30 minutes
            1200,  // 20 minutes
            900,   // 15 minutes
            600,   // 10 minutes
            300,   // 5 minutes
            240,   // 4 minutes
            180,   // 3 minutes
            120,   // 2 minutes
            60,    // 1 minute
            30,
            20,
            15,
            10,
            5,
            4,
            3,
            2,
            1 };

        // Find the largest divisor of 24 hours that is smaller or equal to the input TimeSpan
        foreach (int divisor in divisorsOfADay) {
            if (totalSecondsInput >= divisor) {
                return TimeSpan.FromSeconds(divisor);
            }
        }

        // In case no divisor is found, which should not happen, return the original input
        return input;
    }

    public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
        return Task.FromResult(content.Header);
    }

    public override Task<string[]> BrowseAdapterAddress() {

        string directory = Environment.CurrentDirectory;
        int dirLen = directory.Length;

        var files = Directory.GetFiles(directory, "*.csv")
           .Select(f => f.Substring(dirLen + 1))
           .ToArray();

        return Task.FromResult(files);
    }

    public override Task Shutdown() {
        mapId2Item.Clear();
        running = false;
        return Task.FromResult(true);
    }

    public override Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

        int N = items.Count;
        VTQ[] res = new VTQ[N];

        IList<Row> rows = content.Rows;

        if (rows.Count == 0) {
            Timestamp tNow = Timestamp.Now;
            for (int i = 0; i < N; ++i) {
                ReadRequest request = items[i];
                res[i] = VTQ.Make(request.LastValue.V, tNow, Quality.Bad);
            }
            return Task.FromResult(res);
        }

        DateTime t = MapNow2EffectiveTimeinCsv(rows[0], cycleLen, out DateTime _);
        int idxRow = GetRowIdxFromTime(t);

        Row row = rows[idxRow];
        DateTime rowTime = row.Time;
        // Map rowTime to current time:
        
        Timestamp timestamp = Timestamp.FromDateTime(row.Time);

        for (int i = 0; i < N; ++i) {
            ReadRequest request = items[i];
            try {
                Item it = mapId2Item[request.ID];
                int idxHeader = content.Header.FindIndexOrThrow(column => column == it.Address);
                DataValue dv = row.Values[idxHeader];
                res[i] = VTQ.Make(dv, timestamp, Quality.Good);
            }
            catch (Exception) {
                res[i] = VTQ.Make(request.LastValue.V, timestamp, Quality.Bad);
            }
        }

        return Task.FromResult(res);
    }

    private static DateTime MapNow2EffectiveTimeinCsv(Row firstRow, TimeSpan cycleLength, out DateTime now) {
        
        DateTime tStart = firstRow.Time;
        now = tStart.Kind == DateTimeKind.Utc ? DateTime.UtcNow : DateTime.Now;
        // truncate now to seconds:
        now = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second, now.Kind);

        long cycleLen = (long)cycleLength.TotalSeconds;
        long offSeconds = cycleLen == 0 ? 0 : ((long)(now - tStart).TotalSeconds) % cycleLen;
        offSeconds = offSeconds < 0 ? offSeconds + cycleLen : offSeconds;

        return tStart + TimeSpan.FromSeconds(offSeconds);
    }

    private int GetRowIdxFromTime(DateTime t) {
        int idxRow = Array.BinarySearch(rowTimestamps, t);
        if (idxRow < 0) { // not found
            idxRow = ~idxRow; // get index of first element larger than t
            idxRow--;
        }
        return idxRow;
    }

    public override Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> writeValues, Duration? timeout) {
        FailedDataItemWrite[] err = writeValues
            .Select(v => new FailedDataItemWrite(v.ID, "Write to CSV file unsupported"))
            .ToArray();
        return Task.FromResult(WriteDataItemsResult.Failure(err));
    }

    private void PrintLine(string msg) {
        string name = config.Name ?? "";
        Console.WriteLine(name + ": " + msg);
    }
}
