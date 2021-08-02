// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL
{
    public abstract class SQL_Query_Base : AdapterBase
    {
        protected abstract Task<bool> TestConnection(DbConnection dbConnection);
        protected abstract DbConnection CreateConnection(string connectionString);
        protected abstract DbCommand CreateCommand(DbConnection dbConnection, string cmdText);

        private DbConnection? dbConnection;

        public override bool SupportsScheduledReading => true;

        private Adapter config = new Adapter();
        private AdapterCallback? callback;

        public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

            this.config = config;
            this.callback = callback;

            return Task.FromResult(new Group[0]);
        }

        public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

            int N = items.Count;
            VTQ[] res = new VTQ[N];
            for (int i = 0; i < N; ++i) {
                res[i] = items[i].LastValue;
            }

            bool connected = await TryOpenDatabase();

            if (!connected) {
                return res;
            }

            Dictionary<string, string> mapId2Address = config.GetAllDataItems().ToDictionary(
                item => /* key */ item.ID,
                item => /* val */ item.Address);

            bool anyError = false;

            for (int i = 0; i < N; ++i) {

                ReadRequest req = items[i];
                string itemID = req.ID;
                VTQ lastVal = req.LastValue;
                string query = mapId2Address[itemID];

                try {
                    res[i] = await ReadDataItemFromDB(itemID, query, lastVal);
                    ReturnToNormal("ReadDataItems", $"Successful read of DataItem {itemID}", itemID);
                }
                catch (Exception exp) {
                    Exception e = exp.GetBaseException() ?? exp;
                    LogWarning("ReadDataItems", $"ReadDataItemFromDB failed for {itemID}: {e.Message}", itemID);
                    anyError = true;
                }
            }

            if (anyError) {
                CloseDB();
            }

            return res;
        }

        private async Task<VTQ> ReadDataItemFromDB(string itemID, string query, VTQ lastValue) {

            var rows = new List<JObject>();

            using (DbCommand cmd = CreateCommand(dbConnection!, query)) {

                using (var reader = await cmd.ExecuteReaderAsync()) {

                    while (reader.Read()) {

                        int n = reader.FieldCount;
                        JObject objRow = new JObject();

                        for (int i = 0; i < n; ++i) {
                            string name = reader.GetName(i);
                            object value = reader.GetValue(i);
                            objRow[name] = JToken.FromObject(value);
                        }

                        rows.Add(objRow);
                    }
                }
            }

            DataValue dataValue = DataValue.FromObject(rows, indented: true);
            if (lastValue.V == dataValue) {
                return lastValue;
            }

            VTQ vtq = VTQ.Make(dataValue, Timestamp.Now.TruncateMilliseconds(), Quality.Good);

            int N = rows.Count;
            string firstRow = N == 0 ? "" : StdJson.ObjectToString(rows[0], indented: false);

            if (N == 0) {
                PrintLine($"Read 0 rows for {itemID}");
            }
            else if (N == 1) {
                PrintLine($"Read 1 row for {itemID}: {firstRow}");
            }
            else {
                PrintLine($"Read {N} rows for {itemID}. First row: {firstRow}");
            }

            return vtq;
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
            return Task.FromResult(new string[0]);
        }

        public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {

            string[] examples = new string[] {
                "SELECT * FROM table_name;",
                "SELECT * FROM table_name WHERE site_id = 1;",
                "SELECT * FROM table_name ORDER BY time DESC LIMIT 1;"
            };
            return Task.FromResult(examples);
        }

        public override Task Shutdown() {
            try {
                CloseDB();
            }
            catch (Exception) { }
            return Task.FromResult(true);
        }

        private async Task<bool> TryOpenDatabase() {

            if (dbConnection != null) {
                bool ok = await TestConnection(dbConnection);
                if (ok) {
                    return true;
                }
                else {
                    CloseDB();
                    PrintLine("DB connection lost. Trying to reconnect...");
                }
            }

            try {
                dbConnection = CreateConnection(config.Address);
                await dbConnection.OpenAsync();
                ReturnToNormal("OpenDB", "Connected to Database.");
                return true;
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                LogWarning("OpenDB", "Open database error: " + e.Message);
                CloseDB();
                return false;
            }
        }

        private void CloseDB() {
            if (dbConnection == null) return;
            try {
                dbConnection.Close();
            }
            catch (Exception) { }
            dbConnection = null;
        }

        private void PrintLine(string msg) {
            Console.WriteLine(config.Name + ": " + msg);
        }

        private void LogError(string type, string msg) {
            callback?.Notify_AlarmOrEvent(AdapterAlarmOrEvent.Alarm(type, msg));
        }

        private void LogWarning(string type, string msg, params string[] affectedDataItems) {
            callback?.Notify_AlarmOrEvent(AdapterAlarmOrEvent.Warning(type, msg, affectedDataItems));
        }

        private void ReturnToNormal(string type, string msg, params string[] affectedDataItems) {
            var it = AdapterAlarmOrEvent.Info(type, msg, affectedDataItems);
            it.ReturnToNormal = true;
            callback?.Notify_AlarmOrEvent(it);
        }
    }
}
