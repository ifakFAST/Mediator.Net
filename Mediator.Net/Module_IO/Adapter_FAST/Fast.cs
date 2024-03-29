﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_FAST
{
    [Identify("Fast")]
    public class Fast : AdapterBase
    {
        private Connection? connection = null;
        private Adapter? config;
        private AdapterCallback? callback;
        private Dictionary<string, ItemInfo> mapId2Info = new Dictionary<string, ItemInfo>();
        private string moduleID = "";
        private string lastConnectErrMsg = "";

        public override bool SupportsScheduledReading => true;

        public override async Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

            this.config = config;
            this.callback = callback;

            PrintLine(config.Address);

            await TryConnect();

            return new Group[0];
        }

        private static VariableRef MakeVarRefFromAddress(string address, string moduleID) {
            try {
                string[]? arr = StdJson.ObjectFromString<string[]>(address);
                if (arr != null && arr.Length == 2 && arr[0].Length > 0 && arr[1].Length > 0) {
                    return VariableRef.Make(moduleID, arr[0], arr[1]);
                }
            }
            catch (Exception) {}
            return VariableRef.Make(moduleID, address, "Value");
        }

        private async Task<bool> TryConnect() {

            if (connection != null) {
                return true;
            }

            if (config == null || string.IsNullOrWhiteSpace(config.Address)) {
                lastConnectErrMsg = "No address configured";
                return false;
            }

            try {
                var (host, port, user, pass, moduleID) = GetLogin(config);
                connection = await HttpConnection.ConnectWithUserLogin(host, port, user, pass, timeoutSeconds: 2);
                lastConnectErrMsg = "";

                this.moduleID = moduleID;

                this.mapId2Info = config.GetAllDataItems().Where(di => !string.IsNullOrEmpty(di.Address)).ToDictionary(
                    item => /* key */ item.ID,
                    item => /* val */ new ItemInfo(item, MakeVarRefFromAddress(item.Address, moduleID)));

                return true;
            }
            catch (Exception exp) {
                Exception baseExp = exp.GetBaseException() ?? exp;
                lastConnectErrMsg = baseExp.Message;
                LogWarn("Connect", "Connection error: " + baseExp.Message, details: baseExp.StackTrace);
                await CloseConnection();
                return false;
            }
        }

        private async Task CloseConnection() {
            if (connection == null) return;
            try {
                await connection.Close();
            }
            catch (Exception) { }
            connection = null;
        }

        public override Task Shutdown() {
            return CloseConnection();
        }

        private static (string host, int port, string user, string pass, string moduleID) GetLogin(Adapter config) {
            string address = config.Address;
            int idxAt = address.IndexOf('@');
            if (idxAt <= 0) throw new Exception("Missing username in Address");
            int idxPortSep = address.LastIndexOf(':');
            if (idxPortSep <= 0 || idxPortSep == address.Length - 1) throw new Exception("Missing port in Address");
            string user = address.Substring(0, idxAt);
            string strPort = address.Substring(idxPortSep + 1);
            if (!int.TryParse(strPort, out int port)) throw new Exception("Failed to extract port from Address");
            string host = address.Substring(idxAt + 1, idxPortSep - idxAt - 1);
            var nvPass = config.Config.FirstOrDefault(nv => nv.Name == "PW");
            if (nvPass.Name != "PW") throw new Exception("Failed to extract password from adapter config");
            var nvModule = config.Config.FirstOrDefault(nv => nv.Name == "Module");
            if (nvModule.Name != "Module") throw new Exception("Failed to extract Module from adapter config");
            return (host, port, user, nvPass.Value, nvModule.Value);
        }

        public override Task<string[]> BrowseAdapterAddress() {
            return Task.FromResult(new string[0]);
        }

        public override async Task<string[]> BrowseDataItemAddress(string? idOrNull) {

            if (!await TryConnect() || connection == null) {
                return new string[0];
            }

            var allObjects = await connection.GetAllObjects(moduleID);

            var result = new List<string>();
            foreach (ObjectInfo obj in allObjects) {
                foreach (Variable v in obj.Variables) {
                    string objID = obj.ID.LocalObjectID;
                    string varName = v.Name;
                    if (varName == "Value") {
                        result.Add(objID);
                    }
                    else {
                        string[] objVar = new string[] { objID, varName };
                        result.Add(StdJson.ValueToString(objVar));
                    }
                }
            }

            return result.ToArray();
        }

        public override async Task<BrowseDataItemsResult> BrowseDataItems() {

            if (connection == null) {
                string endpoint = config?.Address ?? "";
                string msg = $"No connection to server '{endpoint}': " + lastConnectErrMsg;
                return new BrowseDataItemsResult(
                    supportsBrowsing: true,
                    browsingError: msg,
                    items: new DataItemBrowseInfo[0],
                    clientCertificate: "");
            }

            var allObjects = await connection.GetAllObjects(moduleID);

            var result = new List<DataItemBrowseInfo>();
            foreach (ObjectInfo obj in allObjects) {
                foreach (Variable v in obj.Variables) {
                    string objID = obj.ID.LocalObjectID;
                    string varName = v.Name;
                    if (varName == "Value") {
                        result.Add(new DataItemBrowseInfo(objID, new[] { objID }));
                    }
                    else {
                        string[] objVar = new string[] { objID, varName };
                        string id = StdJson.ValueToString(objVar);
                        result.Add(new DataItemBrowseInfo(id, objVar));
                    }
                }
            }

            return new BrowseDataItemsResult(
                    supportsBrowsing: true,
                    browsingError: "",
                    items: result.ToArray(),
                    clientCertificate: "");
        }

        public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

            if (!await TryConnect() || connection == null) {
                return GetBadVTQs(items);
            }

            var readHelper = new ReadManager<VariableRef, VariableValue>(items, readRequest => mapId2Info[readRequest.ID].VarRef);
            List<VariableRef> dataItemsToRead = readHelper.GetRefsList();

            try {
                List<VariableValue> readResponse = await connection.ReadVariablesSyncIgnoreMissing(dataItemsToRead);

                if (readResponse.Count == dataItemsToRead.Count) {
                    readHelper.SetAllResults(readResponse, (vv, request) => vv.Value);
                    return readHelper.values;
                }
                else {
                    var badDataItems = new List<DataItem>();
                    for (int i = 0; i < dataItemsToRead.Count; ++i) {
                        VariableRef v = dataItemsToRead[i];
                        try {
                            VariableValue value = readResponse.First(rr => rr.Variable == v);
                            readHelper.SetSingleResult(i, value.Value);
                        }
                        catch (Exception) { // not found
                            ReadRequest req = readHelper.GetReadRequest(i);
                            readHelper.SetSingleResult(i, VTQ.Make(req.LastValue.V, Timestamp.Now, Quality.Bad));
                            DataItem dataItem = mapId2Info[req.ID].Item;
                            badDataItems.Add(dataItem);
                        }
                    }

                    string[] dataItemNamesWithAddresss = badDataItems.Select(di => di.Name + ": " + di.Address).ToArray();
                    string details = string.Join("; ", dataItemNamesWithAddresss);
                    string msg = badDataItems.Count == 1 ? $"Invalid address for data item '{badDataItems[0].Name}': {badDataItems[0].Address}" : $"Invalid address for {badDataItems.Count} data items";
                    LogError("Invalid_Addr", msg, badDataItems.Select(di => di.ID).ToArray(), details);

                    return readHelper.values;
                }
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                LogWarn("ReadExcept", $"Read exception: {e.Message}", details: e.ToString());
                Task ignored = CloseConnection();
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

        public override async Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout) {

            if (!await TryConnect() || connection == null) {
                var failed = values.Select(div => new FailedDataItemWrite(div.ID, "No connection to server")).ToArray();
                return WriteDataItemsResult.Failure(failed);
            }

            var writeMan = new WriteManager<VariableValue, VariableError>(values, request => {
                if (mapId2Info.ContainsKey(request.ID)) {
                    ItemInfo info = mapId2Info[request.ID];
                    return VariableValue.Make(info.VarRef, request.Value);
                }
                else {
                    throw new Exception("No Address defined");
                }
            });

            try {
                var dataItemsToWrite = writeMan.GetRefsList();
                WriteResult res = await connection.WriteVariablesSyncIgnoreMissing(dataItemsToWrite, timeout);
                if (!res.IsOK()) {
                    writeMan.AddWriteErrors(res.FailedVariables, failedVar => {
                        VariableRef v = failedVar.Variable;
                        int idx = dataItemsToWrite.FindIndexOrThrow(vv => vv.Variable == v);
                        string id = writeMan.GetWriteRequest(idx).ID;
                        return new FailedDataItemWrite(id, failedVar.Error);
                    });
                }
            }
            catch (Exception exp) {
                Task ignored = CloseConnection();
                Exception e = exp.GetBaseException() ?? exp;
                string msg = $"Write exception: {e.Message}";
                LogWarn("WriteExcept", msg, details: e.ToString());
                var failed = values.Select(div => new FailedDataItemWrite(div.ID, msg)).ToArray();
                return WriteDataItemsResult.Failure(failed);
            }

            return writeMan.GetWriteResult();
        }

        private void PrintLine(string msg) {
            string name = config?.Name ?? "";
            Console.WriteLine(name + ": " + msg);
        }

        private void LogWarn(string type, string msg, string[]? dataItems = null, string? details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Warning,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = dataItems ?? new string[0]
            };

            callback?.Notify_AlarmOrEvent(ae);
        }

        private void LogError(string type, string msg, string[]? dataItems = null, string? details = null) {

            var ae = new AdapterAlarmOrEvent() {
                Time = Timestamp.Now,
                Severity = Severity.Alarm,
                Type = type,
                Message = msg,
                Details = details ?? "",
                AffectedDataItems = dataItems ?? new string[0]
            };

            callback?.Notify_AlarmOrEvent(ae);
        }
    }

    internal class ItemInfo
    {
        public DataItem Item { get; private set; }
        public VariableRef VarRef { get; private set; }

        public ItemInfo(DataItem item, VariableRef varRef) {
            Item = item;
            VarRef = varRef;
        }
    }
}
