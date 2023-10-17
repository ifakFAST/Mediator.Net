// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish.SQL;

public abstract class SQLPubVar : BufferedVarPub {

    private readonly SQLConfig config;
    private readonly SQLVarPub varPub;
    private readonly ParamInfo[] parametersStatic;
    private readonly ParamInfo[] parametersDynamic;

    public SQLPubVar(string dataFolder, SQLConfig config) :
        base(dataFolder, config.VarPublish!.BufferIfOffline) {
        this.config = config;
        varPub = config.VarPublish!;
        parametersStatic = GetStaticParameterFunctions(varPub.QueryPublish).ToArray();
        parametersDynamic = GetDynamicParameterFunctions(varPub.QueryPublish).ToArray();
        Start();
    }

    protected override string BuffDirName => "SQL_Publish";
    internal override string PublisherID => config.ID;

    protected abstract Task<bool> TestConnection(DbConnection dbConnection);
    protected abstract DbConnection CreateConnection(string connectionString);
    protected abstract DbCommand CreateCommand(DbConnection dbConnection, string cmdText);
    protected abstract DbBatch CreateBatch(DbConnection dbConnection);
    protected abstract DbParameter CreateParameter(string name, object value);

    private DbConnection? dbConnection;

    protected override async Task<bool> DoSend(VariableValues values) {

        bool connected = await TryOpenDatabase();
        if (!connected || dbConnection == null) {
            return false;
        }

        var connection = dbConnection;

        VariableValues changedValues = Util.RemoveEmptyTimestamp(values)
              .Where(v => !lastSentValues.ContainsKey(v.Variable) || lastSentValues[v.Variable] != v.Value)
              .ToList();

        if (changedValues.Count == 0) {
            return true;
        }

        //string id = varPub.QueryTagID2Identifier;
        //string query = "select id from tag_meta where name = '{ID}';";

        var sw = Stopwatch.StartNew();

        bool result = await RunBatch(connection, varPub.QueryPublish, changedValues);

        sw.Stop();

        if (result) {
            foreach (var vv in changedValues) {
                lastSentValues[vv.Variable] = vv.Value;
            }
        }

        Console.WriteLine($"SQLPubVar: Sent {changedValues.Count} values in {sw.ElapsedMilliseconds} ms");

        return result;
    }

    private async Task<bool> RunBatch(DbConnection connection, string query, VariableValues changedValues) {
        
        try {

            DbBatch batch = CreateBatch(connection);

            foreach (VariableValue vv in changedValues) {

                try {

                    VarInfo v = GetVariableInfoOrThrow(vv.Variable);

                    DbBatchCommand cmd = batch.CreateBatchCommand();
                    cmd.CommandText = MakeQuery(query, parametersStatic, v, vv);

                    for (int i = 0; i < parametersDynamic.Length; ++i) {
                        ParamInfo param = parametersDynamic[i];
                        string name  = param.Name;
                        object value = param.Function(vv, v) ?? DBNull.Value;
                        cmd.Parameters.Add(CreateParameter(name, value));
                    }

                    batch.BatchCommands.Add(cmd);
                }
                catch (Exception exp) {
                    Console.Error.WriteLine($"Error creating command for variable '{vv.Variable}': {exp.Message}");
                    continue;
                }
            }

            await batch.ExecuteNonQueryAsync();
            
            return true;
        }
        catch (Exception exp) {
            Console.Error.WriteLine($"SQLPubVar: Error executing batch: {exp.Message}");
            //logger.Error(ex, "Creating channels failed: " + ex.Message);
            return false;
        }
    }

    private static string MakeQuery(string query, ParamInfo[] parametersStatic, VarInfo v, VariableValue vv) {
        string result = query;
        foreach (ParamInfo param in parametersStatic) {
            string name = param.Name;
            object value = param.Function(vv, v) ?? throw new Exception($"Static parameter '{name}' has null value");
            result = result.Replace($"@@{name}", value.ToString());
        }
        return result;
    }

    private record ParamInfo(
        string Name,
        Func<VariableValue, VarInfo, object?> Function
    );

    private static List<ParamInfo> GetDynamicParameterFunctions(string queryString) {

        var result = new List<ParamInfo>();

        IReadOnlyList<Match> matches = Regex.Matches(queryString, pattern: @"(?<!@)@[\w]+");

        foreach (Match match in matches) {
            string paramName = match.Value[1..];
            var function = GetFunctionFromParameterName(paramName);
            result.Add(new(paramName, function));
        }

        return result;
    }

    private static List<ParamInfo> GetStaticParameterFunctions(string queryString) {

        var result = new List<ParamInfo>();

        IReadOnlyList<Match> matches = Regex.Matches(queryString, pattern: @"@@[\w]+");

        foreach (Match match in matches) {
            string paramName = match.Value[2..];
            var function = GetFunctionFromParameterName(paramName);
            result.Add(new(paramName, function));
        }

        return result;
    }

    private static Func<VariableValue, VarInfo, object?> GetFunctionFromParameterName(string paramName) {
        return paramName.ToLowerInvariant() switch {
            "module" => (vv, v) => vv.Variable.Object.ModuleID,
            "variable_name" => (vv, v) => vv.Variable.Name,
            "full_id" => (vv, v) => vv.Variable.Object.ToEncodedString(),
            "id_string" => (vv, v) => vv.Variable.Object.LocalObjectID,
            "id_integer" => (vv, v) => ParseNumeric(vv.Variable.Object.LocalObjectID),
            "id_split_underscore_0" => (vv, v) => vv.Variable.Object.LocalObjectID.Split('_')[0],
            "id_split_underscore_1" => (vv, v) => vv.Variable.Object.LocalObjectID.Split('_')[1],
            "id_split_underscore_2" => (vv, v) => vv.Variable.Object.LocalObjectID.Split('_')[2],
            "id_split_underscore_0_numeric" => (vv, v) => ParseNumeric(vv.Variable.Object.LocalObjectID.Split('_')[0]),
            "id_split_underscore_1_numeric" => (vv, v) => ParseNumeric(vv.Variable.Object.LocalObjectID.Split('_')[1]),
            "id_split_underscore_2_numeric" => (vv, v) => ParseNumeric(vv.Variable.Object.LocalObjectID.Split('_')[2]),
            "var_name" => (vv, v) => vv.Variable.Name,
            "quality_numeric" => (vv, v) => (int)vv.Value.Q,
            "quality_string" => (vv, v) => vv.Value.Q.ToString(),
            "is_good" => (vv, v) => vv.Value.Q == Quality.Good,
            "is_not_bad" => (vv, v) => vv.Value.Q != Quality.Bad,
            "unit" => (vv, v) => v.Variable.Unit,
            "value_numeric" => (vv, v) => vv.Value.V.AsDouble() ?? throw new Exception("value is not numeric"),
            "value_numeric_or_null" => (vv, v) => vv.Value.V.AsDouble(),
            "value_json" => (vv, v) => vv.Value.V.JSON,
            "time" => (vv, v) => vv.Value.T.ToDateTime(),
            "time_utc" => (vv, v) => vv.Value.T.ToDateTime(),
            "time_unspecified" => (vv, v) => vv.Value.T.ToDateTimeUnspecified(),
            "time_local" => (vv, v) => vv.Value.T.ToDateTime().ToLocalTime(),
            "location" => (vv, v) => v.Object.Location.ToString(),
            _ => GetObj(paramName)
        };
    }

    private static Func<VariableValue, VarInfo, object?> GetObj(string paramName) {

        const string prefix = "object_member_";

        if (!paramName.StartsWith(prefix)) {
            throw new Exception("Unknown parameter name: " + paramName);
        }

        string member = paramName[prefix.Length..];
        return (vv, v) => {
            string obj = v.ObjectValue.Value.JSON;
            Json.Linq.JToken? token = StdJson.JObjectFromString(obj).GetValue(member, StringComparison.OrdinalIgnoreCase);
            if (token is not Json.Linq.JValue jval) return null;
            return jval.Value;
        };
    }

    private static long ParseNumeric(string s) => long.Parse(s, CultureInfo.InvariantCulture);

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
            dbConnection = CreateConnection(config.ConnectionString);
            await dbConnection.OpenAsync();
            //ReturnToNormal("OpenDB", "Connected to Database.");
            return true;
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            //LogWarning("OpenDB", "Open database error: " + e.Message);
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
}
