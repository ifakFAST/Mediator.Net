// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http.Json;

namespace Ifak.Fast.Mediator.IO.Adapter_PIWebAPI;

[Identify("PI Web API")]
public class PIWebAPI : AdapterBase
{
    private Adapter? config;
    private AdapterCallback? callback;
    private readonly HttpClient httpClient = new(new HttpClientHandler() {
        ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
    });
    private string baseUrl = "";
    private readonly Dictionary<string, ItemInfo> mapId2Info = [];
    private string lastConnectErrMsg = "";
    private readonly List<DataServer> dataServers = [];
    private readonly HashSet<string> allPointWebIdsPathOnly = [];
    private Timestamp timeAllPointsUpdated = Timestamp.Empty;

    private int maxUriLen = 4000;

    record DataServer(string WebID, string Name);

    private record ItemInfo(string Name, string Address, string WebId);

    public override bool SupportsScheduledReading => true;

    public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        this.config = config;
        this.callback = callback;

        baseUrl = config.Address.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) {
            return Task.FromResult<Group[]>([]);
        }

        if (!baseUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
            baseUrl = "https://" + baseUrl;
        }

        httpClient.BaseAddress = new Uri(baseUrl + "/piwebapi/");

        string strMaxUriLen = config.GetConfigByName("MaxUriLen", defaultValue: "4000");
        if (!int.TryParse(strMaxUriLen, out maxUriLen)) {
            PrintErrorLine($"Invalid value for config parameter 'MaxUriLen': '{strMaxUriLen}'");
            maxUriLen = 4000;
        }

        ConfigureAuthentication();

        // Map all data items
        mapId2Info.Clear();
        List<DataItem> allDataItems = config.GetAllDataItems();
        foreach (DataItem item in allDataItems.Where(di => !string.IsNullOrWhiteSpace(di.Address))) {
            mapId2Info[item.ID] = new ItemInfo(
                item.Name,
                item.Address,
                EncodeAddressToPathOnlyWebID(item.Address)
            );
        }

        Task _ = ResolveDataServers();

        return Task.FromResult<Group[]>([]);
    }

    private void ConfigureAuthentication() {
        // Configure authentication based on adapter config
        if (config?.Login != null) {
            var authBytes = Encoding.ASCII.GetBytes($"{config.Login.Value.UserName}:{config.Login.Value.Password}");
            string authHeader = Convert.ToBase64String(authBytes);
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authHeader);
        }
    }

    private async Task<bool> ResolveDataServers() {

        if (dataServers.Count == 0) {

            string dataServerName = config?.GetConfigByName("DataServerName", defaultValue: "") ?? "";

            try {
                var response = await httpClient.GetAsync("dataservers");
                await HandleErrorResponse(response);
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<DataServersResponse>(content);
                if (data?.Items?.Count > 0) {
                    DataServer[] servers = data.Items.Select(ds => new DataServer(ds.WebId, ds.Name)).ToArray();
                    string serverNames = string.Join(", ", servers.Select(ds => ds.Name));
                    if (!string.IsNullOrWhiteSpace(dataServerName)) {
                        DataServer[] matchingServers = servers.Where(ds => string.Compare(dataServerName, ds.Name, ignoreCase: true) == 0).ToArray();
                        if (matchingServers.Length == 0) {
                            throw new Exception($"Data Server '{dataServerName}' not found. Valid values for DataServerName: {serverNames}");
                        }
                        else {
                            DataServer dataServer = matchingServers[0];
                            dataServers.Add(dataServer);
                            ReturnToNormal("Connect", $"Connected to '{baseUrl}' ({dataServer.Name})", connectionUp: true);
                        }
                    }
                    else {
                        dataServers.AddRange(servers);
                        ReturnToNormal("Connect", $"Connected to '{baseUrl}'", connectionUp: true);
                    }
                }
                else {
                    throw new Exception("No PI Data Server found");
                }
            }
            catch (Exception ex) {
                lastConnectErrMsg = $"Failed to resolve data server: {ex.Message}";
                LogWarn("Connect", lastConnectErrMsg, connectionDown: true);
                return false;
            }
        }

        return true;
    }

    private static string EncodeAddressToPathOnlyWebID(string address) {
        const string prefix = "P1DP"; // P = PathOnly; 1 = WebID version 2; DP = PIPoint
        string pathNoPrefix = address.TrimStart('\\');
        string base64Encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(pathNoPrefix.ToUpperInvariant()));
        string httpEncoded = base64Encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{prefix}{httpEncoded}";
    }

    private async Task UpdateAllPointWebIdsPathOnly() {
        var points = await BrowsePoints(logWarn: false);
        allPointWebIdsPathOnly.Clear();
        timeAllPointsUpdated = Timestamp.Now;
        foreach (var point in points) {
            allPointWebIdsPathOnly.Add(point.WebId);
        }
    }

    public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

        bool serverResolvedOK = await ResolveDataServers();

        if (!serverResolvedOK) {
            return BadVTQsFromLastValue(items);
        }

        if (allPointWebIdsPathOnly.Count == 0) {
            await UpdateAllPointWebIdsPathOnly();
        }

        var result = BadVTQsFromLastValue(items);

        var webIds = new List<string>();
        var indices = new List<int>();

        var invalidItems = new List<ItemInfo>();

        for (int i = 0; i < items.Count; i++) {
            if (mapId2Info.TryGetValue(items[i].ID, out var info)) {
                if (allPointWebIdsPathOnly.Contains(info.WebId)) {
                    webIds.Add(info.WebId!);
                    indices.Add(i);
                }
                else {
                    invalidItems.Add(info);
                }
            }
        }

        if (invalidItems.Count == 1) {
            var info = invalidItems[0];
            LogWarn("InvalidPath", $"Invalid path '{info.Address}' for PI DataItem {info.Name}");
        }
        else if (invalidItems.Count > 1) {
            string[] itemsStr = invalidItems.Select(info => info.Name).ToArray();
            string details = string.Join(", ", itemsStr);
            LogWarn("InvalidPath", $"Invalid path for {invalidItems.Count} PI DataItems: {itemsStr[0]} ...", details: details);
        }

        const string urlPrefix = "streamsets/value?webIdType=PathOnly&selectedFields=Items.WebId;Items.Value.Timestamp;Items.Value.Value;Items.Value.Good;Items.Value.Questionable";

        int TakeUntilLimit(int startIdx) {
            int totalLen = "/piwebapi/".Length + urlPrefix.Length;
            int n = webIds.Count;
            for (int i = startIdx; i < n; ++i) {
                string webId = webIds[i];
                int len = webId.Length;
                totalLen += len;
                totalLen += 7; // &webId=
                if (totalLen > maxUriLen) {
                    return i - startIdx;
                }
            }
            return n - startIdx;
        }

        int startIdx = 0;

        while (startIdx < webIds.Count) {

            int n = TakeUntilLimit(startIdx);
            List<string> slice = webIds.GetRange(startIdx, n);

            try {

                string url = urlPrefix + "&webId=" + string.Join("&webId=", slice);

                // Console.WriteLine($"URL Len: {"/piwebapi/".Length + url.Length}  startIdx: {startIdx} n: {n}");

                var resp = await httpClient.GetAsync(url);
                await HandleErrorResponse(resp);

                StreamValuesResponse response = (await resp.Content.ReadFromJsonAsync<StreamValuesResponse>()) ?? new StreamValuesResponse();

                for (int i = 0; i < slice.Count; i++) {
                    string webId = slice[i];
                    int resultIdx = indices[i + startIdx];
                    StreamValueItem it = response.Items[i];
                    if (it.WebId != webId) {
                        LogWarn("IdMismatch", $"Failed to read value for '{webId}'");
                        continue;
                    }
                    StreamValue? value = it.Value;
                    if (value != null) {
                        DataValue? dv = ConvertValue(value.Value);
                        bool timeOK = Timestamp.TryParse(value.Timestamp ?? "", out Timestamp timestamp);
                        Quality quality = value.GetQuality();
                        if (dv.HasValue && timeOK) {
                            VTQ vtq = VTQ.Make(dv.Value, timestamp, quality);
                            result[resultIdx] = vtq;
                        }
                    }
                }
            }
            catch (Exception ex) {
                LogWarn("Read", $"Failed to read values: {ex.Message}");
                if (Timestamp.Now - timeAllPointsUpdated > Duration.FromMinutes(60)) {
                    allPointWebIdsPathOnly.Clear(); // Error might be due to outdated point list (points deleted on server)
                }
            }

            startIdx += n;
        }

        return result;
    }

    private static DataValue? ConvertValue(JsonElement value) {
        string json = value.GetRawText();
        return StdJson.IsValidJson(json) ? DataValue.FromJSON(json) : null;
    }

    public override async Task<string[]> BrowseDataItemAddress(string? idOrNull) {

        var points = await BrowsePoints(logWarn: true);
        return points
            .Select(points => points.Path)
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(p => p.TrimStart('\\'))
            .ToArray();
    }


    private List<PointInfo>? cachedBrowseResult = null;
    private Timestamp? cacheTime = null;
    private const int CacheTimeMinutes = 30;

    private async Task<List<PointInfo>> BrowsePoints(bool logWarn) {

        if (cachedBrowseResult != null && cacheTime.HasValue && Timestamp.Now - cacheTime.Value < Duration.FromMinutes(CacheTimeMinutes)) {
            PrintLine("Returning cached browse result.");
            return cachedBrowseResult;
        }

        cachedBrowseResult = null;
        cacheTime = null;

        await ResolveDataServers();

        List<PointInfo> result = [];
        var sw = System.Diagnostics.Stopwatch.StartNew();

        foreach (var dataServer in dataServers) {

            try {

                const int maxCount = 500;
                int startIndex = 0;

                while (true) {

                    string uri = $"dataservers/{dataServer.WebID}/points?webIdType=PathOnly&selectedFields=Items.Path;Items.WebId;Items.Descriptor;Items.EngineeringUnits&startIndex={startIndex}&maxCount={maxCount}";
                    var response = await httpClient.GetAsync(uri);

                    await HandleErrorResponse(response);

                    PointsResponse? pointsResponse = await response.Content.ReadFromJsonAsync<PointsResponse>();

                    if (pointsResponse?.Items == null || pointsResponse.Items.Count == 0) {
                        break;
                    }

                    result.AddRange(pointsResponse.Items);
                    startIndex += pointsResponse.Items.Count;

                    if (pointsResponse.Items.Count < maxCount) {
                        break;
                    }
                }

                if (logWarn) {
                    ReturnToNormal("Browse", $"Browsed {result.Count} points from '{dataServer.Name}'");
                }
            }
            catch (Exception ex) {
                if (logWarn) {
                    LogWarn("Browse", $"Failed to browse points: {ex.Message}");
                }
            }
        }
        sw.Stop();


        if (sw.Elapsed > TimeSpan.FromSeconds(5)) {
            PrintLine($"Caching browse result for {CacheTimeMinutes} minutes.");
            cachedBrowseResult = result;
            cacheTime = Timestamp.Now;
        }

        Task _ = WritePointsToFile(result);

        return result;
    }

    private static async Task WritePointsToFile(List<PointInfo> points) {
        string path = "Browse_PI.txt";
        using var writer = new System.IO.StreamWriter(path, append: false, encoding: Encoding.UTF8);
        await writer.WriteLineAsync($"Address\tEngineeringUnits\tDescriptor");
        foreach (var point in points) {
            if (!string.IsNullOrWhiteSpace(point.Path)) {
                await writer.WriteLineAsync($"{point.Path.TrimStart('\\')}\t{point.EngineeringUnits}\t{point.Descriptor}");
            }
        }
    }

    private async Task HandleErrorResponse(HttpResponseMessage response) {
        if (response.IsSuccessStatusCode) return;
        ErrorResponse? errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        if (errorResponse?.Errors != null && errorResponse.Errors.Length > 0) {
            throw new Exception(errorResponse.Errors[0]);
        }
        else {
            throw new Exception($"{response.StatusCode}");
        }
    }

    private void PrintErrorLine(string msg) {
        Console.Error.WriteLine(msg);
    }

    private void PrintLine(string msg) {
        Console.Out.WriteLine(msg);
    }

    private void LogWarn(string type, string msg, string? dataItem = null, string? details = null, bool connectionDown = false) {

        var ae = new AdapterAlarmOrEvent() {
            Time = Timestamp.Now,
            Severity = Severity.Warning,
            Connection = connectionDown,
            Type = type,
            Message = msg,
            Details = details ?? "",
            AffectedDataItems = string.IsNullOrEmpty(dataItem) ? [] : [ dataItem ]
        };

        callback?.Notify_AlarmOrEvent(ae);
    }

    private void ReturnToNormal(string type, string msg, bool connectionUp = false, params string[] affectedDataItems) {
        AdapterAlarmOrEvent _event = AdapterAlarmOrEvent.MakeReturnToNormal(type, msg, affectedDataItems);
        _event.Connection = connectionUp;
        callback?.Notify_AlarmOrEvent(_event);
    }

    public override Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {
        var failed = values.Select(v => new FailedDataItemWrite(v.ID, "Write not supported")).ToArray();
        return Task.FromResult(WriteDataItemsResult.Failure(failed));
    }

    public override Task<string[]> BrowseAdapterAddress() => Task.FromResult(Array.Empty<string>());

    public override Task Shutdown() {
        httpClient.Dispose();
        return Task.CompletedTask;
    }

    #region Response Types

    private sealed class DataServersResponse
    {
        public List<DataServerItem>? Items { get; set; }
    }

    private sealed class DataServerItem
    {
        public string WebId { get; set; } = "";
        public string Name { get; set; } = "";
    }

    private sealed class StreamValuesResponse {
        public List<StreamValueItem> Items { get; set; } = [];
    }

    private sealed class StreamValueItem
    {
        public string WebId { get; set; } = "";
        public StreamValue? Value { get; set; }
    }

    private sealed class StreamValue
    {
        public JsonElement Value { get; set; }
        public string? Timestamp { get; set; } = null;
        public bool Good { get; set; } = true;
        public bool Questionable { get; set; } = false;

        public Quality GetQuality() {
            if (Good && Questionable) return Quality.Uncertain;
            return Good ? Quality.Good : Quality.Bad;
        }
    }

    private sealed class PointsResponse
    {
        public List<PointInfo>? Items { get; set; }
    }

    private sealed class ErrorResponse
    {
        public string[] Errors { get; set; } = [];
    }

    private sealed class PointInfo
    {
        public string WebId { get; set; } = "";
        public string Path { get; set; } = "";
        public string Descriptor { get; set; } = "";
        public string EngineeringUnits { get; set; } = "";
    }

    #endregion
}
