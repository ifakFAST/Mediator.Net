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
    private readonly QueryInfo queryPublish;
    private readonly QueryInfo? queryTagID2Identifier;
    private readonly QueryInfo? queryRegisterTag;
    private readonly bool publishRequiresIdentifier;
    private readonly Dictionary<VariableRef, object> mapVariable2Identifier = [];
    private readonly HashSet<VariableRef> registeredVariables = [];
    private readonly HashSet<VariableRef> warnedMissingIdentifier = [];
    private bool warnedMissingLookupQuery = false;

    public SQLPubVar(string dataFolder, SQLConfig config) :
        base(dataFolder, config.VarPublish!.BufferIfOffline) {
        this.config = config;
        varPub = config.VarPublish!;
        queryPublish = ParseQuery(varPub.QueryPublish);
        queryTagID2Identifier = ParseOptionalQuery(varPub.QueryTagID2Identifier);
        queryRegisterTag = ParseOptionalQuery(varPub.QueryRegisterTag);
        publishRequiresIdentifier = UsesStrictIdentifier(queryPublish);
        Start();
    }

    public override async Task OnConfigChanged() {
        registeredVariables.Clear(); // variables will be re-registered on next send
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

        if (publishRequiresIdentifier && queryTagID2Identifier == null && !warnedMissingLookupQuery) {
            warnedMissingLookupQuery = true;
            Console.Error.WriteLine("SQLPubVar: QueryPublish uses @identifier but QueryTagID2Identifier is empty.");
        }

        var sw = Stopwatch.StartNew();

        BatchResult batchResult = await RunBatch(connection, changedValues);

        sw.Stop();

        if (batchResult.OK) {
            foreach (var vv in batchResult.SentValues) {
                lastSentValues[vv.Variable] = vv.Value;
            }
        }

        if (varPub.LogWrites) {
            Console.WriteLine($"SQLPubVar: Sent {batchResult.SentValues.Count} values in {sw.ElapsedMilliseconds} ms");
        }

        return batchResult.OK;
    }

    private readonly record struct BatchResult(
        bool OK,
        VariableValues SentValues
    );

    private async Task<BatchResult> RunBatch(DbConnection connection, VariableValues changedValues) {
        
        try {

            DbBatch batch = CreateBatch(connection);
            var sentValues = new VariableValues();

            foreach (VariableValue vv in changedValues) {

                try {

                    VarInfo v = GetVariableInfoOrThrow(vv.Variable);
                    object? identifier = await ResolveIdentifier(connection, vv, v);
                    if (identifier != null) {
                        warnedMissingIdentifier.Remove(vv.Variable);
                    }

                    if (publishRequiresIdentifier && identifier == null) {
                        if (warnedMissingIdentifier.Add(vv.Variable)) {
                            Console.Error.WriteLine($"SQLPubVar: Skipping variable '{vv.Variable}' because no identifier could be resolved.");
                        }
                        continue;
                    }

                    DbBatchCommand cmd = batch.CreateBatchCommand();
                    cmd.CommandText = MakeQuery(queryPublish, v, vv, identifier);

                    AddDynamicParameters(cmd.Parameters, queryPublish.DynamicParameters, vv, v, identifier);

                    batch.BatchCommands.Add(cmd);
                    sentValues.Add(vv);
                }
                catch (Exception exp) {
                    Console.Error.WriteLine($"Error creating command for variable '{vv.Variable}': {exp.Message}");
                    continue;
                }
            }

            if (batch.BatchCommands.Count == 0) {
                return new BatchResult(OK: true, SentValues: sentValues);
            }

            await batch.ExecuteNonQueryAsync();
            
            return new BatchResult(OK: true, SentValues: sentValues);
        }
        catch (Exception exp) {
            Console.Error.WriteLine($"SQLPubVar: Error executing batch: {exp.Message}");
            //logger.Error(ex, "Creating channels failed: " + ex.Message);
            return new BatchResult(OK: false, SentValues: []);
        }
    }

    private async Task<object?> ResolveIdentifier(DbConnection connection, VariableValue vv, VarInfo v) {

        VariableRef variable = vv.Variable;

        mapVariable2Identifier.TryGetValue(variable, out object? identifier);

        if (identifier == null && queryTagID2Identifier != null) {
            identifier = await ExecuteScalar(connection, queryTagID2Identifier, vv, v, null);
            if (identifier != null) {
                mapVariable2Identifier[variable] = identifier;
            }
        }

        if (queryRegisterTag != null && !registeredVariables.Contains(variable)) {
            await ExecuteNonQuery(connection, queryRegisterTag, vv, v, identifier);
            registeredVariables.Add(variable);

            if (identifier == null && queryTagID2Identifier != null) {
                identifier = await ExecuteScalar(connection, queryTagID2Identifier, vv, v, null);
                if (identifier != null) {
                    mapVariable2Identifier[variable] = identifier;
                }
            }
        }

        return identifier;
    }

    private async Task<object?> ExecuteScalar(
        DbConnection connection,
        QueryInfo query,
        VariableValue vv,
        VarInfo v,
        object? identifier) {

        string cmdText = MakeQuery(query, v, vv, identifier);
        using DbCommand cmd = CreateCommand(connection, cmdText);
        AddDynamicParameters(cmd.Parameters, query.DynamicParameters, vv, v, identifier);
        object? result = await cmd.ExecuteScalarAsync();
        return result is DBNull ? null : result;
    }

    private async Task ExecuteNonQuery(
        DbConnection connection,
        QueryInfo query,
        VariableValue vv,
        VarInfo v,
        object? identifier) {

        string cmdText = MakeQuery(query, v, vv, identifier);
        using DbCommand cmd = CreateCommand(connection, cmdText);
        AddDynamicParameters(cmd.Parameters, query.DynamicParameters, vv, v, identifier);
        await cmd.ExecuteNonQueryAsync();
    }

    private void AddDynamicParameters(
        DbParameterCollection parameters,
        ParamInfo[] parameterInfos,
        VariableValue vv,
        VarInfo v,
        object? identifier) {

        foreach (ParamInfo param in parameterInfos) {
            string name  = param.Name;
            object value = param.Function(vv, v, identifier) ?? DBNull.Value;
            parameters.Add(CreateParameter(name, value));
        }
    }

    private static string MakeQuery(QueryInfo query, VarInfo v, VariableValue vv, object? identifier) {
        string result = query.Query;
        foreach (ParamInfo param in query.StaticParameters) {
            string name = param.Name;
            object value = param.Function(vv, v, identifier) ?? throw new Exception($"Static parameter '{name}' has null value");
            result = result.Replace($"@@{name}", value.ToString());
        }
        return result;
    }

    private sealed record QueryInfo(
        string Query,
        ParamInfo[] StaticParameters,
        ParamInfo[] DynamicParameters
    );

    private static QueryInfo ParseQuery(string query) {
        return new QueryInfo(
            Query: query,
            StaticParameters: GetStaticParameterFunctions(query).ToArray(),
            DynamicParameters: GetDynamicParameterFunctions(query).ToArray()
        );
    }

    private static QueryInfo? ParseOptionalQuery(string query) {
        if (string.IsNullOrWhiteSpace(query)) {
            return null;
        }
        return ParseQuery(query);
    }

    private static bool UsesStrictIdentifier(QueryInfo query) {
        return query.DynamicParameters.Any(IsStrictIdentifierParamName)
            || query.StaticParameters.Any(IsStrictIdentifierParamName);
    }

    private static bool IsStrictIdentifierParamName(ParamInfo p) {
        return p.Name.Equals("identifier", StringComparison.OrdinalIgnoreCase)
            || p.Name.Equals("identifier_numeric", StringComparison.OrdinalIgnoreCase);
    }

    private record ParamInfo(
        string Name,
        Func<VariableValue, VarInfo, object?, object?> Function
    );

    private static List<ParamInfo> GetDynamicParameterFunctions(string queryString) {

        var result = new List<ParamInfo>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<Match> matches = Regex.Matches(queryString, pattern: @"(?<!@)@[\w]+");

        foreach (Match match in matches) {
            string paramName = match.Value[1..];
            if (!usedNames.Add(paramName)) {
                continue;
            }
            var function = GetFunctionFromParameterName(paramName);
            result.Add(new(paramName, function));
        }

        return result;
    }

    private static List<ParamInfo> GetStaticParameterFunctions(string queryString) {

        var result = new List<ParamInfo>();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<Match> matches = Regex.Matches(queryString, pattern: @"@@[\w]+");

        foreach (Match match in matches) {
            string paramName = match.Value[2..];
            if (!usedNames.Add(paramName)) {
                continue;
            }
            var function = GetFunctionFromParameterName(paramName);
            result.Add(new(paramName, function));
        }

        return result;
    }

    private static Func<VariableValue, VarInfo, object?, object?> GetFunctionFromParameterName(string paramName) {
        return paramName.ToLowerInvariant() switch {
            "module" => (vv, v, i) => vv.Variable.Object.ModuleID,
            "variable_name" => (vv, v, i) => vv.Variable.Name,
            "full_id" => (vv, v, i) => vv.Variable.Object.ToEncodedString(),
            "id_string" => (vv, v, i) => vv.Variable.Object.LocalObjectID,
            "id_integer" => (vv, v, i) => ParseNumeric(vv.Variable.Object.LocalObjectID),
            "id_split_underscore_0" => (vv, v, i) => vv.Variable.Object.LocalObjectID.Split('_')[0],
            "id_split_underscore_1" => (vv, v, i) => vv.Variable.Object.LocalObjectID.Split('_')[1],
            "id_split_underscore_2" => (vv, v, i) => vv.Variable.Object.LocalObjectID.Split('_')[2],
            "id_split_underscore_0_numeric" => (vv, v, i) => ParseNumeric(vv.Variable.Object.LocalObjectID.Split('_')[0]),
            "id_split_underscore_1_numeric" => (vv, v, i) => ParseNumeric(vv.Variable.Object.LocalObjectID.Split('_')[1]),
            "id_split_underscore_2_numeric" => (vv, v, i) => ParseNumeric(vv.Variable.Object.LocalObjectID.Split('_')[2]),
            "var_name" => (vv, v, i) => vv.Variable.Name,
            "quality_numeric" => (vv, v, i) => (int)vv.Value.Q,
            "quality_string" => (vv, v, i) => vv.Value.Q.ToString(),
            "is_good" => (vv, v, i) => vv.Value.Q == Quality.Good,
            "is_not_bad" => (vv, v, i) => vv.Value.Q != Quality.Bad,
            "unit" => (vv, v, i) => v.Variable.Unit,
            "value_numeric" => (vv, v, i) => vv.Value.V.AsDouble() ?? throw new Exception("value is not numeric"),
            "value_numeric_or_null" => (vv, v, i) => vv.Value.V.AsDouble(),
            "value_json" => (vv, v, i) => vv.Value.V.JSON,
            "time" => (vv, v, i) => vv.Value.T.ToDateTime(),
            "time_utc" => (vv, v, i) => vv.Value.T.ToDateTime(),
            "time_unspecified" => (vv, v, i) => vv.Value.T.ToDateTimeUnspecified(),
            "time_local" => (vv, v, i) => vv.Value.T.ToDateTime().ToLocalTime(),
            "location" => (vv, v, i) => v.Object.Location.ToString(),
            "identifier" => (vv, v, i) => i,
            "identifier_or_id_string" => (vv, v, i) => i ?? vv.Variable.Object.LocalObjectID,
            "identifier_numeric" => (vv, v, i) => ParseNumericValue(i),
            _ => GetObj(paramName)
        };
    }

    private static Func<VariableValue, VarInfo, object?, object?> GetObj(string paramName) {

        const string prefix = "object_member_";

        if (!paramName.StartsWith(prefix)) {
            throw new Exception("Unknown parameter name: " + paramName);
        }

        string member = paramName[prefix.Length..];
        return (vv, v, i) => {
            string obj = v.ObjectValue.Value.JSON;
            Json.Linq.JToken? token = StdJson.JObjectFromString(obj).GetValue(member, StringComparison.OrdinalIgnoreCase);
            if (token is not Json.Linq.JValue jval) return null;
            return jval.Value;
        };
    }

    private static long ParseNumericValue(object? value) {
        if (value == null) {
            throw new Exception("identifier is null");
        }
        return value switch {
            long x => x,
            int x => x,
            short x => x,
            sbyte x => x,
            byte x => x,
            ushort x => x,
            uint x => checked((long)x),
            ulong x => checked((long)x),
            string x => ParseNumeric(x),
            _ => Convert.ToInt64(value, CultureInfo.InvariantCulture)
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
        ClearPerVariableState();
        dbConnection = null;
    }

    private void ClearPerVariableState() {
        mapVariable2Identifier.Clear();
        registeredVariables.Clear();
        warnedMissingIdentifier.Clear();
    }

    private void PrintLine(string msg) {
        Console.WriteLine(config.Name + ": " + msg);
    }
}
