// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_Http;

[Identify("HttpGet")]
public class HttpGet : AdapterBase {

    private Dictionary<string, DataItem> mapId2DataItem = new();
    private Adapter config = new Adapter();

    public override bool SupportsScheduledReading => true;

    private readonly HttpClient client = new();

    public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        this.config = config;
        string httpServer = config.Address.Trim();

        if (!string.IsNullOrEmpty(httpServer)) {
            if (!httpServer.StartsWith("http://") && !httpServer.StartsWith("https://")) {
                throw new Exception($"Invalid address '{httpServer}': Missing https:// or http:// prefix");
            }
            PrintLine($"Address: {httpServer}");
            client.BaseAddress = new Uri(httpServer);
        }

        List<DataItem> allDataItems = config.GetAllDataItems();

        this.mapId2DataItem = allDataItems.Where(di => !string.IsNullOrWhiteSpace(di.Address)).ToDictionary(
           item => /* key */ item.ID,
           item => /* val */ item);
   
        return Task.FromResult(Array.Empty<Group>());
    }

    public override Task Shutdown() {
        client.Dispose();
        return Task.CompletedTask;
    }

    public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {
        
        int N = items.Count;

        VTQ[] vtqs = BadVTQsFromLastValue(items);

        Timestamp Now = Timestamp.Now;

        for (int i = 0; i < N; ++i) {

            ReadRequest request = items[i];
            string id = request.ID;
            if (!mapId2DataItem.TryGetValue(id, out DataItem? dataItem)) {
                continue;
            }
            string address = dataItem.Address.Trim();

            try {

                string content = await client.GetStringAsync(address);
                bool json = StdJson.IsValidJson(content);

                DataValue dv;
                if (json) {
                    dv = DataValue.FromJSON(content);
                }
                else {
                    dv = DataValue.FromString(content);
                }

                vtqs[i] = VTQ.Make(dv, Now, Quality.Good);
            }
            catch (Exception exp) {
                PrintLine($"Error reading DataItem {dataItem.ID}: {exp.Message}");
            }
        }

        return vtqs;
    }

    private void PrintLine(string msg) {
        string name = config.Name ?? "";
        Console.WriteLine(name + ": " + msg);
    }

    public override Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {
        int N = values.Count;
        var failed = new FailedDataItemWrite[N];
        for (int i = 0; i < N; ++i) {
            DataItemValue request = values[i];
            string id = request.ID;
            failed[i] = new FailedDataItemWrite(id, "Write not supported.");
        }
        return Task.FromResult(WriteDataItemsResult.Failure(failed));
    }

    public override Task<string[]> BrowseAdapterAddress() {
        return Task.FromResult(Array.Empty<string>());
    }

    public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
        return Task.FromResult(Array.Empty<string>());
    }
}
