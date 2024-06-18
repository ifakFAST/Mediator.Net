// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using Ifak.Fast.Mediator.IO.Adapter_SQL.DbProvider;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL;

public abstract class SQL_Query_Base : AdapterBase
{
    protected DatabaseProvider db = new PostgreSQLProvider();
    protected DbConnection? dbConnection;

    public override bool SupportsScheduledReading => true;

    protected Adapter config = new();
    protected AdapterCallback? callback;

    protected readonly Dictionary<string, DataItem> mapId2DataItem = [];

    protected AlarmManager alarmConnectivity = new(activationDuration: Duration.FromMinutes(5));

    protected abstract DatabaseProvider.DatabaseType DatabaseType { get; }

    public override async Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        this.config = config;
        this.callback = callback;

        this.db = DatabaseProvider.Create(DatabaseType);

        List<DataItem> dataItems = config.GetAllDataItems();
        mapId2DataItem.Clear();
        foreach (DataItem item in dataItems) {
            mapId2DataItem[item.ID] = item;
        }

        alarmConnectivity = new(activationDuration: config.ConnectionRetryTimeout);

        PrintLine($"Address: {config.Address}");

        await TryOpenDatabase(reportFailureImmediatelly: true);

        return [];
    }

    public override async Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

        int N = items.Count;
        VTQ[] res = new VTQ[N];
        for (int i = 0; i < N; ++i) {
            res[i] = items[i].LastValue;
        }

        bool connected = await TryOpenDatabase();

        if (!connected) {
            bool transient = !alarmConnectivity.IsActivated;
            return transient ? res : BadVTQsFromLastValue(items);
        }

        for (int i = 0; i < N; ++i) {

            ReadRequest req = items[i];
            string itemID = req.ID;
            VTQ lastVal = req.LastValue;
            DataItem item = mapId2DataItem[itemID];

            try {
                res[i] = await ReadDataItemFromDB(item, lastVal);
                ReturnToNormal("ReadDataItems", $"Successful read of DataItem {itemID}", affectedDataItems: itemID);
            }
            catch (Exception exp) {
                Exception e = exp.GetBaseException() ?? exp;
                bool connectionOK = await db.TestConnection(dbConnection);
                if (connectionOK) {
                    LogWarning("ReadDataItems", $"SQL query failed for {itemID}: {e.Message}", itemID);
                }
                else {
                    PrintErrorLine($"Read exception with broken connection (closing connection, returning last values): {e.Message}");
                    CloseDB();
                    break;
                }
            }
        }

        return res;
    }

    protected virtual async Task<VTQ> ReadDataItemFromDB(DataItem item, VTQ lastValue) {

        string query = item.Address;

        ValueWithTime vt = await ReadDataValue(query, item.Type, item.Dimension);
        if (lastValue.V == vt.Value && vt.Time == null) {
            return lastValue;
        }

        Timestamp t = vt.Time.HasValue ? Timestamp.FromDateTime(vt.Time.Value) : Timestamp.Now.TruncateMilliseconds();
        VTQ vtq = VTQ.Make(vt.Value, t, Quality.Good);

        //int N = rows.Count;
        //string firstRow = N == 0 ? "" : StdJson.ObjectToString(rows[0], indented: false);

        //if (N == 0) {
        //    PrintLine($"Read 0 rows for {itemID}");
        //}
        //else if (N == 1) {
        //    PrintLine($"Read 1 row for {itemID}: {firstRow}");
        //}
        //else {
        //    PrintLine($"Read {N} rows for {itemID}. First row: {firstRow}");
        //}

        return vtq;
    }

    private record struct ValueWithTime(
        DataValue Value, 
        DateTime? Time = null
    );

    private async Task<ValueWithTime> ReadDataValue(string query, DataType type, int dimension) {

        using DbCommand cmd = db.CreateCommand(dbConnection!, query);
        using DbDataReader reader = await cmd.ExecuteReaderAsync();

        DateTime? GetOptionalTime() {
            try {
                return reader.GetDateTime("timestamp");
            }
            catch (Exception) {
                return null;
            }
        }

        if (type == DataType.Struct) {

            var rows = new List<JObject>();

            while (reader.Read()) {

                int n = reader.FieldCount;
                JObject objRow = [];

                for (int i = 0; i < n; ++i) {
                    string name = reader.GetName(i);
                    object? value = reader.GetValue(i);
                    if (value is DBNull) {
                        value = null;
                    }
                    objRow[name] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
                }

                if (dimension == 1) {
                    return new ValueWithTime(
                        Value: DataValue.FromObject(objRow, indented: true),
                        Time: GetOptionalTime());
                }

                rows.Add(objRow);
            }

            return new ValueWithTime(
                        Value: DataValue.FromObject(rows, indented: true),
                        Time: null);
        }
        else if (type == DataType.Timeseries) {

            var rows = new List<TimeseriesEntry>();

            while (reader.Read()) {
                DateTime time = reader.GetDateTime("Time");
                object? value = reader.GetValue("Value");
                if (value is DBNull) {
                    value = null;
                }
                Timestamp t = Timestamp.FromDateTime(time);
                DataValue dv = DataValue.FromObject(value);
                rows.Add(new TimeseriesEntry(t, dv));
            }

            return new ValueWithTime(
                        Value: DataValue.FromObject(rows, indented: true),
                        Time: null);
        }
        else {

            var rows = new List<object?>();

            while (reader.Read()) {

                object? value = reader.GetValue(0);

                if (value is DBNull) {
                    value = null;
                }

                if (dimension == 1) {

                    DataValue dv = value is string str && type != DataType.String && StdJson.IsValidJson(str)
                        ? DataValue.FromJSON(str)
                        : DataValue.FromObject(value);

                    return new ValueWithTime(
                        Value: dv, 
                        Time:  GetOptionalTime());
                }

                rows.Add(value);
            }

            return new ValueWithTime(
                        Value: DataValue.FromObject(rows, indented: true),
                        Time:  null);
        }
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

        string[] examples = [
            "SELECT * FROM table_name;",
            "SELECT * FROM table_name WHERE site_id = 1;",
            "SELECT * FROM table_name ORDER BY time DESC LIMIT 1;",
            "SELECT value FROM table_name WHERE tag = 'my_tag' ORDER BY time DESC LIMIT 1;"
        ];
        return Task.FromResult(examples);
    }

    public override Task Shutdown() {
        try {
            CloseDB();
        }
        catch (Exception) { }
        return Task.FromResult(true);
    }

    protected async Task<bool> TryOpenDatabase(bool reportFailureImmediatelly = false) {

        if (string.IsNullOrEmpty(config.Address)) {
            return false;
        }

        if (dbConnection != null) {
            bool ok = await db.TestConnection(dbConnection);
            if (ok) {
                return true;
            }
            else {
                CloseDB();

                // Do not log warning because connection might be lost due to session timeout:
                // PrintLine("DB connection lost. Trying to reconnect...");
            }
        }

        try {
            dbConnection = db.CreateConnection(config.Address, timeoutSeconds: 5);
            
            await dbConnection.OpenAsync();

            const string msg = "Connected to Database.";

            if (alarmConnectivity.ReturnToNormal()) {
                PrintLine(msg);
            }

            ReturnToNormal("OpenDB", msg, connectionUp: true);

            return true;
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            string msg = $"Open database error: {e.Message}";
            if (reportFailureImmediatelly || alarmConnectivity.OnWarning(msg)) {
                LogWarning("OpenDB", msg, connectionDown: true);
            }
            CloseDB();
            return false;
        }
    }

    protected void CloseDB() {
        if (dbConnection == null) return;
        try {
            dbConnection.Close();
        }
        catch (Exception) { }
        dbConnection = null;
    }

    protected void PrintLine(string msg) {
        Console.WriteLine(config.Name + ": " + msg);
    }

    protected void PrintErrorLine(string msg) {
        string name = config.Name ?? "";
        Console.Error.WriteLine(name + ": " + msg);
    }

    protected void LogWarning(string type, string msg, string? dataItem = null, bool connectionDown = false) {

        var ae = new AdapterAlarmOrEvent() {
            Time = Timestamp.Now,
            Severity = Severity.Warning,
            Connection = connectionDown,
            Type = type,
            Message = msg,
            AffectedDataItems = string.IsNullOrEmpty(dataItem) ? [] : [dataItem]
        };

        callback?.Notify_AlarmOrEvent(ae);
    }

    protected void ReturnToNormal(string type, string msg, bool connectionUp = false, params string[] affectedDataItems) {
        AdapterAlarmOrEvent _event = AdapterAlarmOrEvent.MakeReturnToNormal(type, msg, affectedDataItems);
        _event.Connection = connectionUp;
        callback?.Notify_AlarmOrEvent(_event);
    }
}
