// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Workstation.ServiceModel.Ua;

namespace Ifak.Fast.Mediator.IO.Adapter_OPC_UA;

// Auto-discovery manager for OPC UA variables
internal sealed class AutoDiscoveryManager( OPC_UA adapter,
                                            string rootNodeAddress,
                                            int maxDepth,
                                            TimeSpan browseInterval,
                                            HashSet<int> excludedNamespaces,
                                            AdapterCallback callback,
                                            Logger logger)
{
    private readonly OPC_UA adapter = adapter;
    private readonly string rootNodeAddress = rootNodeAddress;
    private readonly int maxDepth = maxDepth;
    private readonly HashSet<int> excludedNamespaces = excludedNamespaces;
    private readonly AdapterCallback callback = callback;
    private readonly Logger logger = logger;
    private bool isStopped = false;

    public Task StartAsync() {
        // logger.LogInformation("StartAsync TID: {}", Environment.CurrentManagedThreadId);
        return DicoveryLoop();
    }

    private async Task DicoveryLoop() {

        await Task.Delay(TimeSpan.FromSeconds(5)); // Initial delay

        while (!isStopped) {
            try {
                await PeriodicDiscovery();
            }
            catch (Exception ex) {
                logger.LogError("Discovery loop error: {}", ex.Message);
            }
            // Wait for the next cycle
            await Task.Delay(browseInterval);
        }
    }

    public void Stop() {
        isStopped = true;
    }

    private async Task PeriodicDiscovery() {

        try {
            // logger.LogInformation("DiscoverNewNodes TID: {}", Environment.CurrentManagedThreadId);
            DataItemUpsert[] newItems = await DiscoverNewNodes();
            // logger.LogInformation("ProcessBufferedItems TID: {}", Environment.CurrentManagedThreadId);

            if (newItems.Length > 0) {
                var configUpdate = new ConfigUpdate {
                    DataItemUpserts = newItems
                };
                callback.UpdateConfig(configUpdate);
            }
        }
        catch (Exception ex) {
            logger.LogWarning("Auto-discovery failed: {}", ex.Message);
        }
    }

    public async Task<DataItemUpsert[]> DiscoverNewNodes() {

        bool connected = await adapter.TryConnect();

        if (!connected || adapter.connection == null) {
            logger.LogInformation("No connection available for auto-discovery");
            return [];
        }

        try {
            
            NodeId startingNodeId = GetStartingNodeId();
            if (startingNodeId == NodeId.Null) {
                logger.LogWarning("Could not resolve starting node: {}", rootNodeAddress);
                return [];
            }

            logger.LogDebug("Starting auto-discovery from node: {}", startingNodeId);

            var discoveredNodes = new List<DiscoveredNode>();
            var visitedNodes = new HashSet<NodeId>();
            var configuredVariables = adapter.mapId2Info.Values
                .Select(it => it.Node)
                .Where(it => it != null)
                .Cast<NodeId>()
                .ToHashSet();

            await DiscoverVariablesRecursive(startingNodeId, discoveredNodes, visitedNodes, configuredVariables, 0);

            if (discoveredNodes.Count > 0) {
                logger.LogInformation("Auto-discovery found {} new DataItems", discoveredNodes.Count);
            }

            List<DataItemUpsert> items = [];

            // Convert discovered nodes to data items
            foreach (var node in discoveredNodes) {

                string nodeId = node.NodeId.ToString();
                string id = nodeId;
                string name = node.DisplayName ?? id;
                string address = nodeId;

                logger.LogInformation("Found Var ID: {} Name: {} Type: {} Dimension: {}", id, name, node.DataType, node.Dimension);

                items.Add(new DataItemUpsert {
                    ID = id,
                    Name = name,
                    Address = address,
                    Type = node.DataType,
                    Dimension = node.Dimension,
                    Read = true,
                    Write = false
                });
            }

            return items.ToArray();
        }
        catch (Exception ex) {
            logger.LogError("Error during node discovery: {}", ex.Message);
            return [];
        }
    }

    private NodeId GetStartingNodeId() {
        if (rootNodeAddress.Equals("Objects", StringComparison.OrdinalIgnoreCase)) {
            return NodeId.Parse(ObjectIds.ObjectsFolder);
        }
        else if (rootNodeAddress.Equals("Views", StringComparison.OrdinalIgnoreCase)) {
            return NodeId.Parse(ObjectIds.ViewsFolder);
        }
        else {
            try {
                return NodeId.Parse(rootNodeAddress);
            }
            catch {
                return NodeId.Null;
            }
        }
    }

    private async Task DiscoverVariablesRecursive(NodeId parentNodeId,
                                                  List<DiscoveredNode> discoveredNodes,
                                                  HashSet<NodeId> visitedNodes,
                                                  HashSet<NodeId> configuredVariables,
                                                  int currentDepth) {

        if (currentDepth >= maxDepth || visitedNodes.Contains(parentNodeId) || adapter.connection == null) {
            return;
        }

        visitedNodes.Add(parentNodeId);

        try {

            IList<ReferenceDescription> children = await adapter.BrowseTree(parentNodeId);

            foreach (ReferenceDescription child in children) {

                if (child.NodeId == null) continue;

                NodeId childNodeId = ExpandedNodeId.ToNodeId(child.NodeId, adapter.connection.NamespaceUris);

                // Skip excluded namespaces
                if (excludedNamespaces.Contains(childNodeId.NamespaceIndex))
                    continue;

                // Skip nodes starting with underscore if configured
                if (adapter.excludeUnderscore && child.BrowseName?.Name?.StartsWith('_') == true)
                    continue;

                if (child.NodeClass == NodeClass.Variable) {
                    // Check if this variable is already configured
                    if (!configuredVariables.Contains(childNodeId)) {
                        DiscoveredNode? discoveredNode = await CreateDiscoveredNode(childNodeId, child);
                        if (discoveredNode != null) {
                            discoveredNodes.Add(discoveredNode);
                        }
                    }
                }
                else if (child.NodeClass == NodeClass.Object) {
                    // Recursively browse objects
                    await DiscoverVariablesRecursive(childNodeId, discoveredNodes, visitedNodes, configuredVariables, currentDepth + 1);
                }
            }
        }
        catch (Exception ex) {
            logger.LogWarning("Failed to browse node {}: {}", parentNodeId, ex.Message);
        }
    }

    private async Task<DiscoveredNode?> CreateDiscoveredNode(NodeId nodeId, ReferenceDescription reference) {

        if (adapter.connection == null) return null;

        try {
            // Read the variable's data type and value rank
            var readRequest = new Workstation.ServiceModel.Ua.ReadRequest {
                NodesToRead = [
                    new ReadValueId { NodeId = nodeId, AttributeId = AttributeIds.DataType },
                    new ReadValueId { NodeId = nodeId, AttributeId = AttributeIds.ValueRank },
                    new ReadValueId { NodeId = nodeId, AttributeId = AttributeIds.DisplayName }
                ]
            };

            var response = await adapter.connection.ReadAsync(readRequest);
            if (response.Results == null || 
                response.Results.Length < 3 || 
                response.Results.Where(x => x != null).Count() < 3) return null;

            DataType dataType = MapOpcUaDataTypeToMediatorType(response.Results[0]!.Variant);
            int valueRank = response.Results[1]!.Variant.Type == VariantType.Int32 ? (int)response.Results[1]!.Variant : -1;
            string? displayName = response.Results[2]!.Variant.Type == VariantType.LocalizedText
                ? ((LocalizedText)response.Results[2]!.Variant)!.Text
                : reference.BrowseName?.Name;

            // UA_VALUERANK_SCALAR_OR_ONE_DIMENSION  -3
            // UA_VALUERANK_ANY                      -2
            // UA_VALUERANK_SCALAR                   -1
            // UA_VALUERANK_ONE_OR_MORE_DIMENSIONS    0
            // UA_VALUERANK_ONE_DIMENSION             1
            // UA_VALUERANK_TWO_DIMENSIONS            2
            // UA_VALUERANK_THREE_DIMENSIONS          3

            // ifakFAST Dimension:
            // 0 := var array;
            // 1 := scalar;
            // N := array with exactly N entries

            int dimension = valueRank >= 0 ? 0 : 1;

            return new DiscoveredNode {
                NodeId = nodeId,
                DisplayName = displayName ?? reference.BrowseName?.Name,
                DataType = dataType,
                Dimension = dimension
            };
        }
        catch (Exception ex) {
            logger.LogWarning("Failed to create discovered node for {}: {}", nodeId, ex.Message);
            return null;
        }
    }

    private static DataType MapOpcUaDataTypeToMediatorType(Variant dataTypeVariant) {

        if (dataTypeVariant.Type != VariantType.NodeId) return DataType.Float64;

        NodeId dataTypeNodeId = ((NodeId?)dataTypeVariant.Value)!;

        uint id = (uint)dataTypeNodeId.Identifier;
        VariantType vt = (VariantType)id;

        return vt switch {
            VariantType.Boolean => DataType.Bool,
            VariantType.SByte =>  DataType.SByte,
            VariantType.Byte => DataType.Byte,
            VariantType.Int16 =>  DataType.Int16,
            VariantType.UInt16 =>  DataType.UInt16,
            VariantType.Int32 => DataType.Int32,
            VariantType.UInt32 =>  DataType.UInt32,
            VariantType.Int64 =>  DataType.Int64,
            VariantType.UInt64 =>  DataType.UInt64,
            VariantType.Float =>  DataType.Float32,
            VariantType.Double =>  DataType.Float64,
            VariantType.String =>  DataType.String,
            VariantType.DateTime =>  DataType.Timestamp,
            _ =>  DataType.Float64 // Default fallback
        };
    }
}

// Helper class for discovered nodes
internal sealed class DiscoveredNode
{
    public NodeId NodeId { get; set; } = NodeId.Null;
    public string? DisplayName { get; set; }
    public DataType DataType { get; set; } = DataType.Float64;
    public int Dimension { get; set; } = 1;
}
