// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp;
using Microsoft.Data.Sqlite;
using SqlUtil;
using Xunit;

namespace MediatorLib_Test.Calc;

public sealed class Test_SqlUtil : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "Test_SqlUtil_" + Guid.NewGuid().ToString("N"));
    private readonly string sourceFile;
    private readonly string targetFile;

    public Test_SqlUtil() {
        Directory.CreateDirectory(dir);
        sourceFile = Path.Combine(dir, "source.db");
        targetFile = Path.Combine(dir, "sub", "target.db"); // directory must be created by the helper
        Exec(sourceFile, """
            CREATE TABLE data (id INTEGER, val REAL, name TEXT, bin BLOB, ts DATETIME);
            INSERT INTO data VALUES (1, 1.5, 'one', x'0102', '2026-01-01 10:00:00');
            INSERT INTO data VALUES (2, 2.5, 'two', NULL, '2026-01-02 10:00:00');
            INSERT INTO data VALUES (3, NULL, 'three', x'03', '2026-01-03 10:00:00');
            """);
    }

    public void Dispose() {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(dir, recursive: true); } catch { }
    }

    private SqlToSQLite Helper() => new(SourceDb.SQLite, $"Data Source={sourceFile}", targetFile, SQLiteJournalMode.WAL);

    [Fact]
    public void Copies_SQLite_rows_with_declared_column_types() {
        long n = Helper().CopyQuery("SELECT id, val, name, bin, ts, id + 1 AS expr FROM data ORDER BY id", "copy");
        Assert.Equal(3, n);

        var types = Query(targetFile, "SELECT name, type FROM pragma_table_info('copy') ORDER BY cid")
            .Select(r => $"{r[0]}:{r[1]}").ToArray();
        Assert.Equal(["id:INTEGER", "val:REAL", "name:TEXT", "bin:BLOB", "ts:DATETIME", "expr:BLOB"], types);

        var rows = Query(targetFile, "SELECT id, val, name, bin FROM copy ORDER BY id");
        Assert.Equal(3, rows.Count);
        Assert.Equal(1L, rows[0][0]);
        Assert.Equal(1.5, rows[0][1]);
        Assert.Equal("one", rows[0][2]);
        Assert.Equal(new byte[] { 1, 2 }, rows[0][3]);
        Assert.Equal(DBNull.Value, rows[1][3]);
        Assert.Equal(DBNull.Value, rows[2][1]);
    }

    [Theory]
    [InlineData("SELECT 1 AS value UNION ALL SELECT '00123'")]
    [InlineData("SELECT 1.5 AS value UNION ALL SELECT 9223372036854775807")]
    [InlineData("SELECT 'text' AS value UNION ALL SELECT 1.1234567890123457")]
    public void Preserves_mixed_SQLite_storage_classes(string sql) {
        var expected = Query(sourceFile, $"SELECT value, typeof(value) FROM ({sql})");

        Assert.Equal(expected.Count, Helper().CopyQuery(sql, "copy"));

        var actual = Query(targetFile, "SELECT value, typeof(value) FROM copy ORDER BY rowid");
        Assert.Equal(expected.Count, actual.Count);
        for (int i = 0; i < expected.Count; i++) {
            Assert.Equal(expected[i], actual[i]);
        }
    }

    [Fact]
    public void Declared_type_affinity_applies_to_queries_on_copied_table() {
        Helper().CopyQuery("SELECT id, name FROM data", "copy");
        Assert.Equal(2L, Assert.Single(Query(targetFile, "SELECT id FROM copy WHERE id = '2'"))[0]);
    }

    [Fact]
    public void SQLite_columns_without_affinity_are_copied_as_BLOB() {
        Exec(sourceFile, """
            CREATE TABLE untyped (x);
            INSERT INTO untyped VALUES ('00123'), (1);
            CREATE TABLE loose (a ANY) STRICT;
            INSERT INTO loose VALUES ('00123'), (1);
            CREATE VIEW v AS SELECT id AS vid, x FROM data JOIN untyped;
            """);
        var helper = Helper();

        foreach (string table in new[] { "untyped", "loose" }) {
            string col = table == "untyped" ? "x" : "a";
            helper.CopyQuery($"SELECT {col} FROM {table}", "copy");
            Assert.Equal("BLOB", Assert.Single(Query(targetFile, "SELECT type FROM pragma_table_info('copy')"))[0]);
            var actual = Query(targetFile, $"SELECT {col}, typeof({col}) FROM copy ORDER BY rowid");
            Assert.Equal(Query(sourceFile, $"SELECT {col}, typeof({col}) FROM {table} ORDER BY rowid"), actual);
        }

        helper.CopyQuery("SELECT vid, x FROM v", "copy");
        var types = Query(targetFile, "SELECT type FROM pragma_table_info('copy') ORDER BY cid").Select(r => r[0]).ToArray();
        Assert.Equal(["INTEGER", "BLOB"], types);
    }

    [Theory]
    [InlineData("INTEGER", "INTEGER")]
    [InlineData("VARCHAR(20)", "VARCHAR(20)")]
    [InlineData("NUMERIC(10, 2)", "NUMERIC(10, 2)")]
    [InlineData("UNSIGNED BIG INT", "UNSIGNED BIG INT")]
    [InlineData("", "BLOB")]
    [InlineData("any", "BLOB")]
    [InlineData("my\"type", "BLOB")]
    [InlineData("INT); DROP TABLE x; --", "BLOB")]
    public void SQLiteColumnType_copies_safe_declared_types(string declaredType, string expected) {
        Assert.Equal(expected, SqlToSQLite.SQLiteColumnType(declaredType));
    }

    [Theory]
    [InlineData(typeof(bool), "INTEGER")]
    [InlineData(typeof(byte), "INTEGER")]
    [InlineData(typeof(short), "INTEGER")]
    [InlineData(typeof(int), "INTEGER")]
    [InlineData(typeof(int?), "INTEGER")]
    [InlineData(typeof(long), "INTEGER")]
    [InlineData(typeof(ulong), "BLOB")]
    [InlineData(typeof(float), "REAL")]
    [InlineData(typeof(double), "REAL")]
    [InlineData(typeof(decimal), "REAL")]
    [InlineData(typeof(byte[]), "BLOB")]
    [InlineData(typeof(object), "BLOB")]
    [InlineData(typeof(string), "TEXT")]
    [InlineData(typeof(char), "TEXT")]
    [InlineData(typeof(Guid), "TEXT")]
    [InlineData(typeof(DateTime), "TEXT")]
    [InlineData(typeof(DateTimeOffset), "TEXT")]
    [InlineData(typeof(TimeSpan), "TEXT")]
    public void MapType_maps_field_types(Type type, string expected) {
        Assert.Equal(expected, SqlToSQLite.MapType(type));
    }

    [Fact]
    public void Passes_query_parameters() {
        var parameters = new Dictionary<string, object> {
            ["minId"] = 2,
            ["@name"] = "three",
        };
        long n = Helper().CopyQuery("SELECT id FROM data WHERE id >= @minId AND name = @name", "copy", parameters);
        Assert.Equal(1, n);
        Assert.Equal(3L, Query(targetFile, "SELECT id FROM copy")[0][0]);
    }

    [Fact]
    public void Timestamp_parameter_is_passed_as_DateTime() {
        var parameters = new Dictionary<string, object> { ["since"] = Timestamp.FromComponents(2026, 1, 2, 0, 0, 0) };
        long n = Helper().CopyQuery("SELECT id FROM data WHERE ts > @since", "copy", parameters);
        Assert.Equal(2, n);
    }

    [Fact]
    public void Replaces_existing_table() {
        var helper = Helper();
        helper.CopyQuery("SELECT id, name FROM data", "copy");
        helper.CopyQuery("SELECT val AS other FROM data WHERE id = 1", "copy");

        var columns = Query(targetFile, "SELECT name FROM pragma_table_info('copy')").Select(r => r[0]).ToArray();
        Assert.Equal(["other"], columns);
        Assert.Single(Query(targetFile, "SELECT * FROM copy"));
    }

    [Fact]
    public void Invalid_query_keeps_existing_table() {
        var helper = Helper();
        helper.CopyQuery("SELECT id FROM data", "copy");
        Assert.ThrowsAny<Exception>(() => helper.CopyQuery("SELECT id FROM missing_table", "copy"));
        Assert.Equal(3, Query(targetFile, "SELECT * FROM copy").Count);
    }

    [Fact]
    public void Abort_rolls_back_and_keeps_existing_table() {
        var helper = Helper();
        helper.CopyQuery("SELECT id FROM data", "copy");

        var api = new Api { abortStep = true };
        Assert.Throws<OperationCanceledException>(() => helper.CopyQuery("SELECT name FROM data WHERE id = 1", "copy", api: api));

        var columns = Query(targetFile, "SELECT name FROM pragma_table_info('copy')").Select(r => r[0]).ToArray();
        Assert.Equal(["id"], columns);
        Assert.Equal(3, Query(targetFile, "SELECT * FROM copy").Count);
    }

    [Fact]
    public void Creates_indexes() {
        Helper().CopyQuery("SELECT id, name FROM data", "copy", indexColumns: ["name"]);
        var indexes = Query(targetFile, "SELECT name FROM pragma_index_list('copy')").Select(r => r[0]).ToArray();
        Assert.Equal(["ix_copy_name"], indexes);

        // Index is dropped with the table and recreated on the next copy:
        Helper().CopyQuery("SELECT id, name FROM data", "copy", indexColumns: ["name"]);
    }

    [Fact]
    public void Duplicate_index_columns_create_one_index() {
        Helper().CopyQuery("SELECT id, name FROM data", "copy", indexColumns: ["name", "NAME", "name"]);
        var indexes = Query(targetFile, "SELECT name FROM pragma_index_list('copy')").Select(r => r[0]).ToArray();
        Assert.Equal(["ix_copy_name"], indexes);
    }

    [Fact]
    public void Index_names_do_not_collide_between_tables() {
        var helper = Helper();
        for (int i = 0; i < 2; i++) {
            helper.CopyQuery("SELECT id AS b_c FROM data", "a", indexColumns: ["b_c"]);
            helper.CopyQuery("SELECT id AS c FROM data", "a_b", indexColumns: ["c"]);

            var indexA = Assert.Single(Query(targetFile, "SELECT name FROM pragma_index_list('a')"))[0];
            var indexAB = Assert.Single(Query(targetFile, "SELECT name FROM pragma_index_list('a_b')"))[0];
            Assert.NotEqual(indexA, indexAB);
            Assert.Equal("b_c", Assert.Single(Query(targetFile, $"SELECT name FROM pragma_index_info('{indexA}')"))[0]);
            Assert.Equal("c", Assert.Single(Query(targetFile, $"SELECT name FROM pragma_index_info('{indexAB}')"))[0]);
            Assert.Equal(3, Query(targetFile, "SELECT * FROM a").Count);
            Assert.Equal(3, Query(targetFile, "SELECT * FROM a_b").Count);
        }
    }

    [Fact]
    public void Index_names_do_not_collide_with_existing_schema_objects() {
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
        Exec(targetFile, "CREATE TABLE IX_copy_name (id INTEGER); INSERT INTO IX_copy_name VALUES (42);");

        Helper().CopyQuery("SELECT name FROM data", "copy", indexColumns: ["name"]);

        var index = Assert.Single(Query(targetFile, "SELECT name FROM pragma_index_list('copy')"))[0];
        Assert.Equal("name", Assert.Single(Query(targetFile, $"SELECT name FROM pragma_index_info('{index}')"))[0]);
        Assert.Equal(42L, Assert.Single(Query(targetFile, "SELECT id FROM IX_copy_name"))[0]);
    }

    [Theory]
    [InlineData(SourceDb.SQLServer, "Server=localhost", 3)]
    [InlineData(SourceDb.MySQL, "Server=localhost", 3)]
    [InlineData(SourceDb.PostgreSQL, "Host=localhost", 3)]
    [InlineData(SourceDb.SQLServer, "Server=localhost;User ID=timeout_user", 3)]
    [InlineData(SourceDb.MySQL, "Server=localhost;User ID=timeout_user", 3)]
    [InlineData(SourceDb.PostgreSQL, "Host=localhost;Username=timeout_user", 3)]
    [InlineData(SourceDb.MySQL, "Server=localhost;Default Command Timeout=4", 3)]
    [InlineData(SourceDb.PostgreSQL, "Host=localhost;Command Timeout=4", 3)]
    [InlineData(SourceDb.SQLServer, "Server=localhost;Connect Timeout=0", 0)]
    [InlineData(SourceDb.MySQL, "Server=localhost;Connection Timeout=0", 0)]
    [InlineData(SourceDb.PostgreSQL, "Host=localhost;Timeout=0", 0)]
    [InlineData(SourceDb.SQLServer, "Server=localhost;Connect Timeout=8", 8)]
    [InlineData(SourceDb.MySQL, "Server=localhost;Connection Timeout=8", 8)]
    [InlineData(SourceDb.PostgreSQL, "Host=localhost;Timeout=8", 8)]
    [InlineData(SourceDb.SQLServer, "Server=localhost;cOnNeCtIoN tImEoUt=8", 8)]
    [InlineData(SourceDb.MySQL, "Server=localhost;cOnNeCt tImEoUt=8", 8)]
    [InlineData(SourceDb.PostgreSQL, "Host=localhost;tImEoUt=8", 8)]
    public void Uses_configured_connect_timeout_unless_explicitly_specified(SourceDb sourceType, string connectionString, int expected) {
        var helper = new SqlToSQLite(sourceType, connectionString, targetFile, SQLiteJournalMode.WAL) { ConnectTimeoutSeconds = 3 };

        using DbConnection connection = helper.CreateSourceConnection();

        Assert.Equal(expected, connection.ConnectionTimeout);
    }

    [Fact]
    public void Unknown_index_column_throws() {
        Assert.ThrowsAny<Exception>(() => Helper().CopyQuery("SELECT id FROM data", "copy", indexColumns: ["name"]));
    }

    [Fact]
    public void Duplicate_column_names_throw() {
        Assert.ThrowsAny<Exception>(() => Helper().CopyQuery("SELECT id, name AS ID FROM data", "copy"));
    }

    [Fact]
    public void Quotes_identifiers() {
        long n = Helper().CopyQuery("SELECT id AS \"my \"\"id\"\"\" FROM data", "my \"table\"");
        Assert.Equal(3, n);
        Assert.Equal(3, Query(targetFile, "SELECT \"my \"\"id\"\"\" FROM \"my \"\"table\"\"\"").Count);
    }

    [Fact]
    public void ConvertValue_maps_non_native_types() {
        Assert.Equal(1.25, SqlToSQLite.ConvertValue(1.25m));
        Assert.Equal("x", SqlToSQLite.ConvertValue('x'));
        Guid g = Guid.NewGuid();
        Assert.Equal(g.ToString(), SqlToSQLite.ConvertValue(g));
        Assert.Equal(DBNull.Value, SqlToSQLite.ConvertValue(null));
        Assert.Equal(5L, SqlToSQLite.ConvertValue(5UL));
        Assert.Equal("18446744073709551615", SqlToSQLite.ConvertValue(ulong.MaxValue));
        Assert.Equal(DBNull.Value, SqlToSQLite.ConvertValue(double.NaN));
        Assert.Equal(DBNull.Value, SqlToSQLite.ConvertValue(float.NaN));
        Assert.Equal(double.PositiveInfinity, SqlToSQLite.ConvertValue(double.PositiveInfinity));
        Assert.Equal("127.0.0.1", SqlToSQLite.ConvertValue(System.Net.IPAddress.Loopback));
    }

    [Fact]
    public void ConvertValue_stores_collections_as_JSON() {
        Assert.Equal("[1,2]", SqlToSQLite.ConvertValue(new[] { 1, 2 }));
        Assert.Equal("[\"Größe\",null]", SqlToSQLite.ConvertValue(new[] { "Größe", null }));
        Assert.Equal("[1.5,\"NaN\"]", SqlToSQLite.ConvertValue(new[] { 1.5, double.NaN }));
        Assert.Equal("{\"a\":\"b\"}", SqlToSQLite.ConvertValue(new Dictionary<string, string> { ["a"] = "b" }));
        Assert.Equal("[true,false]", SqlToSQLite.ConvertValue(new System.Collections.BitArray(new[] { true, false })));
    }

    private sealed class NoToString { }
    private struct NoToStringStruct { }

    [Fact]
    public void ConvertValue_rejects_values_without_text_representation() {
        Assert.Throws<NotSupportedException>(() => SqlToSQLite.ConvertValue(new NoToString()));
        Assert.Throws<NotSupportedException>(() => SqlToSQLite.ConvertValue(new NoToStringStruct()));
        Assert.Throws<NotSupportedException>(() => SqlToSQLite.ConvertValue(new int[2, 2]));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SQLite_source_must_not_be_target_file(bool alternativePath) {
        string source = alternativePath ? Path.Combine(dir, "sub", "..", "sub", "TARGET.db") : targetFile;
        if (alternativePath && !OperatingSystem.IsWindows()) source = Path.Combine(dir, "sub", "..", "sub", "target.db");
        Assert.Throws<ArgumentException>(() => new SqlToSQLite(SourceDb.SQLite, $"Data Source={source}", targetFile, SQLiteJournalMode.WAL));
    }

    [Fact]
    public void Missing_SQLite_source_file_is_not_created() {
        string missing = Path.Combine(dir, "missing.db");
        var helper = new SqlToSQLite(SourceDb.SQLite, $"Data Source={missing}", targetFile, SQLiteJournalMode.WAL);
        Assert.ThrowsAny<Exception>(() => helper.CopyQuery("SELECT 1 AS x", "copy"));
        Assert.False(File.Exists(missing));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(";Pooling=True", true)]
    public void SQLite_source_is_read_only_and_not_pooled_by_default(string extra, bool expectedPooling) {
        var helper = new SqlToSQLite(SourceDb.SQLite, $"Data Source={sourceFile}{extra}", targetFile, SQLiteJournalMode.WAL);
        using DbConnection connection = helper.CreateSourceConnection();
        var builder = new SqliteConnectionStringBuilder(connection.ConnectionString);
        Assert.Equal(SqliteOpenMode.ReadOnly, builder.Mode);
        Assert.Equal(expectedPooling, builder.Pooling);
    }

    [Fact]
    public void Files_are_released_after_copy() {
        Helper().CopyQuery("SELECT id FROM data", "copy");
        File.Delete(targetFile);
        File.Delete(sourceFile);
        Assert.False(File.Exists(targetFile));
        Assert.False(File.Exists(sourceFile));
    }

    [Fact]
    public void Index_uses_spelling_of_result_column() {
        Helper().CopyQuery("SELECT name AS \"Größe\" FROM data", "copy", indexColumns: ["GRÖßE"]);
        var index = Assert.Single(Query(targetFile, "SELECT name FROM pragma_index_list('copy')"))[0];
        Assert.Equal("ix_copy_Größe", index);
        Assert.Equal("Größe", Assert.Single(Query(targetFile, $"SELECT name FROM pragma_index_info('{index}')"))[0]);
    }

    [Theory]
    [InlineData(SQLiteJournalMode.WAL, SQLiteJournalMode.Delete, "delete")]
    [InlineData(SQLiteJournalMode.Delete, SQLiteJournalMode.WAL, "wal")]
    public void Target_file_uses_given_journal_mode(SQLiteJournalMode first, SQLiteJournalMode second, string expected) {
        new SqlToSQLite(SourceDb.SQLite, $"Data Source={sourceFile}", targetFile, first).CopyQuery("SELECT id FROM data", "copy");
        new SqlToSQLite(SourceDb.SQLite, $"Data Source={sourceFile}", targetFile, second).CopyQuery("SELECT id FROM data", "copy");
        Assert.Equal(expected, Assert.Single(Query(targetFile, "PRAGMA journal_mode"))[0]);
        Assert.Equal(3L, Assert.Single(Query(targetFile, "SELECT COUNT(*) FROM copy"))[0]);
    }

    [Fact]
    public void Script_using_SqlUtil_compiles() {
        string code = $$"""
            using SqlUtil;
            // {{Guid.NewGuid()}}
            class Script {
                Api api = new Api();
                SqlToSQLite copy = new(SourceDb.PostgreSQL, "Host=srv;Database=lab", "local.db", SQLiteJournalMode.Delete);
                public void Step(Timestamp t, Duration dt) {
                    long n = copy.CopyQuery("SELECT 1 AS x", "t",
                        new Dictionary<string, object> { ["since"] = t - Duration.FromDays(7) },
                        indexColumns: ["x"], api: api);
                }
            }
            """;
        string usings = "using System; using System.Collections.Generic; using Ifak.Fast.Mediator; using Ifak.Fast.Mediator.Calc.Adapter_CSharp;\n";
        CompileResult res = CompileLib.CSharpCode2Assembly(usings + code, []);
        File.Delete(res.AssemblyFileName);
    }

    private static void Exec(string file, string sql) {
        using var con = new SqliteConnection($"Data Source={file};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static List<object[]> Query(string file, string sql) {
        using var con = new SqliteConnection($"Data Source={file};Pooling=False");
        con.Open();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        var rows = new List<object[]>();
        while (reader.Read()) {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);
            rows.Add(row);
        }
        return rows;
    }
}
