﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Workstation.ServiceModel.Ua;
using Workstation.ServiceModel.Ua.Channels;

namespace Ifak.Fast.Mediator.IO.Adapter_OPC_UA
{
    [Identify("OPC UA")]
    public class OPC_UA : AdapterBase
    {
        private ApplicationDescription appDescription = new ApplicationDescription();
        //private ICertificateStore certificateStore;
        private UaTcpSessionChannel? connection;

        private readonly ILoggerFactory loggerFactory = new LoggerFactory();

        private Adapter? config;
        private AdapterCallback? callback;
        private Dictionary<string, ItemInfo> mapId2Info = new Dictionary<string, ItemInfo>();

        public override bool SupportsScheduledReading => true;

        public override async Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

            this.config = config;
            this.callback = callback;

            string appName = "Mediator.IO.OPC_UA";

            appDescription = new ApplicationDescription {
                ApplicationName = appName,
                ApplicationUri = $"urn:{Dns.GetHostName()}:{appName}",
                ApplicationType = ApplicationType.Client
            };

            //string pkiPath = Path.Combine(
            //    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            //    "Mediator.IO.OPC_UA",
            //    "pki");

            //certificateStore = new DirectoryStore(pkiPath, acceptAllRemoteCertificates: true, createLocalCertificateIfNotExist: true);

            this.mapId2Info = config.GetAllDataItems().Where(di => !string.IsNullOrEmpty(di.Address)).ToDictionary(
               item => /* key */ item.ID,
               item => /* val */ new ItemInfo(item.ID, item.Name, item.Type, item.Dimension, item.Address));

            PrintLine(config.Address);

            await TryConnect();

            return new Group[0];
        }

        private async Task<bool> TryConnect() {

            if (connection != null) {
                return true;
            }

            if (config == null || string.IsNullOrEmpty(config.Address)) return false;

            try {

                var getEndpointsRequest = new GetEndpointsRequest {
                    EndpointUrl = config.Address,
                    ProfileUris = new[] { TransportProfileUris.UaTcpTransport }
                };

                GetEndpointsResponse endpointsResponse = await UaTcpDiscoveryService.GetEndpointsAsync(getEndpointsRequest);
                EndpointDescription?[] endpoints = endpointsResponse.Endpoints ?? new EndpointDescription[0];
                EndpointDescription[] noSecurityEndpoints = endpoints
                    .Where(e => e != null && e.SecurityPolicyUri == SecurityPolicyUris.None)
                    .Cast<EndpointDescription>()
                    .ToArray();

                var (endpoint, userIdentity) = FirstEndpointWithLogin(noSecurityEndpoints);

                if (endpoint == null || userIdentity == null) {
                    throw new Exception("No matching endpoint");
                }

                var channel = new UaTcpSessionChannel(
                            this.appDescription,
                            null,
                            userIdentity,
                            endpoint,
                            loggerFactory);

                await channel.OpenAsync();

                this.connection = channel;

                PrintLine($"Opened session with endpoint '{channel.RemoteEndpoint.EndpointUrl}'.");
                PrintLine($"SecurityPolicy: '{channel.RemoteEndpoint.SecurityPolicyUri}'.");
                PrintLine($"SecurityMode: '{channel.RemoteEndpoint.SecurityMode}'.");
                PrintLine($"UserIdentityToken: '{channel.UserIdentity}'.");

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
                return true;
            }
            catch (Exception exp) {
                Exception baseExp = exp.GetBaseException() ?? exp;
                LogWarn("OpenChannel", "Open channel error: " + baseExp.Message, dataItem: null, details: baseExp.StackTrace);
                await CloseChannel();
                return false;
            }
        }

        private (EndpointDescription?, IUserIdentity?) FirstEndpointWithLogin(EndpointDescription[] endpoints) {

            if (config == null) return (null, null);

            foreach (var endpoint in endpoints) {

                foreach (var policy in CleanNulls(endpoint.UserIdentityTokens)) {

                    if (policy.TokenType == UserTokenType.Anonymous && !config.Login.HasValue) {
                        return (endpoint, new AnonymousIdentity());
                    }

                    if (policy.TokenType == UserTokenType.UserName && config.Login.HasValue) {
                        return (endpoint, new UserNameIdentity(config.Login.Value.UserName, config.Login.Value.Password));
                    }
                }
            }
            return (null, null);
        }

        private static IEnumerable<T> CleanNulls<T>(IEnumerable<T?>? items) where T: class {
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
            UaTcpSessionChannel? connection = this.connection;
            if (connection == null) return;
            this.connection = null;
            try {
                await connection.CloseAsync();
            }
            catch (Exception) { }
        }

        public override async Task Shutdown() {
            try {
                await CloseChannel();
            }
            catch (Exception) { }
        }

        public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

            bool connected = await TryConnect();
            if (!connected || connection == null) {
                return GetBadVTQs(items);
            }

            var readHelper = new ReadManager<ReadValueId, Workstation.ServiceModel.Ua.DataValue>(items, request => {
                NodeId node = mapId2Info[request.ID].Node ?? NodeId.Null;
                return new ReadValueId { AttributeId = AttributeIds.Value, NodeId = node };
            });
            ReadValueId[] dataItemsToRead = readHelper.GetRefs();

            try {

                if (dataItemsToRead.Length > 0) {

                    var readRequest = new Workstation.ServiceModel.Ua.ReadRequest {
                        NodesToRead = dataItemsToRead,
                        TimestampsToReturn = TimestampsToReturn.Source,
                    };

                    ReadResponse readResponse = await connection.ReadAsync(readRequest);
                    readHelper.SetAllResults(readResponse.Results, (vv, request) => MakeVTQ(vv, request.LastValue, request.ID));
                    return readHelper.values;
                }
                else {
                    return readHelper.values;
                }
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                LogWarn("UAReadExcept", $"Read exception: {e.Message}", details: e.ToString());
                Task ignored = CloseChannel();
                return GetBadVTQs(items);
            }
        }

        private static VTQ[] GetBadVTQs(IList<ReadRequest> items) {
            int N = items.Count;
            var t = Timestamp.Now;
            VTQ[] res = new VTQ[N];
            for (int i = 0; i < N; ++i) {
                VTQ vtq = items[i].LastValue;
                vtq.Q = Quality.Bad;
                vtq.T = t;
                res[i] = vtq;
            }
            return res;
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

        public override async Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {

            int N = values.Count;

            bool connected = await TryConnect();
            if (!connected || connection == null) {
                var failed = new FailedDataItemWrite[N];
                for (int i = 0; i < N; ++i) {
                    DataItemValue request = values[i];
                    failed[i] = new FailedDataItemWrite(request.ID, "No connection to OPC UA server");
                }
                return WriteDataItemsResult.Failure(failed);
            }

            List<FailedDataItemWrite>? listFailed = null;

            var dataItemsToWrite = new List<WriteValue>(N);
            for (int i = 0; i < N; ++i) {
                DataItemValue request = values[i];
                string id = request.ID;
                if (mapId2Info.ContainsKey(id)) {
                    ItemInfo info = mapId2Info[request.ID];
                    NodeId di = info.Node ?? NodeId.Null;
                    try {
                        //if (!di.IsWriteable) throw new Exception($"OPC item '{di.Name}' is not writeable");
                        dataItemsToWrite.Add(MakeWriteValue(di, request.Value.V, info.Type, info.Dimension));
                    }
                    catch (Exception exp) {
                        if (listFailed == null) {
                            listFailed = new List<FailedDataItemWrite>();
                        }
                        listFailed.Add(new FailedDataItemWrite(id, exp.Message));
                    }
                }
                else {
                    if (listFailed == null) {
                        listFailed = new List<FailedDataItemWrite>();
                    }
                    listFailed.Add(new FailedDataItemWrite(id, $"No writeable data item with id '{id}' found."));
                }
            }

            if (dataItemsToWrite.Count > 0) {
                WriteRequest req = new WriteRequest() {
                    NodesToWrite = dataItemsToWrite.ToArray()
                };
                WriteResponse resp = await connection.WriteAsync(req);
                // TODO: Check result!
            }

            if (listFailed == null)
                return WriteDataItemsResult.OK;
            else
                return WriteDataItemsResult.Failure(listFailed.ToArray());
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

        public override async Task<string[]> BrowseDataItemAddress(string? idOrNull) {

            if (connection == null) {
                return new string[0];
            }

            var result = new List<BrowseNode>();

            NodeId objectsID = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(ObjectIds.ObjectsFolder), connection.NamespaceUris);
            var objects = new BrowseNode(objectsID, new QualifiedName("Objects"));

            NodeId viewsID = ExpandedNodeId.ToNodeId(ExpandedNodeId.Parse(ObjectIds.ViewsFolder), connection.NamespaceUris);
            var views = new BrowseNode(viewsID, new QualifiedName("Views"));

            await BrowseEntireTree(objects, result);
            await BrowseEntireTree(views, result);

            return result.Select(n => n.ToString()).ToArray();
        }

        private async Task BrowseEntireTree(BrowseNode parent, List<BrowseNode> result) {

            var children = await BrowseTree(parent.ID);
            if (children == null || connection == null) return;
            foreach (ReferenceDescription item in children) {
                if (item.NodeId == null) continue;
                NodeId id = ExpandedNodeId.ToNodeId(item.NodeId, connection.NamespaceUris);
                if (item.NodeClass == NodeClass.Object && id.NamespaceIndex != 0 && item.BrowseName != null) {
                    var nodeObject = new BrowseNode(id, item.BrowseName, parent);
                    await BrowseEntireTree(nodeObject, result);
                }
                else if (item.NodeClass == NodeClass.Variable && id.NamespaceIndex != 0 && item.BrowseName != null) {
                    var nodeVariable = new BrowseNode(id, item.BrowseName, parent);
                    if (result.All(n => n.ID != id)) {
                        result.Add(nodeVariable);
                    }
                }
            }
        }

        private async Task<IList<ReferenceDescription>> BrowseTree(NodeId tree) {

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
            string name = config?.Name ?? "";
            Console.WriteLine(name + ": " + msg);
        }

        private void LogWarn(string type, string msg, string? dataItem = null, string? details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Warning,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = string.IsNullOrEmpty(dataItem) ? new string[0] : new string[] { dataItem }
            };

            callback?.Notify_AlarmOrEvent(ae);
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
                Node = NodeId.Parse(address);
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
            int x;
            return int.TryParse(ns, out x);
        }
    }

    class BrowseNode
    {
        public NodeId ID { get; set; }
        public QualifiedName BrowseName { get; set; }
        public BrowseNode? Parent { get; set; }

        public BrowseNode(NodeId id, QualifiedName browseName, BrowseNode? parent = null) {
            ID = id;
            BrowseName = browseName;
            Parent = parent;
        }

        public override string ToString() {
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
    }
}
