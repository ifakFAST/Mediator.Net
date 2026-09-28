// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;

namespace SqlUtil;

public enum SourceDb
{
    SQLServer,
    MySQL,
    PostgreSQL,
    SQLite,
}

/// <summary>Journal mode of the target SQLite file. The mode is stored in the file and applies to all connections.</summary>
public enum SQLiteJournalMode
{
    /// <summary>Write-ahead log: readers are not blocked by the copy. Creates the files &lt;file&gt;-wal and &lt;file&gt;-shm
    /// while the database is open. Not supported for files on a network share.</summary>
    WAL,
    /// <summary>Rollback journal (SQLite default): readers are blocked once the copy transaction outgrows the page cache.</summary>
    Delete,
}

/// <summary>
/// Copies the result of an SQL SELECT query into a table of a local SQLite database file.
/// The target table is dropped, recreated from the schema of the query result and filled
/// within a single transaction, i.e. on error the previous table content remains intact.
/// </summary>
/// <remarks>
/// <para>
/// For SQLite sources, a result column that is a plain table column gets the declared type of that
/// column, so the target column has the same type affinity. Other result columns (expressions,
/// literals) get BLOB (no type affinity) so that each value retains its storage class, even when
/// a result column contains values of different types.
/// </para>
/// <para>
/// The query is executed as given. Only pass SELECT statements: a statement that returns no rows
/// (e.g. DELETE or UPDATE) is executed on the source database before the copy fails with
/// "Query does not return any columns". Consider using a database user with read-only permissions.
/// </para>
/// <para>
/// Rows are streamed from the source into the target table within a single write transaction, so
/// memory use does not depend on the size of the result. The SQLite file is therefore locked for
/// writing until the query result has been read completely, and other writers wait for up to their
/// busy timeout. With <see cref="SQLiteJournalMode.Delete"/>, readers are also blocked once the
/// transaction outgrows the page cache. <see cref="SQLiteJournalMode.WAL"/> lets readers continue.
/// </para>
/// <para>
/// An SQLite source must not be the target file, because the open source query would block the
/// commit (use CREATE TABLE ... AS SELECT on that file instead). For the same reason, the source
/// query must not read from the target file via ATTACH.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using SqlUtil;
///
/// class Script {
///     Api api = new Api();
///     SqlToSQLite copy = new(SourceDb.PostgreSQL, "Host=srv;Database=lab;Username=u;Password=p", @"D:\Data\local.db", SQLiteJournalMode.WAL);
///
///     public void Step(Timestamp t, Duration dt) {
///         long rows = copy.CopyQuery("SELECT id, ts, value FROM samples WHERE ts > @since", "samples",
///                                    new Dictionary&lt;string, object?&gt; { ["since"] = t - Duration.FromDays(7) },
///                                    indexColumns: ["ts"], api: api);
///     }
/// }
/// </code>
/// </example>
public sealed class SqlToSQLite
{
    private readonly SourceDb sourceType;
    private readonly string sourceConnectionString;
    private readonly string sqliteFile;
    private readonly SQLiteJournalMode journalMode;

    /// <summary>Timeout in seconds for connecting to the source database
    /// (ignored if the connection string already specifies a connection timeout).</summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;

    /// <summary>Timeout in seconds for executing the source query and for waiting on a locked SQLite file.</summary>
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <param name="sourceType">Type of the source database.</param>
    /// <param name="sourceConnectionString">Connection string of the source database.</param>
    /// <param name="sqliteFile">Path of the target SQLite file (created if it does not exist).</param>
    /// <param name="journalMode">Journal mode the target file is switched to before each copy.</param>
    public SqlToSQLite(SourceDb sourceType, string sourceConnectionString, string sqliteFile, SQLiteJournalMode journalMode) {
        if (string.IsNullOrWhiteSpace(sourceConnectionString)) throw new ArgumentException("Source connection string must not be empty", nameof(sourceConnectionString));
        if (string.IsNullOrWhiteSpace(sqliteFile)) throw new ArgumentException("SQLite file name must not be empty", nameof(sqliteFile));
        if (sourceType == SourceDb.SQLite && IsSameFile(sourceConnectionString, sqliteFile)) {
            throw new ArgumentException("The SQLite source database must not be the target file, " +
                                        "use CREATE TABLE ... AS SELECT on that file instead", nameof(sqliteFile));
        }
        this.sourceType = sourceType;
        this.sourceConnectionString = sourceConnectionString;
        this.sqliteFile = sqliteFile;
        this.journalMode = journalMode;
    }

    /// <summary>
    /// Runs <paramref name="selectQuery"/> on the source database and replaces <paramref name="targetTable"/>
    /// in the SQLite file with the result.
    /// </summary>
    /// <param name="selectQuery">The SELECT query. Every result column needs a unique name.</param>
    /// <param name="targetTable">Name of the SQLite table to (re)create.</param>
    /// <param name="parameters">Optional query parameters, referenced as @name in the query.
    /// Timestamp values are passed as UTC DateTime.</param>
    /// <param name="indexColumns">Optional result columns for which an index is created (duplicates are ignored).
    /// The index is named ix_&lt;table&gt;_&lt;column&gt;, with a numeric suffix only if another object already uses that name.</param>
    /// <param name="api">Optional Api object for aborting the copy when the calculation step is aborted.</param>
    /// <returns>The number of rows written.</returns>
    public long CopyQuery(string selectQuery,
                          string targetTable,
                          IDictionary<string, object?>? parameters = null,
                          string[]? indexColumns = null,
                          Api? api = null) {

        if (string.IsNullOrWhiteSpace(selectQuery)) throw new ArgumentException("Query must not be empty", nameof(selectQuery));
        if (string.IsNullOrWhiteSpace(targetTable)) throw new ArgumentException("Target table name must not be empty", nameof(targetTable));

        using DbConnection source = CreateSourceConnection();
        source.Open();

        using DbCommand query = source.CreateCommand();
        query.CommandText = selectQuery;
        query.CommandTimeout = CommandTimeoutSeconds;
        AddParameters(query, parameters);

        using DbDataReader reader = query.ExecuteReader();
        try {
            return CopyRows(source, reader, targetTable, indexColumns, api);
        }
        catch {
            // Disposing a reader with unread rows makes SqlClient, Npgsql and MySqlConnector read
            // the rest of the result set. Cancel the query first, so that an abort returns promptly.
            try {
                query.Cancel();
                reader.Dispose();
            }
            catch { }
            throw;
        }
    }

    private long CopyRows(DbConnection source, DbDataReader reader, string targetTable, string[]? indexColumns, Api? api) {

        int columnCount = reader.FieldCount;
        if (columnCount == 0) throw new Exception("Query does not return any columns");

        // SQLite's field type can vary by row. Affinity based on the first row could
        // coerce later values, e.g. the text '00123' into the integer 123.
        string[]? declaredTypes = sourceType == SourceDb.SQLite ? GetSQLiteDeclaredTypes(source, reader) : null;

        string[] columnNames = new string[columnCount];
        string[] columnTypes = new string[columnCount];
        var columnsByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < columnCount; i++) {
            string name = reader.GetName(i);
            if (string.IsNullOrWhiteSpace(name)) throw new Exception($"Result column {i + 1} has no name, use an alias (... AS name)");
            if (!columnsByName.TryAdd(name, name)) throw new Exception($"Duplicate result column name '{name}', use an alias (... AS name)");
            columnNames[i] = name;
            columnTypes[i] = declaredTypes != null ? SQLiteColumnType(declaredTypes[i]) : MapType(reader.GetFieldType(i));
        }

        // Use the spelling of the result column, because SQLite ignores the case of ASCII letters only.
        string[] indexCols = (indexColumns ?? [])
            .Select(col => columnsByName.TryGetValue(col, out string? name) ? name : throw new Exception($"Index column '{col}' is not a result column of the query"))
            .Distinct()
            .ToArray();

        string? dir = Path.GetDirectoryName(Path.GetFullPath(sqliteFile));
        if (!string.IsNullOrEmpty(dir)) {
            Directory.CreateDirectory(dir);
        }

        var targetConStr = new SqliteConnectionStringBuilder {
            DataSource = sqliteFile,
            DefaultTimeout = CommandTimeoutSeconds,
            Pooling = false, // release the file after the copy
        };

        using var target = new SqliteConnection(targetConStr.ConnectionString);
        target.Open();

        string mode = journalMode switch {
            SQLiteJournalMode.WAL => "WAL",
            SQLiteJournalMode.Delete => "DELETE",
            _ => throw new ArgumentOutOfRangeException(nameof(journalMode), journalMode, "Unknown journal mode"),
        };
        ExecuteNonQuery(target, $"PRAGMA journal_mode={mode};");

        using SqliteTransaction transaction = target.BeginTransaction();

        string table = QuoteIdent(targetTable);
        string columnDefs = string.Join(", ", columnNames.Select((name, i) => $"{QuoteIdent(name)} {columnTypes[i]}"));

        ExecuteNonQuery(target, $"DROP TABLE IF EXISTS {table};");
        ExecuteNonQuery(target, $"CREATE TABLE {table} ({columnDefs});");

        using SqliteCommand insert = target.CreateCommand();
        insert.CommandText = $"INSERT INTO {table} ({string.Join(", ", columnNames.Select(QuoteIdent))}) " +
                             $"VALUES ({string.Join(", ", Enumerable.Range(0, columnCount).Select(i => "$p" + i))});";
        SqliteParameter[] insertParams = new SqliteParameter[columnCount];
        for (int i = 0; i < columnCount; i++) {
            insertParams[i] = insert.Parameters.Add(new SqliteParameter("$p" + i, null));
        }
        insert.Prepare();

        long rowCount = 0;
        while (true) {
            if (rowCount % 1000 == 0 && api != null && api.AbortStep) {
                throw new OperationCanceledException($"Copy to SQLite table '{targetTable}' aborted");
            }
            if (!reader.Read()) break;
            for (int i = 0; i < columnCount; i++) {
                try {
                    insertParams[i].Value = ConvertValue(reader.GetValue(i));
                }
                catch (NotSupportedException e) {
                    throw new NotSupportedException($"Result column '{columnNames[i]}': {e.Message}", e);
                }
            }
            insert.ExecuteNonQuery();
            rowCount++;
        }

        foreach (string col in indexCols) {
            string index = QuoteIdent(GetAvailableIndexName(target, $"ix_{targetTable}_{col}"));
            ExecuteNonQuery(target, $"CREATE INDEX {index} ON {table} ({QuoteIdent(col)});");
        }

        transaction.Commit();
        return rowCount;
    }

    internal DbConnection CreateSourceConnection() {

        string conStr = sourceConnectionString;

        switch (sourceType) {

            case SourceDb.SQLServer:
                var sqlServer = new SqlConnectionStringBuilder(conStr);
                if (!sqlServer.ShouldSerialize("Connect Timeout")) {
                    sqlServer.ConnectTimeout = ConnectTimeoutSeconds;
                }
                return new SqlConnection(sqlServer.ConnectionString);

            case SourceDb.MySQL:
                var mySql = new MySqlConnectionStringBuilder(conStr);
                if (!mySql.ContainsKey("Connection Timeout")) {
                    mySql.ConnectionTimeout = (uint)ConnectTimeoutSeconds;
                }
                return new MySqlConnection(mySql.ConnectionString);

            case SourceDb.PostgreSQL:
                var postgreSql = new NpgsqlConnectionStringBuilder(conStr);
                if (!postgreSql.ShouldSerialize("Timeout")) {
                    postgreSql.Timeout = ConnectTimeoutSeconds;
                }
                return new NpgsqlConnection(postgreSql.ConnectionString);

            case SourceDb.SQLite:
                var sqlite = new SqliteConnectionStringBuilder(conStr);
                if (sqlite.Mode == SqliteOpenMode.ReadWriteCreate) {
                    sqlite.Mode = SqliteOpenMode.ReadOnly; // a mistyped path must not create an empty database file
                }
                if (!sqlite.ShouldSerialize("Pooling")) {
                    sqlite.Pooling = false; // release the file after the copy
                }
                return new SqliteConnection(sqlite.ConnectionString);

            default:
                throw new Exception($"Invalid source database type '{sourceType}'");
        }
    }

    private static void AddParameters(DbCommand cmd, IDictionary<string, object?>? parameters) {
        if (parameters == null) return;
        foreach (var (name, value) in parameters) {
            DbParameter p = cmd.CreateParameter();
            p.ParameterName = name.StartsWith('@') ? name : "@" + name;
            p.Value = value switch {
                null => DBNull.Value,
                Timestamp t => t.ToDateTime(),
                _ => value
            };
            cmd.Parameters.Add(p);
        }
    }

    private static void ExecuteNonQuery(SqliteConnection con, string sql) {
        using SqliteCommand cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static string GetAvailableIndexName(SqliteConnection con, string baseName) {
        // Index names share a database-wide namespace with tables and views. The caller
        // holds a write transaction, so the chosen name stays available until creation.
        using SqliteCommand cmd = con.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE name = $name COLLATE NOCASE LIMIT 1;";
        SqliteParameter nameParam = cmd.Parameters.AddWithValue("$name", baseName);
        string name = baseName;
        for (int suffix = 1; ; suffix++) {
            nameParam.Value = name;
            if (cmd.ExecuteScalar() == null) return name;
            name = $"{baseName}_{suffix}";
        }
    }

    private static string QuoteIdent(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";

    // Returns the declared type of the table column behind each result column,
    // or "" for result columns that are not a plain table column (expressions, literals).
    private static string[] GetSQLiteDeclaredTypes(DbConnection con, DbDataReader reader) {

        string[] res = new string[reader.FieldCount];
        Array.Fill(res, "");

        DataTable? schema = reader.GetSchemaTable();
        if (schema == null) return res;

        using DbCommand cmd = con.CreateCommand();
        for (int i = 0; i < res.Length; i++) {
            DataRow row = schema.Rows[i];
            string table = row[SchemaTableColumn.BaseTableName] as string ?? "";
            string column = row[SchemaTableColumn.BaseColumnName] as string ?? "";
            string dbName = row[SchemaTableColumn.BaseSchemaName] as string ?? "";
            if (table == "" || column == "") continue;

            cmd.Parameters.Clear();
            cmd.CommandText = dbName == ""
                ? "SELECT type FROM pragma_table_info($table) WHERE name = $column COLLATE NOCASE;"
                : "SELECT type FROM pragma_table_info($table, $db) WHERE name = $column COLLATE NOCASE;";
            AddParameter(cmd, "$table", table);
            AddParameter(cmd, "$column", column);
            if (dbName != "") AddParameter(cmd, "$db", dbName);
            res[i] = cmd.ExecuteScalar() as string ?? "";
        }
        return res;
    }

    private static void AddParameter(DbCommand cmd, string name, object value) {
        DbParameter p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value;
        cmd.Parameters.Add(p);
    }

    // The declared type is copied, so the target column has the same affinity as the source
    // column and the (already coerced) source values are stored unchanged. BLOB (no affinity)
    // is used for result columns without a declared type, for ANY (no coercion in STRICT tables,
    // but NUMERIC affinity in ordinary tables) and for unusual type names.
    internal static string SQLiteColumnType(string declaredType) {
        string t = declaredType.Trim();
        if (t == "" || t.Equals("ANY", StringComparison.OrdinalIgnoreCase)) return "BLOB";
        if (!Regex.IsMatch(t, @"^[A-Za-z_][A-Za-z0-9_ ]*(\(\s*[+-]?\d+\s*(,\s*[+-]?\d+\s*)?\))?$")) return "BLOB";
        return t;
    }

    internal static string MapType(Type type) {
        Type t = Nullable.GetUnderlyingType(type) ?? type;
        if (t == typeof(bool) ||
            t == typeof(byte) || t == typeof(sbyte) ||
            t == typeof(short) || t == typeof(ushort) ||
            t == typeof(int) || t == typeof(uint) ||
            t == typeof(long)) {
            return "INTEGER";
        }
        if (t == typeof(ulong)) {
            // Values above long.MaxValue are stored as text. INTEGER affinity would
            // convert that text into an inexact REAL, so use no affinity instead.
            return "BLOB";
        }
        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal)) {
            return "REAL";
        }
        if (t == typeof(byte[])) {
            return "BLOB";
        }
        if (t == typeof(object)) {
            // Values of different types per row (e.g. SQL Server sql_variant):
            // no affinity, so that each value retains its storage class.
            return "BLOB";
        }
        return "TEXT";
    }

    internal static object ConvertValue(object? v) {
        return v switch {
            null or DBNull => DBNull.Value,
            bool or byte or sbyte or short or ushort or int or uint or long => v,
            ulong ul => ul <= long.MaxValue ? (long)ul : ul.ToString(CultureInfo.InvariantCulture),
            float f => float.IsNaN(f) ? DBNull.Value : f, // Microsoft.Data.Sqlite cannot store NaN
            double d => double.IsNaN(d) ? DBNull.Value : d,
            decimal m => (double)m,
            string or byte[] => v,
            char c => c.ToString(),
            Guid g => g.ToString(),
            DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan => v,
            IEnumerable => ToJson(v), // e.g. PostgreSQL arrays, bit(n) and hstore
            _ => ToText(v)
        };
    }

    private static readonly JsonSerializerOptions jsonOptions = new() {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private static string ToJson(object v) {
        try {
            return JsonSerializer.Serialize(v, v.GetType(), jsonOptions);
        }
        catch (NotSupportedException) { // e.g. multi-dimensional arrays
            throw Unsupported(v);
        }
    }

    private static string ToText(object v) {
        // Without an override of ToString(), the value would be stored as its type name.
        Type? declaringType = v.GetType().GetMethod(nameof(ToString), Type.EmptyTypes)?.DeclaringType;
        if (declaringType == null || declaringType == typeof(object) || declaringType == typeof(ValueType)) {
            throw Unsupported(v);
        }
        return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
    }

    private static NotSupportedException Unsupported(object v) {
        return new NotSupportedException($"Values of type {v.GetType()} are not supported, convert them in the query (e.g. CAST(... AS text))");
    }

    private static bool IsSameFile(string sqliteConnectionString, string file) {
        var builder = new SqliteConnectionStringBuilder(sqliteConnectionString);
        string source = builder.DataSource;
        if (builder.Mode == SqliteOpenMode.Memory || source == "" || source == ":memory:" || source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(Path.GetFullPath(source), Path.GetFullPath(file), comparison);
    }
}
