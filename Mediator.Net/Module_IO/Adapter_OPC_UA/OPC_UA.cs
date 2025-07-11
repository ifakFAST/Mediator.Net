﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Workstation.ServiceModel.Ua;
using Workstation.ServiceModel.Ua.Channels;
using Ifak.Fast.Mediator.Util;
using Microsoft.Extensions.Logging;

namespace Ifak.Fast.Mediator.IO.Adapter_OPC_UA;

[Identify("OPC UA")]
public class OPC_UA : AdapterBase
{
    private ApplicationDescription appDescription = new();
    private ICertificateStore? certificateStore;
    private string certificateLocation = "";
    internal ClientSessionChannel? connection;

    private Adapter config = new();
    private AdapterCallback? callback;
    internal Dictionary<string, ItemInfo> mapId2Info = [];

    public override bool SupportsScheduledReading => true;

    private string lastConnectErrMsg = "";
    internal bool excludeUnderscore = true;

    private AlarmManager alarmConnectivity = new(activationDuration: Duration.FromMinutes(5));

    private Duration timeout = Duration.FromSeconds(15);
    private Duration maxAge = Duration.FromSeconds(0); // 0 means always read from down stream device (no caching)
    private bool validateRemoteCertificates = false;

    // Auto-creation configuration
    private bool autoCreateDataItems = false;
    private string autoCreateDataItems_RootNode = "Objects";
    private int autoCreateDataItems_MaxDepth = 20;
    private string autoCreateDataItems_ExcludeNamespaces = "0";
    private Duration autoCreateDataItems_BrowseInterval = Duration.FromMinutes(5);
    private HashSet<int> excludedNamespaces = [];

    private string browse_RootId = "";
    private string browse_RootName = "";

    // Auto-discovery components
    private AutoDiscoveryManager? discoveryManager;
    
    private LoggerFactory loggerFactory = new("UA:", LogLevel.Debug);
    private Logger logger = new("UA:", LogLevel.Debug);

    private static readonly Dictionary<string, string> mapSecurityPolicies = new() {
        { "",                                         "" },
        { "Any".ToLowerInvariant(),                   "" },
        { "None".ToLowerInvariant(),                  SecurityPolicyUris.None },
        { "Basic128Rsa15".ToLowerInvariant(),         SecurityPolicyUris.Basic128Rsa15 },
        { "Basic256".ToLowerInvariant(),              SecurityPolicyUris.Basic256 },
        { "Https".ToLowerInvariant(),                 SecurityPolicyUris.Https },
        { "Basic256Sha256".ToLowerInvariant(),        SecurityPolicyUris.Basic256Sha256 },
        { "Aes128_Sha256_RsaOaep".ToLowerInvariant(), SecurityPolicyUris.Aes128_Sha256_RsaOaep },
        { "Aes256_Sha256_RsaPss".ToLowerInvariant(),  SecurityPolicyUris.Aes256_Sha256_RsaPss },
    };

    private static readonly string[] securityPolicies = [
        "Any",
        "None",
        "Basic128Rsa15",
        "Basic256",
        "Https",
        "Basic256Sha256",
        "Aes128_Sha256_RsaOaep",
        "Aes256_Sha256_RsaPss",
    ];

    public override async Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        this.config = config;
        this.callback = callback;
        this.alarmConnectivity = new(activationDuration: config.ConnectionRetryTimeout);

        string strLogLevel = config.GetConfigByName("LogLevel", defaultValue: "info").ToLowerInvariant();
        LogLevel logLevel = strLogLevel switch {
            "all" => LogLevel.Trace,
            "trace" => LogLevel.Trace,
            "debug" => LogLevel.Debug,
            "information" => LogLevel.Information,
            "info" => LogLevel.Information,
            "warning" => LogLevel.Warning,
            "warn" => LogLevel.Warning,
            "error" => LogLevel.Error,
            "err" => LogLevel.Error,
            "critical" => LogLevel.Critical,
            _ => LogLevel.Information,
        };

        loggerFactory = new LoggerFactory($"{config.Name}:", logLevel);
        logger = loggerFactory.CreateLogger();

        browse_RootId = config.GetConfigByName("BrowseRoot_ID", ObjectIds.ObjectsFolder);
        browse_RootName = config.GetConfigByName("BrowseRoot_Name", "Objects");

        string strExcludeUnderscore = config.GetConfigByName("ExcludeUnderscoreNodes", defaultValue: "true").ToLowerInvariant();
        excludeUnderscore = strExcludeUnderscore == "true";

        string strTimeout = config.GetConfigByName("Timeout", defaultValue: "15 s");
        if (!Duration.TryParse(strTimeout, out timeout)) {
            PrintErrorLine($"Invalid value for config parameter 'Timeout': '{strTimeout}'");
        }

        string strMaxAge = config.GetConfigByName("MaxAge", defaultValue: "0 s");
        if (!Duration.TryParse(strMaxAge, out maxAge)) {
            PrintErrorLine($"Invalid value for config parameter 'MaxAge': '{strMaxAge}'");
        }

        string strValidateRemoteCertificates = config.GetConfigByName("ValidateRemoteCertificates", defaultValue: "false").ToLowerInvariant();
        this.validateRemoteCertificates = strValidateRemoteCertificates == "true";

        // Parse auto-creation configuration
        string strAutoCreateDataItems = config.GetConfigByName("AutoCreateDataItems", defaultValue: "false").ToLowerInvariant();
        this.autoCreateDataItems = strAutoCreateDataItems == "true";

        this.autoCreateDataItems_RootNode = config.GetConfigByName("AutoCreateDataItems_RootNode", defaultValue: "Objects").Trim();

        string strMaxDepth = config.GetConfigByName("AutoCreateDataItems_MaxDepth", defaultValue: "20");
        if (!int.TryParse(strMaxDepth, out this.autoCreateDataItems_MaxDepth) || this.autoCreateDataItems_MaxDepth < 1) {
            PrintErrorLine($"Invalid value for config parameter 'AutoCreateDataItems_MaxDepth': '{strMaxDepth}'. Using default: 20");
            this.autoCreateDataItems_MaxDepth = 20;
        }

        this.autoCreateDataItems_ExcludeNamespaces = config.GetConfigByName("AutoCreateDataItems_ExcludeNamespaces", defaultValue: "0");


        string strBrowseInterval = config.GetConfigByName("AutoCreateDataItems_BrowseInterval", defaultValue: "5 min").Trim();
        if (!Duration.TryParse(strBrowseInterval, out Duration browseInterval)) {
            PrintErrorLine($"Invalid value for config parameter 'AutoCreateDataItems_BrowseInterval': '{strBrowseInterval}'. Using default: 5 min");
            browseInterval = Duration.FromMinutes(5);
        }
        this.autoCreateDataItems_BrowseInterval = browseInterval;

        // Parse excluded namespaces
        this.excludedNamespaces = [];
        string[] nsStrings = this.autoCreateDataItems_ExcludeNamespaces.Split(',');
        foreach (string nsStr in nsStrings) {
            if (int.TryParse(nsStr.Trim(), out int ns)) {
                this.excludedNamespaces.Add(ns);
            }
        }

        const string appName = "Mediator.IO.OPC_UA";
        appDescription = new ApplicationDescription {
            ApplicationName = appName,
            ApplicationUri = $"urn:{Dns.GetHostName()}:{appName}",
            ApplicationType = ApplicationType.Client
        };

        List<DataItem> allDataItems = config.GetAllDataItems();

        this.mapId2Info = allDataItems.Where(di => !string.IsNullOrEmpty(di.Address)).ToDictionary(
           item => /* key */ item.ID,
           item => /* val */ new ItemInfo(item.ID, item.Name, item.Type, item.Dimension, item.Address));

        PrintLine($"Address: {config.Address}");

        await TryConnect(reportFailureImmediatelly: true);

        // Initialize auto-discovery if enabled
        if (autoCreateDataItems) {
            
            this.discoveryManager = new AutoDiscoveryManager(
                this,
                autoCreateDataItems_RootNode,
                autoCreateDataItems_MaxDepth,
                autoCreateDataItems_BrowseInterval,
                excludedNamespaces,
                callback,
                logger);

            _ = discoveryManager.StartAsync();

            PrintLine($"Auto-creation enabled: Root={autoCreateDataItems_RootNode}, MaxDepth={autoCreateDataItems_MaxDepth}, ExcludeNS=[{autoCreateDataItems_ExcludeNamespaces}]");
        }

        return [];
    }

    internal async Task<bool> TryConnect(bool reportFailureImmediatelly = false) {

        if (connection != null) {
            return true;
        }

        if (string.IsNullOrEmpty(config.Address)) {
            lastConnectErrMsg = "No address configured";
            return false;
        }

        try {

            if (certificateStore == null) {

                var pkiPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ifakFAST.IO.OPC_UA",
                    "pki");

                const string Config_PkiDir = "PkiDir";

                if (config.Config.Any(nv => nv.Name == Config_PkiDir)) {
                    pkiPath = Path.GetFullPath(config.Config.First(nv => nv.Name == Config_PkiDir).Value.Trim());
                }

                Console.WriteLine($"Location of OPC UA certificate store: {pkiPath}");

                certificateStore = new DirectoryStore(pkiPath, acceptAllRemoteCertificates: !validateRemoteCertificates, createLocalCertificateIfNotExist: true);
                certificateLocation = Path.Combine(pkiPath, "own", "certs");
            }

            string sec = config.GetConfigByName("Security", defaultValue: "None").ToLowerInvariant();

            if (!mapSecurityPolicies.ContainsKey(sec)) {
                string strKeys = string.Join(", ", securityPolicies);
                throw new Exception($"Invalid value for config setting 'Security': {sec}. Expected any of: {strKeys}");
            }

            var endpoint = new EndpointDescription {
                EndpointUrl = config.Address,
                SecurityPolicyUri = mapSecurityPolicies[sec],
            };

            IUserIdentity identity = GetIdentity();

            ClientSessionChannelOptions opts = new() {
                TimeoutHint = (uint)timeout.TotalMilliseconds,
            };

            var channel = new MyUaChannel(
                        localDescription: appDescription,
                        certificateStore: certificateStore,
                        userIdentity: identity,
                        remoteEndpoint: endpoint,
                        loggerFactory: loggerFactory,
                        options: opts);

            await channel.OpenAsync();

            this.connection = channel;
            lastConnectErrMsg = "";

            PrintLine($"Connected to OPC UA server at {channel.RemoteEndpoint.EndpointUrl}. Timeout: {timeout}, MaxAge: {maxAge}");

            ItemInfo[] nodesNeedingResolve = mapId2Info.Values.Where(n => n.Node == null).ToArray();
            if (nodesNeedingResolve.Length > 0) {

                PrintLine($"Resolving node ids for {nodesNeedingResolve.Length} items...");

                TranslateBrowsePathsToNodeIdsRequest req = new TranslateBrowsePathsToNodeIdsRequest() {
                    BrowsePaths = nodesNeedingResolve.Select(n => new BrowsePath() {
                        StartingNode = n.StartingNode,
                        RelativePath = n.RelativePath
                    }).ToArray()
                };
                TranslateBrowsePathsToNodeIdsResponse resp = await connection.TranslateBrowsePathsToNodeIdsAsync(req);

                if (resp.Results == null || resp.Results.Length != nodesNeedingResolve.Length) {
                    LogWarn("Mismatch", "TranslateBrowsePathsToNodeIds failed");
                }
                else {
                    for (int i = 0; i < resp.Results.Length; ++i) {
                        BrowsePathResult? x = resp.Results[i];
                        if (x != null && StatusCode.IsGood(x.StatusCode) && x.Targets != null && x.Targets.Length > 0) {
                            BrowsePathTarget? target = x.Targets[0];
                            if (target != null && target.TargetId != null) {
                                NodeId id = target.TargetId.NodeId;
                                nodesNeedingResolve[i].Node = id;
                                PrintLine($"Resolved item '{nodesNeedingResolve[i].Name}' => {id}");
                            }
                            else {
                                PrintLine($"Could not resolve item '{nodesNeedingResolve[i].Name}': StatusCode: {x.StatusCode}");
                            }
                        }
                        else {
                            string statusCode = x == null ? "?" : x.StatusCode.ToString();
                            PrintLine($"Could not resolve item '{nodesNeedingResolve[i].Name}': StatusCode: {statusCode}");
                        }
                    }
                }
            }
            alarmConnectivity.ReturnToNormal();
            ReturnToNormal("OpenChannel", $"Connected to OPC UA server '{config.Address}'", connectionUp: true);
            return true;
        }
        catch (Exception exp) {
            Exception baseExp = exp.GetBaseException() ?? exp;
            lastConnectErrMsg = baseExp.Message;
            string msg = $"No connection to OPC UA server '{config.Address}': {baseExp.Message}";
            if (reportFailureImmediatelly || alarmConnectivity.OnWarning(msg)) {
                LogWarn("OpenChannel", msg, dataItem: null, connectionDown: true);
            }
            await CloseChannel();
            return false;
        }
    }

    private IUserIdentity GetIdentity() {
        if (config.Login.HasValue) {
            return new UserNameIdentity(config.Login.Value.UserName, config.Login.Value.Password);
        }
        else {
            return new AnonymousIdentity();
        }
    }

    private static IEnumerable<T> CleanNulls<T>(IEnumerable<T?>? items) where T : class {
        if (items == null) {
            yield break;
        }
        foreach (var item in items) {
            if (item != null) {
                yield return item;
            }
        }
    }

    private async Task CloseChannel() {
        ClientSessionChannel? connection = this.connection;
        if (connection == null) return;
        this.connection = null;
        try {
            await connection.CloseAsync();
        }
        catch (Exception) { }
    }

    public override async Task Shutdown() {
        try {
            discoveryManager?.Stop();
            await CloseChannel();
        }
        catch (Exception) { }
    }

    //private bool readExceptionWarning = false;
    //private bool writeExceptionWarning = false;

    private Timestamp lastReadDurationTimestamp = Timestamp.Now;

    public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

        bool connected = await TryConnect();
        if (!connected || connection == null) {

            if (!alarmConnectivity.IsActivated) {
                return items.Select(it => it.LastValue).ToArray();
            }
            else {
                return BadVTQsFromLastValue(items);
            }
        }

        var readHelper = new ReadManager<ReadValueId, Workstation.ServiceModel.Ua.DataValue>(items, request => {
            NodeId node = mapId2Info[request.ID].Node ?? NodeId.Null;
            return new ReadValueId { AttributeId = AttributeIds.Value, NodeId = node };
        });
        ReadValueId[] dataItemsToRead = readHelper.GetRefs();

        try {

            VTQ[] result;

            if (dataItemsToRead.Length > 0) {

                var readRequest = new Workstation.ServiceModel.Ua.ReadRequest {
                    MaxAge = maxAge.TotalMilliseconds,
                    NodesToRead = dataItemsToRead,
                    TimestampsToReturn = TimestampsToReturn.Source,
                };

                var now = Timestamp.Now;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                ReadResponse readResponse = await connection.ReadAsync(readRequest);

                sw.Stop();
                if (now >= lastReadDurationTimestamp + Duration.FromSeconds(1)) {
                    lastReadDurationTimestamp = now;
                    double duration = sw.Elapsed.TotalMilliseconds;
                    var vtq = VTQ.Make(duration, now, Quality.Good);
                    callback?.Notify_AdapterVarUpdate(AdapterVar.LastInnerReadDuration, vtq);
                }

                readHelper.SetAllResults(readResponse.Results, (vv, request) => MakeVTQ(vv, request.LastValue, request.ID));
                result = readHelper.values;
            }
            else {
                result = readHelper.values;
            }

            //if (readExceptionWarning) {
            //    readExceptionWarning = false;
            //    ReturnToNormal("UAReadExcept", "ReadDataItems successful again");
            //}

            return result;
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            //readExceptionWarning = true;
            //LogWarn("UAReadExcept", $"Read exception: {e.Message}", details: e.ToString());
            PrintErrorLine($"Read exception (closing connection, returning last values): {e.Message}");
            Task ignored = CloseChannel();
            return items.Select(it => it.LastValue).ToArray(); // Exception is most likely due to connection failure, just return last values
        }
    }

    private VTQ MakeVTQ(Workstation.ServiceModel.Ua.DataValue readRes, VTQ lastValue, string dataItemID) {
        var t = Timestamp.FromDateTime(readRes.SourceTimestamp);
        try {
            var q = MapQuality(readRes.StatusCode);
            var v = MakeDataValue(readRes.Variant, lastValue.V);
            return new VTQ(t, q, v);
        }
        catch (Exception exp) {
            string name = mapId2Info.ContainsKey(dataItemID) ? mapId2Info[dataItemID].Name : dataItemID;
            LogWarn("ReadConvertFailed", $"Converting read result of '{name}' failed.", dataItemID, exp.Message);
            return new VTQ(t, Quality.Bad, lastValue.V);
        }
    }

    private static DataValue MakeDataValue(Variant v, DataValue lastValue) {

        bool array = v.ArrayDimensions != null && v.ArrayDimensions.Length > 0;

        switch (v.Type) {
            case VariantType.Float:

                if (array)
                    return DataValue.FromFloatArray((float[]?)v);
                else
                    return DataValue.FromFloat((float)v);

            case VariantType.Double:

                if (array)
                    return DataValue.FromDoubleArray((double[]?)v);
                else
                    return DataValue.FromDouble((double)v);

            case VariantType.Boolean:

                if (array)
                    return DataValue.FromBoolArray((bool[]?)v);
                else
                    return DataValue.FromBool((bool)v);

            case VariantType.Int64:

                if (array)
                    return DataValue.FromLongArray((long[]?)v);
                else
                    return DataValue.FromLong((long)v);

            case VariantType.UInt64:

                if (array)
                    return DataValue.FromULongArray((ulong[]?)v);
                else
                    return DataValue.FromULong((ulong)v);

            case VariantType.Int32:

                if (array)
                    return DataValue.FromIntArray((int[]?)v);
                else
                    return DataValue.FromInt((int)v);

            case VariantType.UInt32:

                if (array)
                    return DataValue.FromUIntArray((uint[]?)v);
                else
                    return DataValue.FromUInt((uint)v);

            case VariantType.Int16:

                if (array)
                    return DataValue.FromShortArray((short[]?)v);
                else
                    return DataValue.FromShort((short)v);

            case VariantType.UInt16:

                if (array)
                    return DataValue.FromUShortArray((ushort[]?)v);
                else
                    return DataValue.FromUShort((ushort)v);

            case VariantType.SByte:

                if (array)
                    return DataValue.FromSByteArray((sbyte[]?)v);
                else
                    return DataValue.FromSByte((sbyte)v);

            case VariantType.Byte:

                if (array)
                    return DataValue.FromByteArray((byte[]?)v);
                else
                    return DataValue.FromByte((byte)v);

            case VariantType.String:

                if (array)
                    return DataValue.FromStringArray((string[]?)v);
                else
                    return DataValue.FromString((string?)v);

            case VariantType.Null: return lastValue;

            case VariantType.DateTime:
                if (array) {
                    DateTime[]? theArr = (DateTime[]?)v;
                    Timestamp[]? timestamps = theArr == null ? null : theArr.Select(Timestamp.FromDateTime).ToArray();
                    return DataValue.FromTimestampArray(timestamps);
                }
                else {
                    return DataValue.FromTimestamp(Timestamp.FromDateTime((DateTime)v));
                }

            case VariantType.Guid:
            case VariantType.ByteString:
            case VariantType.XmlElement:
            case VariantType.NodeId:
            case VariantType.ExpandedNodeId:
            case VariantType.StatusCode:
            case VariantType.QualifiedName:
            case VariantType.LocalizedText:
            case VariantType.ExtensionObject:
            case VariantType.DataValue:
            case VariantType.Variant:
            case VariantType.DiagnosticInfo:
            default:
                throw new Exception($"Type {v.Type} not implemented");
        }
    }

    private static Quality MapQuality(StatusCode sc) {
        if (StatusCode.IsGood(sc)) return Quality.Good;
        if (StatusCode.IsBad(sc)) return Quality.Bad;
        return Quality.Uncertain;
    }

    private Timestamp lastWriteDurationTimestamp = Timestamp.Now;

    public override async Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {

        int N = values.Count;

        bool connected = await TryConnect();
        if (!connected || connection == null) {
            var failed = new FailedDataItemWrite[N];
            for (int i = 0; i < N; ++i) {
                DataItemValue request = values[i];
                failed[i] = new FailedDataItemWrite(request.ID, "No connection to OPC UA server", noConnection: true);
            }
            return WriteDataItemsResult.Failure(failed);
        }

        List<FailedDataItemWrite>? listFailed = null;

        var dataItemsToWrite = new List<WriteValue>(N);
        var usedDataItemIDs = new List<string>(N);

        for (int i = 0; i < N; ++i) {
            DataItemValue request = values[i];
            string id = request.ID;
            if (mapId2Info.ContainsKey(id)) {
                ItemInfo info = mapId2Info[request.ID];
                NodeId di = info.Node ?? NodeId.Null;
                try {
                    //if (!di.IsWriteable) throw new Exception($"OPC item '{di.Name}' is not writeable");
                    dataItemsToWrite.Add(MakeWriteValue(di, request.Value.V, info.Type, info.Dimension));
                    usedDataItemIDs.Add(id);
                }
                catch (Exception exp) {
                    listFailed ??= [];
                    listFailed.Add(new FailedDataItemWrite(id, exp.Message));
                }
            }
            else {
                listFailed ??= [];
                listFailed.Add(new FailedDataItemWrite(id, $"No writeable data item with id '{id}' found."));
            }
        }

        try {

            if (dataItemsToWrite.Count > 0) {
                WriteRequest req = new() {
                    NodesToWrite = dataItemsToWrite.ToArray()
                };

                var now = Timestamp.Now;
                var sw = System.Diagnostics.Stopwatch.StartNew();

                WriteResponse resp = await connection.WriteAsync(req);

                sw.Stop();
                if (now >= lastWriteDurationTimestamp + Duration.FromSeconds(1)) {
                    lastWriteDurationTimestamp = now;
                    double duration = sw.Elapsed.TotalMilliseconds;
                    var vtq = VTQ.Make(duration, now, Quality.Good);
                    callback?.Notify_AdapterVarUpdate(AdapterVar.LastInnerWriteDuration, vtq);
                }

                if (resp.Results is not null && resp.Results.Length == dataItemsToWrite.Count) {
                    StatusCode[] results = resp.Results;
                    for (int i = 0; i < results.Length; ++i) {
                        StatusCode sc = results[i];
                        if (StatusCode.IsBad(sc)) {
                            listFailed ??= new List<FailedDataItemWrite>();
                            listFailed.Add(new FailedDataItemWrite(usedDataItemIDs[i], GetErrorDescriptionFromStatusCode(sc)));
                        }
                    }
                }

                //if (writeExceptionWarning) {
                //    writeExceptionWarning = false;
                //    ReturnToNormal("UAWriteExcept", "WriteDataItems successful again");
                //}
            }
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            //writeExceptionWarning = true;
            //LogWarn("UAWriteExcept", $"Write exception: {e.Message}", details: e.ToString());
            PrintErrorLine($"Write exception (closing connection): {e.Message}");
            _ = CloseChannel();
            foreach (string id in usedDataItemIDs) {
                listFailed ??= new List<FailedDataItemWrite>();
                listFailed.Add(new FailedDataItemWrite(id, e.Message, noConnection: true));
            }
        }

        if (listFailed == null)
            return WriteDataItemsResult.OK;
        else
            return WriteDataItemsResult.Failure(listFailed.ToArray());
    }

    private static readonly Dictionary<uint, string> statusCodeDictionary = new() {
        {0x804D0000, "BrowseDirectionInvalid"},
        {0x80600000, "BrowseNameInvalid"},
        {0x80480000, "ContentFilterInvalid"},
        {0x804A0000, "ContinuationPointInvalid"},
        {0x80380000, "DataEncodingInvalid"},
        {0x80390000, "DataEncodingUnsupported"},
        {0x80470000, "EventFilterInvalid"},
        {0x80450000, "FilterNotAllowed"},
        {0x80490000, "FilterOperandInvalid"},
        {0x80710000, "HistoryOperationInvalid"},
        {0x80720000, "HistoryOperationUnsupported"},
        {0x80360000, "IndexRangeInvalid"},
        {0x80370000, "IndexRangeNoData"},
        {0x80430000, "MonitoredItemFilterInvalid"},
        {0x80440000, "MonitoredItemFilterUnsupported"},
        {0x80420000, "MonitoredItemIdInvalid"},
        {0x80410000, "MonitoringModeInvalid"},
        {0x80310000, "NoCommunication"},
        {0x804B0000, "NoContinuationPoints"},
        {0x805F0000, "NodeClassInvalid"},
        {0x80330000, "NodeIdInvalid"},
        {0x80340000, "NodeIdUnknown"},
        {0x80690000, "NoDeleteRights"},
        {0x804E0000, "NodeNotInView"},
        {0x803E0000, "NotFound"},
        {0x80400000, "NotImplemented"},
        {0x803A0000, "NotReadable"},
        {0x803D0000, "NotSupported"},
        {0x803B0000, "NotWritable"},
        {0x803F0000, "ObjectDeleted"},
        {0x803C0000, "OutOfRange"},
        {0x804C0000, "ReferenceTypeIdInvalid"},
        {0x80640000, "SourceNodeIdInvalid"},
        {0x80460000, "StructureMissing"},
        {0x80650000, "TargetNodeIdInvalid"},
        {0x80630000, "TypeDefinitionInvalid"},
        {0x80740000, "TypeMismatch"},
        {0x80320000, "WaitingForInitialData"}
    };

    private static string GetErrorDescriptionFromStatusCode(StatusCode statusCode) {
        statusCodeDictionary.TryGetValue((uint)statusCode, out string? description);
        return description ?? statusCode.ToString();
    }

    private static WriteValue MakeWriteValue(NodeId item, DataValue value, DataType type, int dimension) {

        object? v = value.GetValue(type, dimension);
        var dv = new Workstation.ServiceModel.Ua.DataValue(v);

        return new WriteValue() {
            NodeId = item,
            AttributeId = AttributeIds.Value,
            Value = dv
        };
    }

    public override Task<string[]> BrowseAdapterAddress() {
        return Task.FromResult(new string[0]);
    }

    private string[]? cachedBrowseResult = null;
    private Timestamp? cacheTime = null;
    private const int CacheTimeMinutes = 30;

    public override async Task<string[]> BrowseDataItemAddress(string? idOrNull) {

        if (connection == null) {
            return new string[0];
        }

        if (cachedBrowseResult != null && cacheTime.HasValue && Timestamp.Now - cacheTime.Value < Duration.FromMinutes(CacheTimeMinutes)) {
            PrintLine("Returning cached browse result.");
            return cachedBrowseResult;
        }

        cachedBrowseResult = null;
        cacheTime = null;

        var result = new List<BrowseNode>();

        NodeId objectsID = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(browse_RootId), connection.NamespaceUris);
        var objects = new BrowseNode(objectsID, new QualifiedName(browse_RootName));

        //NodeId viewsID = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(ObjectIds.ViewsFolder), connection.NamespaceUris);
        //var views = new BrowseNode(viewsID, new QualifiedName("Views"));
        var set = new HashSet<BrowseNode>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await BrowseEntireTree(objects, result, set);
        sw.Stop();

        //await BrowseEntireTree(views, result);

        string[] ids = new string[result.Count];
        for (int i = 0; i < result.Count; ++i) {
            ids[i] = result[i].ToString();
        }

        if (ids.Length > 0) {
            Task _ = File.WriteAllLinesAsync("Browse_OPC_UA.txt", ids);
        }

        if (sw.Elapsed > TimeSpan.FromSeconds(5)) {
            PrintLine($"Caching browse result for {CacheTimeMinutes} minutes.");
            cachedBrowseResult = ids;
            cacheTime = Timestamp.Now;
        }

        return ids;
    }

    public override async Task<BrowseDataItemsResult> BrowseDataItems() {

        string clientCertificate = GetCertificate(certificateLocation);

        if (connection == null) {
            string endpoint = config.Address ?? "";
            string msg = $"No connection to OPC UA server '{endpoint}': " + lastConnectErrMsg;
            return new BrowseDataItemsResult(
                supportsBrowsing: true,
                browsingError: msg,
                items: [],
                clientCertificate: clientCertificate);
        }

        NodeId objectsID = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(browse_RootId), connection.NamespaceUris);
        BrowseNode objects = new BrowseNode(objectsID, new QualifiedName(browse_RootName));

        var result = new List<BrowseNode>();
        var set = new HashSet<BrowseNode>();

        await BrowseEntireTree(objects, result, set);

        var items = result.Select(MakeDataItemBrowseInfo).ToArray();
        return new BrowseDataItemsResult(
                supportsBrowsing: true,
                browsingError: "",
                items: items,
                clientCertificate: clientCertificate);
    }

    private static string GetCertificate(string certificateLocation) {
        if (Directory.Exists(certificateLocation)) {
            string[] files = Directory.GetFiles(certificateLocation, "*.crt");
            string? latestFile = null;
            DateTime latestTime = DateTime.MinValue;
            foreach (string file in files) {
                FileInfo fi = new FileInfo(file);
                DateTime t = fi.CreationTimeUtc;
                if (t > latestTime) {
                    latestTime = t;
                    latestFile = file;
                }
            }
            if (latestFile != null) {
                string path = Path.Combine(certificateLocation, latestFile);
                return File.ReadAllText(path, Encoding.UTF8);
            }
        }
        return "";
    }

    private static DataItemBrowseInfo MakeDataItemBrowseInfo(BrowseNode n) {

        var list = new List<BrowseNode>();
        BrowseNode.BuildPath(n, list, includeRoot: false);
        string[] path = list.Select(node => node.BrowseName.Name ?? node.BrowseName.ToString()).ToArray();

        return new DataItemBrowseInfo(n.ID.ToString(), path);
    }

    private bool IncludeNode(NodeId id, ReferenceDescription item) {

        if (id.NamespaceIndex == 0) return false;
        if (item.BrowseName == null) return false;
        if (item.BrowseName.Name == null) return false;
        if (excludeUnderscore && item.BrowseName.Name.StartsWith('_')) return false;

        return true;
    }

    private async Task BrowseEntireTree(BrowseNode parent, List<BrowseNode> result, HashSet<BrowseNode> set) {

        var children = await BrowseTree(parent.ID);
        if (children == null || connection == null) return;
        foreach (ReferenceDescription item in children) {
            if (item.NodeId == null) continue;
            NodeId id = ExpandedNodeId.ToNodeId(item.NodeId, connection.NamespaceUris);
            if (item.NodeClass == NodeClass.Object && IncludeNode(id, item)) {
                var nodeObject = new BrowseNode(id, item.BrowseName!, parent);
                await BrowseEntireTree(nodeObject, result, set);
            }
            else if (item.NodeClass == NodeClass.Variable && IncludeNode(id, item)) {
                var nodeVariable = new BrowseNode(id, item.BrowseName!, parent);
                if (!set.Contains(nodeVariable)) {
                    result.Add(nodeVariable);
                    set.Add(nodeVariable);
                }
            }
        }
    }

    internal async Task<IList<ReferenceDescription>> BrowseTree(NodeId tree) {

        if (connection == null) return new ReferenceDescription[0];

        var browseRequest = new BrowseRequest {
            NodesToBrowse = new[] {
                new BrowseDescription {
                    NodeId = tree,
                    ReferenceTypeId = NodeId.Parse(ReferenceTypeIds.HierarchicalReferences),
                    ResultMask = (uint)BrowseResultMask.TargetInfo,
                    NodeClassMask = (uint)NodeClass.Object | (int)NodeClass.Variable,
                    BrowseDirection = BrowseDirection.Forward,
                    IncludeSubtypes = true
                }
            },
            RequestedMaxReferencesPerNode = 1000
        };

        var rds = new List<ReferenceDescription>();
        BrowseResponse browseResponse = await connection.BrowseAsync(browseRequest);
        Workstation.ServiceModel.Ua.BrowseResult[] results = CleanNulls(browseResponse.Results).ToArray();
        rds.AddRange(results.SelectMany(result => CleanNulls(result.References)));

        var continuationPoints = results.Select(br => br.ContinuationPoint).Where(cp => cp != null).ToArray();

        while (continuationPoints.Length > 0 && connection != null) {

            var browseNextRequest = new BrowseNextRequest { ContinuationPoints = continuationPoints, ReleaseContinuationPoints = false };
            var browseNextResponse = await connection.BrowseNextAsync(browseNextRequest);
            Workstation.ServiceModel.Ua.BrowseResult[] nextResults = CleanNulls(browseNextResponse.Results).ToArray();
            rds.AddRange(nextResults.SelectMany(result => CleanNulls(result.References)));
            continuationPoints = nextResults.Select(br => br.ContinuationPoint).Where(cp => cp != null).ToArray();
        }

        return rds;
    }

    private void PrintLine(string msg) {
        logger.LogInformation(msg);
    }

    private void PrintErrorLine(string msg) {
        logger.LogError(msg);
    }

    private void LogWarn(string type, string msg, string? dataItem = null, string? details = null, bool connectionDown = false) {

        var ae = new AdapterAlarmOrEvent() {
            Time = Timestamp.Now,
            Severity = Severity.Warning,
            Connection = connectionDown,
            Type = type,
            Message = msg,
            Details = details ?? "",
            AffectedDataItems = string.IsNullOrEmpty(dataItem) ? [] : [dataItem]
        };

        callback?.Notify_AlarmOrEvent(ae);
    }

    private void ReturnToNormal(string type, string msg, bool connectionUp = false, params string[] affectedDataItems) {
        AdapterAlarmOrEvent _event = AdapterAlarmOrEvent.MakeReturnToNormal(type, msg, affectedDataItems);
        _event.Connection = connectionUp;
        callback?.Notify_AlarmOrEvent(_event);
    }
}

public class ItemInfo
{
    public string ID { get; }
    public string Name { get; }
    public DataType Type { get; }
    public int Dimension { get; }
    public NodeId? Node { get; set; }

    public RelativePath RelativePath { get; } = new RelativePath();
    public NodeId StartingNode { get; } = NodeId.Null;

    public const char Separator = '/';
    private const string Objects = "Objects/";
    private const string Views = "Views/";

    public ItemInfo(string id, string name, DataType type, int dimension, string address) {
        this.ID = id;
        this.Name = name;
        this.Type = type;
        this.Dimension = dimension;

        if (string.IsNullOrEmpty(address)) {
            Node = NodeId.Null;
        }
        else if (address.StartsWith(Objects) || address.StartsWith(Views)) {
            Node = null;
            if (address.StartsWith(Objects)) {
                StartingNode = NodeId.Parse(ObjectIds.ObjectsFolder);
                string path = address.Substring(Objects.Length);
                List<string> components = GetComponents(path);
                RelativePath = new RelativePath() {
                    Elements = components.Select(comp => new RelativePathElement() { TargetName = QualifiedName.Parse(comp) }).ToArray()
                };
            }
            else {
                StartingNode = NodeId.Parse(ObjectIds.ViewsFolder);
                string path = address.Substring(Views.Length);
                List<string> components = GetComponents(path);
                RelativePath = new RelativePath() {
                    Elements = components.Select(comp => new RelativePathElement() { TargetName = QualifiedName.Parse(comp) }).ToArray()
                };
            }
        }
        else {

            try {
                Node = NodeId.Parse(address);
            }
            catch (Exception) {
                throw new Exception($"DataItem {id}: address '{address}' is not a valid OPC UA node id");
            }
        }
    }

    private static List<string> GetComponents(string path) {
        string[] componentsSource = path.Split(Separator);
        var components = new List<string>();
        foreach (string component in componentsSource) {
            if (components.Count == 0 || HasNamespaceIdx(component))
                components.Add(component);
            else
                components[components.Count - 1] = components[components.Count - 1] + Separator + component;
        }
        return components;
    }

    private static bool HasNamespaceIdx(string s) {
        int idx = s.IndexOf(':');
        if (idx <= 0) return false;
        string ns = s.Substring(0, idx);
        return int.TryParse(ns, out _);
    }
}

sealed class BrowseNode(NodeId id, QualifiedName browseName, BrowseNode? parent = null) : IEquatable<BrowseNode>
{
    public NodeId ID { get; set; } = id;
    public QualifiedName BrowseName { get; set; } = browseName;
    public BrowseNode? Parent { get; set; } = parent;

    public override string ToString() {

        if (ID.IdType == IdType.String) {
            return ID.ToString();
        }

        var sb = new StringBuilder();
        PrintPath(this, sb);
        return sb.ToString();
    }

    private static void PrintPath(BrowseNode node, StringBuilder sb) {
        if (node.Parent == null) {
            sb.Append(node.BrowseName.Name);
        }
        else {
            PrintPath(node.Parent, sb);
            sb.Append(ItemInfo.Separator);
            sb.Append(node.BrowseName.ToString());
        }
    }

    public static void BuildPath(BrowseNode node, List<BrowseNode> sb, bool includeRoot) {
        if (node.Parent == null) {
            if (includeRoot) {
                sb.Add(node);
            }
        }
        else {
            BuildPath(node.Parent, sb, includeRoot);
            sb.Add(node);
        }
    }

    public override int GetHashCode() => ID.GetHashCode();

    public bool Equals([AllowNull] BrowseNode other) => other != null && ID == other.ID;

    public override bool Equals(object? obj) {
        return obj is BrowseNode bn && Equals(bn);
    }
}
