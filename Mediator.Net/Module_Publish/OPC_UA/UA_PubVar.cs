// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using OpcUaServerNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish.OPC_UA;

internal class UA_PubVar : BufferedVarPub {

    private readonly OpcUaConfig config;
    private readonly string host = "";
    private readonly ushort port = 4840;
    private readonly bool allowAnonym = true;
    private readonly OPC_UA_Server.LogLevel logLevel = OPC_UA_Server.LogLevel.Info;
    private readonly string loginUser = "";
    private readonly string loginPass = "";
    private readonly string certFile = "";

    public UA_PubVar(string dataFolder, OpcUaConfig config)
        : base(dataFolder, bufferIfOffline: false) {

        this.config = config;

        this.host = config.Host;
        this.port = config.Port;
        this.allowAnonym = config.AllowAnonym;
        this.loginUser = config.LoginUser;
        this.loginPass = config.LoginPass;
        this.certFile = config.ServerCertificateFile;

        this.logLevel = config.LogLevel.ToLowerInvariant() switch {
            "fatal"   => OPC_UA_Server.LogLevel.Fatal,
            "error"   => OPC_UA_Server.LogLevel.Error,
            "warn"    => OPC_UA_Server.LogLevel.Warning,
            "warning" => OPC_UA_Server.LogLevel.Warning,
            "info"    => OPC_UA_Server.LogLevel.Info,
            "information" => OPC_UA_Server.LogLevel.Info,
            "debug"   => OPC_UA_Server.LogLevel.Debug,
            "trace" => OPC_UA_Server.LogLevel.Trace,
            _ => OPC_UA_Server.LogLevel.Info,
        };

        var thread = new Thread(UA_Runner_Thread) {
            IsBackground = true
        };
        thread.Start();

        Start();
        _ = WriteReceiveRunner();
    }

    private async Task WriteReceiveRunner() {

        var list = new VariableValues();

        while (running) {

            while (valuesIncoming.TryDequeue(out UA_Var_Value? varVal)) {
                list.Add(VariableValue.Make(varVal.UaVar.VariableRef, varVal.Value));
            }

            Connection? con = fastConnection;
            if (list.Count > 0 && con != null) {
                try {
                    // Console.WriteLine($"UA_PubVar: Writing {list.Count} variables: {list[0]}");
                    await con.WriteVariablesIgnoreMissing(list);
                }
                catch (Exception exp) {
                    Console.Error.WriteLine($"UA_PubVar: Error writing variables: {exp.Message}");
                }
            }

            list.Clear();
            await Task.Delay(50);
        }
    }

    protected override string BuffDirName => "UA_Publish";
    internal override string PublisherID => config.ID;

    private bool configHasChanged = false;
    private readonly Dictionary<VariableRef, UA_Var> registeredVariables = [];

    private record VarInfoWithVTQ(VarInfo VarInfo, VTQ VTQ);

    public override async Task OnConfigChanged() {
        Console.WriteLine("Restaring OPC UA server because of changed configuration...");
        configHasChanged = true;
        VariableValues lastValues = lastSentValues.Select(kv => VariableValue.Make(kv.Key, kv.Value)).ToList();
        registeredVariables.Clear();
        lastSentValues.Clear();
        while (running && configHasChanged) {
            await Task.Delay(10);
        }
        await DoSend(lastValues, reportMissingVars: false);
    }

    protected override Task<bool> DoSend(VariableValues values) {
        return DoSend(values, reportMissingVars: true);
    }

    private Task<bool> DoSend(VariableValues values, bool reportMissingVars) {

        try {

            VariableValues changedValues = values
              .Where(v => !lastSentValues.ContainsKey(v.Variable) || lastSentValues[v.Variable] != v.Value)
              .ToList();

            var varInfosForRegister = new List<VarInfoWithVTQ>();

            foreach (VariableValue vv in changedValues) {

                VariableRef variable = vv.Variable;

                bool isRegistered = registeredVariables.TryGetValue(variable, out UA_Var? _);

                if (isRegistered) {
                    continue;
                }

                try {
                    VarInfo v = GetVariableInfoOrThrow(variable);
                    varInfosForRegister.Add(new VarInfoWithVTQ(v, vv.Value));
                }
                catch (Exception exp) {
                    if (reportMissingVars) { 
                        Console.Error.WriteLine($"Error getting info for variable '{vv.Variable}': {exp.Message}");
                    }
                    continue;
                }
            }

            if (varInfosForRegister.Count > 0) {
                RegisterVariables(varInfosForRegister);
            }

            foreach (VariableValue vv in changedValues) {
                VariableRef variable = vv.Variable;
                if (registeredVariables.TryGetValue(variable, out UA_Var? uaVar)) {
                    mapValuesOutgoing[uaVar.NodeId] = vv.Value;
                    lastSentValues[variable] = vv.Value;
                }
            }

            return Task.FromResult(true);
        }
        catch (Exception exp) {
            Console.Error.WriteLine($"UA_PubVar: Error setting value: {exp.Message}");
            //logger.Error(ex, "Creating channels failed: " + ex.Message);
            return Task.FromResult(false);
        }
    }

    private void RegisterVariables(List<VarInfoWithVTQ> varInfosForRegister) {

        foreach (VarInfoWithVTQ varInfoWithVTQ in varInfosForRegister) {
            VarInfo varInfo = varInfoWithVTQ.VarInfo;
            VTQ vtq = varInfoWithVTQ.VTQ;
            VariableRef variable = varInfo.VarRef;
            bool isValue = variable.Name == "Value";
            string nodeID;
            if (isValue)
                nodeID = $"ns=1;s={variable.Object.LocalObjectID}";
            else
                nodeID = $"ns=1;s={variable.Object.LocalObjectID}.{variable.Name}";

            bool writable = varInfo.Variable.Writable;

            List<UA_Folder> folders = [];

            if (varInfo.Object.Variables.Length > 1 || !isValue) {
                ObjectInfo obj = varInfo.Object;
                folders.Add(new UA_Folder($"ns=1;s={obj.ID.ToEncodedString()}", obj.Name));
            }

            foreach (MemInfo mem in varInfo.Parents) {
                ObjectInfo obj = mem.Obj;
                string member = mem.Member;
                ObjectMember? objMember = mem.ClassInfo.ObjectMember.FirstOrDefault(m => m.Name == member);
                bool isMultiMember = objMember != null && objMember.Dimension == Dimension.Array;
                bool addMemberFolder = mem.ClassInfo.ObjectMember.Count > 1 || isMultiMember;
                if (addMemberFolder) {
                    folders.Add(new UA_Folder($"ns=1;s={obj.ID.ToEncodedString()}__{member}", member));
                }
                folders.Add(new UA_Folder($"ns=1;s={obj.ID.ToEncodedString()}", obj.Name));
            }

            var uaVar = new UA_Var() {
                NodeId = nodeID,
                Name =  variable.Name == "Value" ? varInfo.Object.Name : $"{varInfo.Object.Name}.{variable.Name}",
                Writable = writable,
                Type = varInfo.Variable.Type,
                IsArray = varInfo.Variable.Dimension != 1,
                InitialValue = vtq,
                Unit = varInfo.Variable.Unit,
                VariableRef = variable,
                Folder = folders.ToArray(),
            };

            registeredVariables[variable] = uaVar;
            queueRegisterVariables.Enqueue(uaVar);
        }
    }

    private static UA_Quality MapQuality(Quality q) {
        return q switch {
            Quality.Bad => UA_Quality.Bad,
            Quality.Good => UA_Quality.Good,
            Quality.Uncertain => UA_Quality.Uncertain,
            _ => UA_Quality.Bad,
        };
    }

    private static Quality MapQuality(UA_Quality q) {
        return q switch {
            UA_Quality.Bad => Quality.Bad,
            UA_Quality.Good => Quality.Good,
            UA_Quality.Uncertain => Quality.Uncertain,
            _ => Quality.Bad,
        };
    }

    private record UA_Folder(string NodeId, string Name);

    private sealed class UA_Var {

        public string NodeId { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Writable { get; set; }
        public DataType Type { get; set; } = DataType.Float64;
        public bool IsArray { get; set; }
        public VTQ InitialValue { get; set; }
        public string Unit { get; set; } = "";

        public UA_Folder[] Folder { get; set; } = []; // first element is parent folder, last element is the root folder

        public VariableRef VariableRef { get; set; }

        public override string ToString() {
            return NodeId ?? "??";
        }
    }

    private record UA_Var_Value(UA_Var UaVar, VTQ Value);

    private readonly ConcurrentDictionary<string, VTQ> mapValuesOutgoing = new();
    private readonly ConcurrentQueue<UA_Var_Value> valuesIncoming = new();
    private readonly ConcurrentQueue<UA_Var> queueRegisterVariables = new();

    private void UA_Runner_Thread() {
        while (running) {
            configHasChanged = false;
            UA_Runner_Thread_Inner();
        }
    }

    private void UA_Runner_Thread_Inner() {

        List<UA_Var> uaVariables = [];
        OPC_UA_Server? uaServer = null;

        Dictionary<string, VTQ> mapLastValues = [];

        try {

            const string appName = "ifakFAST";

            Console.WriteLine($"Starting OPC UA server on port {port}...");
            
            // if host is empty string: listen externally (not just localhost)
            uaServer = OPC_UA_Server.Create(appName, host, port, logLevel, allowAnonym, loginUser, loginPass, certFile);
            uaServer.RunStartup();

            Console.WriteLine($"OPC UA server running on port {port}.");

            while (running && !configHasChanged) {

                while (queueRegisterVariables.TryDequeue(out UA_Var? uaVar)) {
                    if (AddVariable(uaServer, uaVar)) {
                        uaVariables.Add(uaVar);
                        mapValuesOutgoing[uaVar.NodeId] = uaVar.InitialValue;
                    }
                }

                foreach (var uaVar in uaVariables) {
                    string nodeId = uaVar.NodeId;
                    if (mapValuesOutgoing.TryRemove(nodeId, out VTQ vtq)) {
                        mapLastValues[nodeId] = vtq;
                        WriteVariable(uaServer, uaVar, vtq);
                    }
                }

                uaServer.RunStep();

                foreach (var uaVar in uaVariables) {
                    if (uaVar.Writable) {
                        string nodeId = uaVar.NodeId;
                        VTQ? vtqOpt = ReadVariable(uaServer, uaVar);
                        if (vtqOpt.HasValue) {
                            VTQ vtq = vtqOpt.Value;
                            VTQ lastVQ = mapLastValues[nodeId];
                            if (vtq.T > lastVQ.T) {
                                //Console.WriteLine($"Incoming value: {value} {quality}");
                                valuesIncoming.Enqueue(new UA_Var_Value(uaVar, vtq));
                                mapLastValues[nodeId] = vtq;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception exp) {
            Console.Error.WriteLine("OPC UA server error: " + exp.Message);
            Thread.Sleep(5000);
        }
        finally {
            uaVariables.Clear();
            mapValuesOutgoing.Clear();
            valuesIncoming.Clear();
            queueRegisterVariables.Clear();
            uaServer?.Close();
            Console.WriteLine($"OPC UA server stopped.");
        }
    }

    private static bool AddVariable(OPC_UA_Server uaServer, UA_Var uaVar) {

        if (uaVar.Folder.Length > 0) {
            UA_Folder parentFolder = uaVar.Folder.First();
            if (!uaServer.NodeExists(parentFolder.NodeId)) {
                string? parentNodeID = null;
                foreach (var folder in uaVar.Folder.Reverse()) {
                    if (!uaServer.NodeExists(folder.NodeId)) {
                        try {
                            uaServer.AddFolderNode(folder.NodeId, folder.Name, parentNodeID);
                        }
                        catch (Exception exp) {
                            Console.Error.WriteLine(exp.Message);
                            return false;
                        }
                    }
                    parentNodeID = folder.NodeId;
                }
            }
        }

        string? parentNodeId = uaVar.Folder.Length > 0 ? uaVar.Folder.First().NodeId : null;
        string nodeId = uaVar.NodeId;
        string name = uaVar.Name;
        try {
            switch (uaVar.Type) {

                case DataType.Bool:
                    uaServer.AddVariableNode_Boolean(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetBool(), parentNodeId);
                    break;

                case DataType.SByte:
                    uaServer.AddVariableNode_SByte(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetSByte(), parentNodeId);
                    break;

                case DataType.Byte:
                    if (!uaVar.IsArray)
                        uaServer.AddVariableNode_Byte(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetByte(), parentNodeId);
                    else
                        uaServer.AddVariableNode_ByteString(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetByteArray() ?? [], parentNodeId);
                    break;

                case DataType.Int16:
                    uaServer.AddVariableNode_Int16(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetShort(), parentNodeId);
                    break;

                case DataType.UInt16:
                    uaServer.AddVariableNode_UInt16(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetUShort(), parentNodeId);
                    break;

                case DataType.Int32:
                    uaServer.AddVariableNode_Int32(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetInt(), parentNodeId);
                    break;

                case DataType.UInt32:
                    uaServer.AddVariableNode_UInt32(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetUInt(), parentNodeId);
                    break;

                case DataType.Int64:
                    uaServer.AddVariableNode_Int64(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetLong(), parentNodeId);
                    break;

                case DataType.UInt64:
                    uaServer.AddVariableNode_UInt64(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetULong(), parentNodeId);
                    break;

                case DataType.Float32:
                    uaServer.AddVariableNode_Float(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.AsFloat() ?? 0.0f, parentNodeId);
                    break;

                case DataType.Float64:
                    uaServer.AddVariableNode_Double(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.AsDouble() ?? 0.0, parentNodeId);
                    break;

                case DataType.String:
                case DataType.JSON:
                    uaServer.AddVariableNode_String(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetString() ?? "", parentNodeId);
                    break;

                case DataType.Timestamp:
                    uaServer.AddVariableNode_DateTime(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetTimestamp().ToDateTime(), parentNodeId);
                    break;

                case DataType.Guid:
                    uaServer.AddVariableNode_Guid(nodeId, name, uaVar.Writable, uaVar.InitialValue.V.GetGuid(), parentNodeId);
                    break;
            }
            return true;
        }
        catch (Exception exp) {
            Console.Error.WriteLine($"Error adding variable {uaVar.Name}: {exp.Message}");
            return false;
        }
    }

    private static void WriteVariable(OPC_UA_Server uaServer, UA_Var uaVar, VTQ vq) {

        string nodeId = uaVar.NodeId;

        try {

            DateTime t = vq.T.ToDateTime();
            UA_Quality q = MapQuality(vq.Q);

            switch (uaVar.Type) {

                case DataType.Bool:
                    uaServer.WriteVariableValue_Boolean(nodeId, vq.V.GetBool(), t, q);
                    break;

                case DataType.SByte:
                    uaServer.WriteVariableValue_SByte(nodeId, vq.V.GetSByte(), t, q);
                    break;

                case DataType.Byte:
                    if (!uaVar.IsArray)
                        uaServer.WriteVariableValue_Byte(nodeId, vq.V.GetByte(), t, q);
                    else
                        uaServer.WriteVariableValue_ByteString(nodeId, vq.V.GetByteArray() ?? [], t, q);
                    break;

                case DataType.Int16:
                    uaServer.WriteVariableValue_Int16(nodeId, vq.V.GetShort(), t, q);
                    break;

                case DataType.UInt16:
                    uaServer.WriteVariableValue_UInt16(nodeId, vq.V.GetUShort(), t, q);
                    break;

                case DataType.Int32:
                    uaServer.WriteVariableValue_Int32(nodeId, vq.V.GetInt(), t, q);
                    break;

                case DataType.UInt32:
                    uaServer.WriteVariableValue_UInt32(nodeId, vq.V.GetUInt(), t, q);
                    break;

                case DataType.Int64:
                    uaServer.WriteVariableValue_Int64(nodeId, vq.V.GetLong(), t, q);
                    break;

                case DataType.UInt64:
                    uaServer.WriteVariableValue_UInt64(nodeId, vq.V.GetULong(), t, q);
                    break;

                case DataType.Float32:
                    uaServer.WriteVariableValue_Float(nodeId, vq.V.AsFloat() ?? throw new Exception("Valueis not a float"), t, q);
                    break;

                case DataType.Float64:
                    uaServer.WriteVariableValue_Double(nodeId, vq.V.AsDouble() ?? throw new Exception("Valueis not a double"), t, q);
                    break;

                case DataType.String:
                case DataType.JSON:
                    uaServer.WriteVariableValue_String(nodeId, vq.V.GetString() ?? "", t, q);
                    break;

                case DataType.Timestamp:
                    uaServer.WriteVariableValue_DateTime(nodeId, vq.V.GetTimestamp().ToDateTime(), t, q);
                    break;

                case DataType.Guid:
                    uaServer.WriteVariableValue_Guid(nodeId, vq.V.GetGuid(), t, q);
                    break;
            }
        }
        catch (Exception ex) {
            Console.Error.WriteLine("Error updating OPC UA variable " + nodeId + ": " + ex.Message);
        }
    }

    private static VTQ? ReadVariable(OPC_UA_Server uaServer, UA_Var uaVar) {

        string nodeId = uaVar.NodeId;
        DataValue dv = DataValue.Empty;
        UA_Quality quality = UA_Quality.Bad;
        DateTime timestamp = DateTime.MinValue;

        try {
            switch (uaVar.Type) {

                case DataType.Bool:
                    dv = DataValue.FromBool(uaServer.ReadVariableValue_Boolean(nodeId, out quality, out timestamp));
                    break;

                case DataType.SByte:
                    dv = DataValue.FromSByte(uaServer.ReadVariableValue_SByte(nodeId, out quality, out timestamp));
                    break;

                case DataType.Byte:
                    if (!uaVar.IsArray)
                        dv = DataValue.FromByte(uaServer.ReadVariableValue_Byte(nodeId, out quality, out timestamp));
                    else
                        dv = DataValue.FromByteArray(uaServer.ReadVariableValue_ByteString(nodeId, out quality, out timestamp));
                    break;

                case DataType.Int16:
                    dv = DataValue.FromShort(uaServer.ReadVariableValue_Int16(nodeId, out quality, out timestamp));
                    break;

                case DataType.UInt16:
                    dv = DataValue.FromUShort(uaServer.ReadVariableValue_UInt16(nodeId, out quality, out timestamp));
                    break;

                case DataType.Int32:
                    dv = DataValue.FromInt(uaServer.ReadVariableValue_Int32(nodeId, out quality, out timestamp));
                    break;

                case DataType.UInt32:
                    dv = DataValue.FromUInt(uaServer.ReadVariableValue_UInt32(nodeId, out quality, out timestamp));
                    break;

                case DataType.Int64:
                    dv = DataValue.FromLong(uaServer.ReadVariableValue_Int64(nodeId, out quality, out timestamp));
                    break;

                case DataType.UInt64:
                    dv = DataValue.FromULong(uaServer.ReadVariableValue_UInt64(nodeId, out quality, out timestamp));
                    break;

                case DataType.Float32:
                    dv = DataValue.FromFloat(uaServer.ReadVariableValue_Float(nodeId, out quality, out timestamp));
                    break;

                case DataType.Float64:
                    dv = DataValue.FromDouble(uaServer.ReadVariableValue_Double(nodeId, out quality, out timestamp));
                    break;

                case DataType.String:
                case DataType.JSON:
                    dv = DataValue.FromString(uaServer.ReadVariableValue_String(nodeId, out quality, out timestamp));
                    break;

                case DataType.Timestamp:
                    dv = DataValue.FromTimestamp(Timestamp.FromDateTime(uaServer.ReadVariableValue_DateTime(nodeId, out quality, out timestamp)));
                    break;

                case DataType.Guid:
                    dv = DataValue.FromGuid(uaServer.ReadVariableValue_Guid(nodeId, out quality, out timestamp));
                    break;
            }

            return VTQ.Make(dv, Timestamp.FromDateTime(timestamp), MapQuality(quality));
        }
        catch (Exception ex) {
            Console.Error.WriteLine("Error reading OPC UA variable " + nodeId + ": " + ex.Message);
            return null;
        }
    }
}
