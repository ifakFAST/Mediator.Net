// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_TextFile;

[Identify("ReadFromTextFile")]
public class ReadFromTextFile : AdapterBase
{
    private sealed class Item {
        public string Address = "";
        public VTQ Value;
        public DataValue DefaultValue;

        public Item(string address, VTQ value, DataValue defaultValue) {
            Address = address;
            Value = value;
            DefaultValue = defaultValue;
        }
    }

    private readonly Dictionary<string, Item> values = new();

    private string fileName = "";
    private string fileExtension = "";
    private bool running = false;

    public override bool SupportsScheduledReading => true;

    public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        fileName = config.Address.Trim();

        if (fileName != "") {

            fileName = Path.GetFullPath(fileName);

            if (!File.Exists(fileName)) {
                throw new Exception($"File '{fileName}' not found");
            }

            fileExtension = Path.GetExtension(fileName).ToLowerInvariant().TrimStart('.');
        }

        List<DataItem> dataItems = config.GetAllDataItems().Where(it => it.Read).ToList();

        var t = Timestamp.Now.TruncateMilliseconds();

        foreach (DataItem di in dataItems) {
            string address = string.IsNullOrWhiteSpace(di.Address) ? di.ID : di.Address;
            DataValue v = DataValue.FromDataType(di.Type, di.Dimension);
            VTQ vtq = VTQ.Make(v, t, Quality.Bad);
            values[di.ID] = new Item(address, vtq, v);
        }

        if (fileName != "") {
            UpdateValuesFromFileContent();
        }

        return Task.FromResult(new Group[0]);
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

        DateTime time = File.GetLastWriteTimeUtc(fileName);
        string   text = File.ReadAllText(fileName, Encoding.UTF8);

        Func<string, string> id2Value = fileExtension switch {
            "txt"  => ReadFromText(text),
            "csv"  => ReadFromCSV(text),
            "json" => ReadFromJSON(text),
            _ => throw new Exception($"Unsupported file extension '.{fileExtension}'. Expected .txt or .csv or .json")
        };

        UpdateValues(time, id2Value);
    }

    private static Func<string, string> ReadFromJSON(string json) {
        JObject obj = StdJson.JObjectFromString(json);
        return id => {
            JToken tokenValue = obj[id] ?? throw new Exception($"No value for '{id}'");
            return tokenValue.ToString(Json.Formatting.None);
        };
    }

    private static Func<string, string> ReadFromCSV(string text) {

        static (string id, string value)? IdValueFromLine(string line) {
            string[] columns = line.Split(',');
            if (columns.Length < 2) return null;
            string id = columns[0].Trim();
            string value = columns[1].Trim();
            return (id, value);
        }

        return ReadFromLines(text, IdValueFromLine);
    }

    private static Func<string, string> ReadFromText(string text) {

        static (string id, string value)? IdValueFromLine(string line) {
            int idx = line.IndexOf('=');
            if (idx <= 0) return null;
            string id = line[..idx].Trim();
            string value = line[(idx + 1)..].Trim();
            return (id, value);
        }

        return ReadFromLines(text, IdValueFromLine);
    }

    private static Func<string, string> ReadFromLines(string text, Func<string, (string id, string value)?> line2IdValue) {

        var id2Value = text
            .Split('\n')
            .Select(line2IdValue)
            .Where(idValue => idValue.HasValue)
            .Where(idValue => StdJson.IsValidJson(idValue!.Value.value))
            .ToDictionary(
                idValue => idValue!.Value.id,
                idValue => idValue!.Value.value);

        return id => id2Value[id];
    }

    private void UpdateValues(DateTime fileModified, Func<string, string> id2Value) {
        Timestamp tFileModified = Timestamp.FromDateTime(fileModified);
        foreach (var item in values) {
            Item it = item.Value;
            string id = it.Address;
            try {
                string value = id2Value(id);
                it.Value = VTQ.Make(DataValue.FromJSON(value), tFileModified, Quality.Good);
            }
            catch (Exception) {
                it.Value = VTQ.Make(it.DefaultValue, Timestamp.Now, Quality.Bad);
            }
        }
    }

    public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
        return Task.FromResult(new string[0]);
    }

    public override Task<string[]> BrowseAdapterAddress() {

        string directory = Environment.CurrentDirectory;
        int dirLen = directory.Length;

        var files = Directory.GetFiles(directory, "*.json")
           .Concat(Directory.GetFiles(directory, "*.txt"))
           .Concat(Directory.GetFiles(directory, "*.csv"))
           .Select(f => f.Substring(dirLen + 1))
           .ToArray();

        return Task.FromResult(files);
    }

    public override Task Shutdown() {
        values.Clear();
        running = false;
        return Task.FromResult(true);
    }

    public override Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

        int N = items.Count;
        VTQ[] res = new VTQ[N];

        for (int i = 0; i < N; ++i) {
            ReadRequest request = items[i];
            string id = request.ID;
            if (values.ContainsKey(id)) {
                res[i] = values[id].Value;
            }
            else {
                res[i] = new VTQ(Timestamp.Now, Quality.Bad, request.LastValue.V);
            }
        }

        return Task.FromResult(res);
    }

    public override Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> writeValues, Duration? timeout) {

        return Task.FromResult(WriteDataItemsResult.OK);
    }
}
