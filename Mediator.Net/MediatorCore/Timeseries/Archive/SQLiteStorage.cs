// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using Ifak.Fast.Mediator.Timeseries.SQLite;
using Ifak.Fast.Mediator.Util;
using NLog;

namespace Ifak.Fast.Mediator.Timeseries.Archive;

/// <summary>
/// SQLite-based storage implementation that organizes data into quarterly database files.
/// Each quarter (e.g., 2025Q1.db) contains data for all channels within that time period.
/// Uses bit-packed INTEGER PRIMARY KEY for efficient storage: (var_id &lt;&lt; 24) | day_number.
/// </summary>
public sealed class SQLiteStorage(string existingBaseFolder, bool readOnly) : StorageBase {

    private static readonly Logger Logger = LogManager.GetLogger("SQLiteArchiveStorage");

    private const long MillisecondsPerDay = 86400000L;

    private readonly string baseFolder = existingBaseFolder;
    private readonly Dictionary<string, QuarterDb> quarterConnections = [];
    private readonly Dictionary<(string quarter, ChannelRef channel), int> variableIdCache = [];

    public override (int dayStart, int dayEnd)? GetStoredDayNumberRange(ChannelRef channel) {

        // Scan all quarter files to find day range for this channel
        string[] dbFiles = Directory.GetFiles(baseFolder, "*.db");
        if (dbFiles.Length == 0) {
            return null;
        }

        int? minDay = null;
        int? maxDay = null;

        foreach (string dbFile in dbFiles) {

            string quarter = Path.GetFileNameWithoutExtension(dbFile);
            QuarterDb qdb = GetOrOpenQuarter(quarter);

            long varId = GetVariableId(qdb, channel);
            if (varId <= 0) {
                continue;
            }

            // Query min and max day_number for this variable
            // Key = (varId << 24) | dayNumber, so we need to extract dayNumber from key
            long minKey = varId << 24;
            long maxKey = (varId + 1) << 24;

            using var cmd = Factory.MakeCommand("SELECT MIN(id), MAX(id) FROM day_data WHERE id >= @1 AND id < @2", qdb.Connection);
            cmd.Parameters.Add(Factory.MakeParameter("@1", minKey));
            cmd.Parameters.Add(Factory.MakeParameter("@2", maxKey));

            using var reader = cmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0)) {
                long minId = reader.GetInt64(0);
                long maxId = reader.GetInt64(1);

                int minDayInQuarter = (int)(minId & 0xFFFFFF);
                int maxDayInQuarter = (int)(maxId & 0xFFFFFF);

                if (minDay == null || minDayInQuarter < minDay) {
                    minDay = minDayInQuarter;
                }
                if (maxDay == null || maxDayInQuarter > maxDay) {
                    maxDay = maxDayInQuarter;
                }
            }
        }

        if (minDay == null || maxDay == null) {
            return null;
        }

        return (minDay.Value, maxDay.Value);
    }

    public override void WriteDayData(ChannelRef channel, int dayNumber, byte[] data) {

        if (readOnly) {
            throw new InvalidOperationException("Cannot write data in read-only mode.");
        }

        string quarter = GetQuarterFromDayNumber(dayNumber);
        QuarterDb qdb = GetOrOpenQuarter(quarter);

        int varId = GetOrCreateVariableId(qdb, channel);
        long key = ComputeKey(varId, dayNumber);

        qdb.StmtUpsertDayData![0] = key;
        qdb.StmtUpsertDayData![1] = data;
        qdb.StmtUpsertDayData!.ExecuteNonQuery();
    }

    public override Stream? ReadDayData(ChannelRef channel, int dayNumber) {

        string quarter = GetQuarterFromDayNumber(dayNumber);
        string dbPath = GetQuarterDbPath(quarter);

        if (!File.Exists(dbPath)) {
            return null;
        }

        QuarterDb qdb = GetOrOpenQuarter(quarter);

        int varId = GetVariableId(qdb, channel);
        if (varId <= 0) {
            return null;
        }

        long key = ComputeKey(varId, dayNumber);

        qdb.StmtGetDayData[0] = key;
        using var reader = qdb.StmtGetDayData.ExecuteReader();

        if (reader.Read() && !reader.IsDBNull(0)) {
            using Stream stream = reader.GetStream(0);
            var mem = MemoryManager.GetMemoryStream("SQLiteStorage_ReadDayData"); // No using here - we return the MemoryStream
            try {
                stream.CopyTo(mem);
                mem.Position = 0;
            } 
            catch (Exception) {
                mem.Dispose();
                throw;
            }
            return mem;
        }

        return null;
    }

    public override void DeleteDayData(ChannelRef channel, int startDayNumberInclusive, int endDayNumberInclusive) {

        if (readOnly) {
            throw new InvalidOperationException("Cannot delete data in read-only mode.");
        }

        // Group day ranges by quarter
        var rangesByQuarter = new Dictionary<string, (int min, int max)>();
        for (int day = startDayNumberInclusive; day <= endDayNumberInclusive; day++) {
            string quarter = GetQuarterFromDayNumber(day);
            if (rangesByQuarter.TryGetValue(quarter, out var range)) {
                rangesByQuarter[quarter] = (Math.Min(range.min, day), Math.Max(range.max, day));
            }
            else {
                rangesByQuarter[quarter] = (day, day);
            }
        }

        foreach (var (quarter, range) in rangesByQuarter) {
            string dbPath = GetQuarterDbPath(quarter);
            if (!File.Exists(dbPath)) {
                continue;
            }

            QuarterDb qdb = GetOrOpenQuarter(quarter);

            int varId = GetVariableId(qdb, channel);
            if (varId <= 0) {
                continue;
            }

            // Delete day range in one pass
            long startKey = ComputeKey(varId, range.min);
            long endKey   = ComputeKey(varId, range.max);
            qdb.StmtDeleteDayDataRange![0] = startKey;
            qdb.StmtDeleteDayDataRange![1] = endKey;
            qdb.StmtDeleteDayDataRange!.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Computes the bit-packed key: (varId &lt;&lt; 24) | dayNumber.
    /// </summary>
    private static long ComputeKey(int varId, int dayNumber) {
        // Convert to long acts as a guard against 32-bit overflow during shift
        long vid = varId;
        long day = dayNumber & 0xFFFFFF;
        return (vid << 24) | day;
    }

    /// <summary>
    /// Converts a day number to a quarter string (e.g., "2025Q1").
    /// </summary>
    private static string GetQuarterFromDayNumber(int dayNumber) {
        Timestamp t = Timestamp.FromJavaTicks((long)dayNumber * MillisecondsPerDay);
        DateTime dt = t.ToDateTime(); // UTC
        int year = dt.Year;
        int quarter = (dt.Month - 1) / 3 + 1;
        return $"{year}Q{quarter}";
    }

    private string GetQuarterDbPath(string quarter) {
        return Path.Combine(baseFolder, $"{quarter}.db");
    }

    private QuarterDb GetOrOpenQuarter(string quarter) {

        if (quarterConnections.TryGetValue(quarter, out var qdb)) {
            return qdb;
        }

        string dbPath = GetQuarterDbPath(quarter);
        qdb = new QuarterDb(dbPath, readOnly);
        quarterConnections[quarter] = qdb;
        return qdb;
    }

    private int GetVariableId(QuarterDb qdb, ChannelRef channel) {
        var cacheKey = (qdb.Quarter, channel);
        if (variableIdCache.TryGetValue(cacheKey, out int cachedId)) {
            return cachedId;
        }

        qdb.StmtGetVariableId[0] = channel.ObjectID;
        qdb.StmtGetVariableId[1] = channel.VariableName;

        using var reader = qdb.StmtGetVariableId.ExecuteReader();
        if (reader.Read()) {
            int id = reader.GetInt32(0);
            variableIdCache[cacheKey] = id;
            return id;
        }

        return -1;
    }

    private int GetOrCreateVariableId(QuarterDb qdb, ChannelRef channel) {

        int id = GetVariableId(qdb, channel);
        if (id > 0) {
            return id;
        }

        qdb.StmtInsertVariable![0] = channel.ObjectID;
        qdb.StmtInsertVariable![1] = channel.VariableName;
        qdb.StmtInsertVariable!.ExecuteNonQuery();

        // Get the inserted ID
        using var cmd = Factory.MakeCommand("SELECT last_insert_rowid()", qdb.Connection);
        long longValue = (long)cmd.ExecuteScalar()!;
        if (longValue <= 0 || longValue > int.MaxValue) {
            throw new InvalidOperationException("Failed to retrieve last inserted variable ID.");
        }
        id = (int)longValue;

        var cacheKey = (qdb.Quarter, channel);
        variableIdCache[cacheKey] = id;
        return id;
    }

    public override bool CanCompact() {
        if (readOnly) {
            return false;
        }
        return quarterConnections.Values.Any(qdb => qdb.CanCompact());
    }

    public override void Compact() {
        if (readOnly) {
            return;
        }
        foreach (var qdb in quarterConnections.Values) {
            if (qdb.CanCompact()) {
                qdb.Compact();
            }
        }
    }

    public override void Dispose() {
        foreach (var qdb in quarterConnections.Values) {
            qdb.Dispose();
        }
        quarterConnections.Clear();
        variableIdCache.Clear();
    }

    /// <summary>
    /// Represents a single quarterly SQLite database connection and its prepared statements.
    /// </summary>
    private sealed class QuarterDb : IDisposable {

        public readonly string Quarter;
        public readonly DbConnection Connection;
        public readonly PreparedStatement StmtGetVariableId;
        public readonly PreparedStatement? StmtInsertVariable;
        public readonly PreparedStatement StmtGetDayData;
        public readonly PreparedStatement? StmtUpsertDayData;
        public readonly PreparedStatement? StmtDeleteDayDataRange;

        public QuarterDb(string dbPath, bool readOnly) {
            Quarter = Path.GetFileNameWithoutExtension(dbPath);
            string connectionString = $"Filename=\"{dbPath}\";Pooling=False;";
            //if (readOnly) {
            //    connectionString += ";Mode=ReadOnly";
            //}

            Connection = Factory.MakeConnection(connectionString);
            Connection.Open();

            if (!readOnly) {
                // Set pragmas for performance
                using (var cmd = Factory.MakeCommand("PRAGMA page_size=16384; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;", Connection)) {
                    cmd.ExecuteNonQuery();
                }
                CreateTablesIfNotExist();
            }

            StmtGetVariableId = new PreparedStatement(Connection, "SELECT id FROM variables WHERE object_id = @1 AND variable_name = @2", 2);
            StmtGetDayData    = new PreparedStatement(Connection, "SELECT data FROM day_data WHERE id = @1", 1);

            if (readOnly) {
                StmtInsertVariable = null!;
                StmtUpsertDayData = null!;
                StmtDeleteDayDataRange = null!;
            }
            else {
                StmtInsertVariable     = new PreparedStatement(Connection,  "INSERT INTO variables (object_id, variable_name) VALUES (@1, @2)", 2);
                StmtUpsertDayData      = new PreparedStatement(Connection, @"INSERT INTO day_data (id, data) VALUES (@1, @2)
                                                                             ON CONFLICT(id) DO UPDATE SET data = @2", 2);
                StmtDeleteDayDataRange = new PreparedStatement(Connection,  "DELETE FROM day_data WHERE id >= @1 AND id <= @2", 2);
            }
        }

        private void CreateTablesIfNotExist() {
            using var cmd = Factory.MakeCommand(@"
                CREATE TABLE IF NOT EXISTS variables (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    object_id TEXT NOT NULL,
                    variable_name TEXT NOT NULL,
                    UNIQUE(object_id, variable_name)
                );

                CREATE TABLE IF NOT EXISTS day_data (
                    id INTEGER PRIMARY KEY,
                    data BLOB NOT NULL
                );
            ", Connection);
            cmd.ExecuteNonQuery();
        }

        public bool CanCompact() {
            double? freeSpacePercent = FreeSpacePercent();
            return freeSpacePercent.HasValue && freeSpacePercent.Value > 20.0;
        }

        public void Compact() {
            try {
                using var cmd = Factory.MakeCommand("VACUUM;", Connection);
                cmd.ExecuteNonQuery();
            }
            catch (Exception exp) {
                Logger.Warn($"Failed to compact database for quarter {Quarter}: {exp.Message}");
            }
        }

        public void Dispose() {
            StmtGetVariableId?.Reset();
            StmtInsertVariable?.Reset();
            StmtGetDayData?.Reset();
            StmtUpsertDayData?.Reset();
            StmtDeleteDayDataRange?.Reset();

            try { Connection.Close(); } catch { }
            try { Connection.Dispose(); } catch { }
        }

        public double? FreeSpacePercent() {
            try {
                long freeListCount = 0;
                long pageCount = 0;

                using (var command = Factory.MakeCommand("PRAGMA freelist_count;", Connection)) {
                    object? res = command.ExecuteScalar();
                    if (res != null) freeListCount = Convert.ToInt64(res);
                }

                using (var command = Factory.MakeCommand("PRAGMA page_count;", Connection)) {
                    object? res = command.ExecuteScalar();
                    if (res != null) pageCount = Convert.ToInt64(res);
                }

                if (pageCount == 0) return 0.0;
                return (double)freeListCount / (double)pageCount * 100.0;
            }
            catch (Exception exp) {
                Logger.Warn($"Failed to get free space percent for quarter {Quarter}: {exp.Message}");
                return null;
            }
        }
    }
}
