// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Compression;
using Ifak.Fast.Mediator.BinSeri;
using Ifak.Fast.Mediator.Timeseries.SQLite;
using Ifak.Fast.Mediator.Util;
using NLog;

namespace Ifak.Fast.Mediator.Timeseries;

/// <summary>
/// SQLite-based cache for aggregated history values by UTC day.
/// Improves performance of HistorianReadAggregatedIntervals by caching
/// pre-computed aggregations per variable, per day.
/// </summary>
/// <remarks>
/// This cache is thread-safe and can be shared between the main worker and read workers.
/// It uses the mediator-level types (Ifak.Fast.Mediator.Aggregation and 
/// Ifak.Fast.Mediator.QualityFilter) rather than the Timeseries types for compatibility
/// with HistoryDBWorker which operates on mediator types.
/// </remarks>
/// <remarks>
/// Creates a new HistoryAggregationCache with a SQLite database at the specified path.
/// The database file is not created until the first call to TryGet or Set.
/// </remarks>
/// <param name="cacheDbPath">Full path to the SQLite cache database file</param>
public sealed class HistoryAggregationCache : IDisposable
{
    private static readonly Logger logger = LogManager.GetLogger("HistoryAggregationCache");

    /// <summary>
    /// Number of compressed VTTQ values to cache per day (~5 min resolution).
    /// </summary>
    public const int CompressedValuesPerDay = 288;

    private readonly string cacheDbPath;
    private readonly Dictionary<VariableRef, long> variableIdCache = [];
    private readonly object dbLock = new();

    private DbConnection? connection;
    private PreparedStatement? stmtGetVariableId;
    private PreparedStatement? stmtInsertVariable;
    private PreparedStatement? stmtGetCacheEntry;
    private PreparedStatement? stmtUpsertCacheEntry;
    private PreparedStatement? stmtDeleteCacheEntry;
    private PreparedStatement? stmtGetCompressedEntry;
    private PreparedStatement? stmtUpsertCompressedEntry;
    private PreparedStatement? stmtDeleteCompressedEntry;
    private bool initialized = false;

    public HistoryAggregationCache(string cacheDbPath) {
        this.cacheDbPath = cacheDbPath;
        bool dbExists = File.Exists(cacheDbPath);
        if (dbExists) {
            InitializeDatabase();
        } 
    }

    private void InitializeDatabase() {

        if (initialized) return;
        initialized = true;

        string connectionString = $"Filename=\"{cacheDbPath}\";Pooling=False;";

        try {

            connection = Factory.MakeConnection(connectionString);
            connection.Open();

            using (var cmd = Factory.MakeCommand("PRAGMA page_size=16384; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;", connection)) {
                cmd.ExecuteNonQuery();
            }

            CreateTablesIfNotExist(connection);

            stmtGetVariableId    = new PreparedStatement(connection, "SELECT id FROM variables WHERE module_id = @1 AND object_id = @2 AND variable = @3", 3);
            stmtInsertVariable   = new PreparedStatement(connection, "INSERT INTO variables (module_id, object_id, variable) VALUES (@1, @2, @3)", 3);

            stmtGetCacheEntry    = new PreparedStatement(connection, "SELECT value, count FROM cache_agg WHERE var_id = @1 AND day_number = @2 AND aggregation = @3 AND quality_filter = @4", 4);
            stmtDeleteCacheEntry = new PreparedStatement(connection, "DELETE FROM cache_agg WHERE var_id = @1 AND day_number >= @2 AND day_number <= @3", 3);
            stmtUpsertCacheEntry = new PreparedStatement(connection,
                                                                    @"INSERT INTO cache_agg (var_id, day_number, aggregation, quality_filter, value, count) 
                                                                      VALUES (@1, @2, @3, @4, @5, @6)
                                                                      ON CONFLICT(var_id, day_number, aggregation, quality_filter) 
                                                                      DO UPDATE SET value = @5, count = @6", 6);

            stmtGetCompressedEntry    = new PreparedStatement(connection,  "SELECT data FROM cache_compressed WHERE id = @1", 1);
            stmtDeleteCompressedEntry = new PreparedStatement(connection,  "DELETE FROM cache_compressed WHERE id >= @1 AND id < @2", 2);
            stmtUpsertCompressedEntry = new PreparedStatement(connection, @"INSERT INTO cache_compressed (id, data) 
                                                                            VALUES (@1, @2)
                                                                            ON CONFLICT(id) 
                                                                            DO UPDATE SET data = @2", 2);

            logger.Debug("HistoryAggregationCache opened: {0}", cacheDbPath);
        }
        catch (Exception ex) {
            logger.Warn(ex, $"Failed to open aggregation cache database: {cacheDbPath}");
            Close();
        }
    }

    private static void CreateTablesIfNotExist(DbConnection connection) {
        using var cmd = Factory.MakeCommand(@"
            CREATE TABLE IF NOT EXISTS variables (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                module_id TEXT NOT NULL,
                object_id TEXT NOT NULL,
                variable TEXT NOT NULL,
                UNIQUE(module_id, object_id, variable)
            );

            CREATE TABLE IF NOT EXISTS cache_agg (
                var_id INTEGER NOT NULL,
                day_number INTEGER NOT NULL,
                aggregation INTEGER NOT NULL,
                quality_filter INTEGER NOT NULL,
                value REAL,
                count INTEGER,
                PRIMARY KEY(var_id, day_number, aggregation, quality_filter),
                FOREIGN KEY(var_id) REFERENCES variables(id)
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS cache_compressed (
                id INTEGER PRIMARY KEY,
                data BLOB
            );
        ", connection);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Attempts to retrieve a cached aggregation value for a specific variable, aggregation type,
    /// quality filter, and UTC day. This method is thread-safe.
    /// </summary>
    /// <param name="variable">The variable reference</param>
    /// <param name="aggregation">The aggregation type (Average, Min, Max, Count, Sum)</param>
    /// <param name="filter">The quality filter applied to raw data</param>
    /// <param name="dayStartUtc">The UTC day start timestamp</param>
    /// <param name="value">The cached aggregated value</param>
    /// <param name="count">The count (for Average aggregation, null otherwise)</param>
    /// <returns>True if a cached value was found, false otherwise</returns>
    public bool TryGet(VariableRef variable, Ifak.Fast.Mediator.Aggregation aggregation, Ifak.Fast.Mediator.QualityFilter filter,
                       Timestamp dayStartUtc, out double? value, out long? count) {

        value = null;
        count = null;

        lock (dbLock) {

            InitializeDatabase();
            
            if (connection is null || stmtGetCacheEntry is null) {
                return false; // Database not initialized successfully
            }

            if (!variableIdCache.TryGetValue(variable, out long varId)) {
                varId = GetVariableId(variable);
                if (varId <= 0) {
                    return false;
                }
                variableIdCache.Add(variable, varId);
            }

            try {

                stmtGetCacheEntry[0] = varId;
                stmtGetCacheEntry[1] = GetDayNumber(dayStartUtc);
                stmtGetCacheEntry[2] = (int)aggregation;
                stmtGetCacheEntry[3] = (int)filter;

                using var reader = stmtGetCacheEntry.ExecuteReader();
                if (reader.Read()) {
                    value = reader.IsDBNull(0) ? null : reader.GetDouble(0);
                    count = reader.IsDBNull(1) ? null : reader.GetInt64(1);
                    return true;
                }
                return false;
            }
            catch (Exception ex) {
                logger.Warn(ex, "Failed to get cache entry for {0}", variable);
                return false;
            }
        }
    }

    /// <summary>
    /// Stores an aggregation value in the cache. This method is thread-safe.
    /// </summary>
    /// <param name="variable">The variable reference</param>
    /// <param name="aggregation">The aggregation type</param>
    /// <param name="filter">The quality filter</param>
    /// <param name="dayStartUtc">The UTC day start timestamp</param>
    /// <param name="value">The aggregated value to cache</param>
    /// <param name="count">The count (required for Average, null for others)</param>
    public void Set(VariableRef variable, Ifak.Fast.Mediator.Aggregation aggregation, Ifak.Fast.Mediator.QualityFilter filter,
                    Timestamp dayStartUtc, double? value, long? count) {

        lock (dbLock) {

            InitializeDatabase();

            if (connection is null || stmtUpsertCacheEntry is null) return; // Database not initialized successfully

            try {
                if (!variableIdCache.TryGetValue(variable, out long varId)) {
                    varId = GetOrCreateVariableId(variable);
                    variableIdCache.Add(variable, varId);
                }

                stmtUpsertCacheEntry[0] = varId;
                stmtUpsertCacheEntry[1] = GetDayNumber(dayStartUtc);
                stmtUpsertCacheEntry[2] = (int)aggregation;
                stmtUpsertCacheEntry[3] = (int)filter;
                stmtUpsertCacheEntry[4] = value.HasValue ? value.Value : DBNull.Value;
                stmtUpsertCacheEntry[5] = count.HasValue ? count.Value : DBNull.Value;

                stmtUpsertCacheEntry.ExecuteNonQuery();
            }
            catch (Exception ex) {
                logger.Warn(ex, "Failed to set cache entry for {0}", variable);
            }
        }
    }

    /// <summary>
    /// Attempts to retrieve cached compressed VTTQ values for a specific variable,
    /// quality filter, and UTC day. This method is thread-safe.
    /// </summary>
    /// <param name="variable">The variable reference</param>
    /// <param name="filter">The quality filter applied to raw data</param>
    /// <param name="dayStartUtc">The UTC day start timestamp</param>
    /// <param name="values">The cached compressed VTTQ values</param>
    /// <returns>True if cached values were found, false otherwise</returns>
    public bool TryGetCompressed(VariableRef variable, Ifak.Fast.Mediator.QualityFilter filter,
                                 Timestamp dayStartUtc, out List<VTTQ>? values) {

        values = null;

        lock (dbLock) {

            InitializeDatabase();

            if (connection is null || stmtGetCompressedEntry is null) {
                return false;
            }

            if (!variableIdCache.TryGetValue(variable, out long varId)) {
                varId = GetVariableId(variable);
                if (varId <= 0) {
                    return false;
                }
                variableIdCache.Add(variable, varId);
            }

            try {
                long key = ComputeCompressedKey(varId, GetDayNumber(dayStartUtc), filter);
                stmtGetCompressedEntry[0] = key;

                using var reader = stmtGetCompressedEntry.ExecuteReader();
                if (reader.Read()) {
                    if (reader.IsDBNull(0)) {
                        values = [];
                        return true;
                    }
                    using Stream stream = reader.GetStream(0);
                    using var mem = MemoryManager.GetMemoryStream("HistoryAggregationCache1");
                    using var decompressStream = new GZipStream(stream, CompressionMode.Decompress);
                    decompressStream.CopyTo(mem);
                    mem.Position = 0;
                    values = VTQ_Serializer.DeserializeAsVTTQ(mem);
                    return true;
                }
                return false;
            }
            catch (Exception ex) {
                logger.Warn(ex, "Failed to get compressed cache entry for {0}", variable);
                return false;
            }
        }
    }

    /// <summary>
    /// Stores compressed VTTQ values in the cache. This method is thread-safe.
    /// </summary>
    /// <param name="variable">The variable reference</param>
    /// <param name="filter">The quality filter</param>
    /// <param name="dayStartUtc">The UTC day start timestamp</param>
    /// <param name="values">The compressed VTTQ values to cache</param>
    public void SetCompressed(VariableRef variable, Ifak.Fast.Mediator.QualityFilter filter,
                              Timestamp dayStartUtc, List<VTTQ> values) {

        lock (dbLock) {

            InitializeDatabase();

            if (connection is null || stmtUpsertCompressedEntry is null) return;

            try {
                if (!variableIdCache.TryGetValue(variable, out long varId)) {
                    varId = GetOrCreateVariableId(variable);
                    variableIdCache.Add(variable, varId);
                }

                long key = ComputeCompressedKey(varId, GetDayNumber(dayStartUtc), filter);
                stmtUpsertCompressedEntry[0] = key;

                if (values.Count == 0) {
                    stmtUpsertCompressedEntry[1] = DBNull.Value;
                    stmtUpsertCompressedEntry.ExecuteNonQuery();
                }
                else {

                    using var stream = MemoryManager.GetMemoryStream("HistoryAggregationCache2");
                    VTQ_Serializer.SerializeVTTQ(stream, values, Common.CurrentBinaryVersion);
                    stream.Position = 0;
                    using var mem = MemoryManager.GetMemoryStream("HistoryAggregationCache3");
                    using (var compressStream = new GZipStream(mem, CompressionLevel.Optimal, leaveOpen: true)) {
                        stream.CopyTo(compressStream);
                    }
                    mem.Position = 0;
                    byte[] dataCompress = mem.ToArray();

                    stmtUpsertCompressedEntry[1] = dataCompress;
                    stmtUpsertCompressedEntry.ExecuteNonQuery();
                }
            }
            catch (Exception ex) {
                logger.Warn(ex, "Failed to set compressed cache entry for {0}", variable);
            }
        }
    }

    /// <summary>
    /// Invalidates all cached entries for a variable within the specified time range.
    /// This should be called whenever history data is modified. This method is thread-safe.
    /// </summary>
    /// <param name="variable">The variable reference</param>
    /// <param name="startUtc">Start of the modified time range (inclusive)</param>
    /// <param name="endUtc">End of the modified time range (inclusive)</param>
    public void InvalidateDays(VariableRef variable, Timestamp startUtc, Timestamp endUtc) {

        lock (dbLock) {

            if (connection is null || stmtDeleteCacheEntry is null || stmtDeleteCompressedEntry is null) return;

            if (!variableIdCache.TryGetValue(variable, out long varId)) {
                varId = GetVariableId(variable);
                if (varId <= 0) {
                    return; // Variable not in cache, nothing to invalidate
                }
                variableIdCache.Add(variable, varId);
            }

            try {

                Timestamp dayStart = GetUtcDayStart(startUtc);
                Timestamp dayEnd = GetUtcDayStart(endUtc);

                stmtDeleteCacheEntry[0] = varId;
                stmtDeleteCacheEntry[1] = GetDayNumber(dayStart);
                stmtDeleteCacheEntry[2] = GetDayNumber(dayEnd);

                int deletedAgg = stmtDeleteCacheEntry.ExecuteNonQuery();

                long minKeyInclusive = ComputeCompressedKey(varId, GetDayNumber(dayStart));
                long maxKeyExclusive = ComputeCompressedKey(varId, GetDayNumber(dayEnd) + 1);

                stmtDeleteCompressedEntry[0] = minKeyInclusive;
                stmtDeleteCompressedEntry[1] = maxKeyExclusive;

                int deletedCompressed = stmtDeleteCompressedEntry.ExecuteNonQuery();

                int deleted = deletedAgg + deletedCompressed;
                if (deleted > 0) {
                    logger.Debug("Invalidated {0} cache entries for {1} from {2} to {3}", deleted, variable, dayStart, dayEnd);
                }
            }
            catch (Exception ex) {
                logger.Warn(ex, "Failed to invalidate cache for {0}", variable);
            }
        }
    }

    /// <summary>
    /// Invalidates all cached entries for a variable (used for truncate operations). 
    /// This method is thread-safe.
    /// </summary>
    /// <param name="variable">The variable reference</param>
    public void InvalidateAll(VariableRef variable) {

        lock (dbLock) {

            if (connection is null) return; // Database not initialized successfully

            if (!variableIdCache.TryGetValue(variable, out long varId)) {
                varId = GetVariableId(variable);
                if (varId <= 0) {
                    return;
                }
                variableIdCache.Add(variable, varId);
            }

            try {

                using (var cmd = Factory.MakeCommand("DELETE FROM cache_agg WHERE var_id = @1", connection)) {
                    cmd.Parameters.Add(Factory.MakeParameter("@1", varId));
                    cmd.ExecuteNonQuery();
                }

                // Delete all compressed entries for this variable across all days and quality filters
                long minKeyInclusive = ComputeCompressedKey(varId);
                long maxKeyExclusive = ComputeCompressedKey(varId + 1);

                using (var cmd = Factory.MakeCommand("DELETE FROM cache_compressed WHERE id >= @1 AND id < @2", connection)) {
                    cmd.Parameters.Add(Factory.MakeParameter("@1", minKeyInclusive));
                    cmd.Parameters.Add(Factory.MakeParameter("@2", maxKeyExclusive));
                    cmd.ExecuteNonQuery();
                }

                logger.Debug("Invalidated all cache entries for {0}", variable);
            }
            catch (Exception ex) {
                logger.Warn(ex, "Failed to invalidate all cache for {0}", variable);
            }
        }
    }

    private long GetVariableId(VariableRef variable) {

        if (connection is null || stmtGetVariableId is null) return -1;

        try {
            stmtGetVariableId[0] = variable.Object.ModuleID;
            stmtGetVariableId[1] = variable.Object.LocalObjectID;
            stmtGetVariableId[2] = variable.Name;

            using var reader = stmtGetVariableId.ExecuteReader();
            return reader.Read() ? reader.GetInt64(0) : -1;
        }
        catch (Exception ex) {
            logger.Warn(ex, "Failed to get variable ID for {0}", variable);
            return -1;
        }
    }

    private long GetOrCreateVariableId(VariableRef variable) {

        long id = GetVariableId(variable);
        if (id > 0) {
            return id;
        }

        if (connection is null || stmtInsertVariable is null) return -1;

        try {

            stmtInsertVariable[0] = variable.Object.ModuleID;
            stmtInsertVariable[1] = variable.Object.LocalObjectID;
            stmtInsertVariable[2] = variable.Name;
            stmtInsertVariable.ExecuteNonQuery();

            // Get the inserted ID
            using var cmd = Factory.MakeCommand("SELECT last_insert_rowid()", connection);
            return (long)cmd.ExecuteScalar()!;

        }
        catch (Exception ex) {
            logger.Warn(ex, "Failed to create variable ID for {0}", variable);
            return -1;
        }
    }

    /// <summary>
    /// Gets the UTC day start timestamp for a given timestamp.
    /// </summary>
    private static Timestamp GetUtcDayStart(Timestamp t) {
        var dt = t.ToDateTime();
        return Timestamp.FromComponents(dt.Year, dt.Month, dt.Day);
    }

    /// <summary>
    /// Milliseconds per UTC day (24 * 60 * 60 * 1000).
    /// </summary>
    private const long MillisecondsPerDay = 86400000L;

    /// <summary>
    /// Converts a UTC day start timestamp to a day number (0 = Jan 1, 1970, 1 = Jan 2, 1970, etc.).
    /// </summary>
    private static long GetDayNumber(Timestamp dayStartUtc) {
        return dayStartUtc.JavaTicks / MillisecondsPerDay;
    }

    private static long ComputeCompressedKey(long varId, long dayNumber, Mediator.QualityFilter filter) {
        uint q = filter switch {
            Mediator.QualityFilter.ExcludeNone    => 0,
            Mediator.QualityFilter.ExcludeBad     => 1,
            Mediator.QualityFilter.ExcludeNonGood => 2,
            _ => 0
        };
        return ComputeCompressedKey(varId, dayNumber, q);
    }

    /// <summary>
    /// Computes a single INTEGER PRIMARY KEY by bit-packing var_id, day_number, and quality_filter.
    /// Bit layout (64-bit signed integer):
    /// - Bits 0-7    (8 bits = 1 byte):  quality_filter
    /// - Bits 8-31  (24 bits = 3 bytes): day_number (supports ~45964 years from epoch)
    /// - Bits 32+   (32 bits = 4 bytes): var_id
    /// </summary>
    private static long ComputeCompressedKey(long varId, long dayNumber = 0, uint qualityFilter = 0) {
        return (varId << 32) | ((dayNumber & 0xFFFFFF) << 8) | (qualityFilter & 0xFF);
    }

    /// <summary>
    /// Gets all complete UTC days that are fully contained within the given range [start, end).
    /// </summary>
    /// <param name="start">Start of range (inclusive)</param>
    /// <param name="end">End of range (exclusive)</param>
    /// <returns>List of day start timestamps for complete days</returns>
    public static List<Timestamp> GetCompleteDaysInRange(Timestamp start, Timestamp end) {
        var result = new List<Timestamp>();

        // Get the first complete day start (day after start, unless start is at day boundary)
        Timestamp startDayStart = GetUtcDayStart(start);
        Timestamp firstCompleteDay = startDayStart == start ? start : startDayStart.AddDays(1);

        // Get the last day start that ends before 'end'
        Timestamp endDayStart = GetUtcDayStart(end);

        // Iterate through complete days
        Timestamp currentDay = firstCompleteDay;
        while (currentDay < endDayStart) {
            result.Add(currentDay);
            currentDay = currentDay.AddDays(1);
        }

        return result;
    }

    /// <summary>
    /// Checks if an aggregation type is cacheable.
    /// First and Last are excluded because combining them across days requires timestamp tracking.
    /// </summary>
    public static bool IsCacheableAggregation(Ifak.Fast.Mediator.Aggregation aggregation) {
        return aggregation switch {
            Ifak.Fast.Mediator.Aggregation.Average => true,
            Ifak.Fast.Mediator.Aggregation.Min => true,
            Ifak.Fast.Mediator.Aggregation.Max => true,
            Ifak.Fast.Mediator.Aggregation.Count => true,
            Ifak.Fast.Mediator.Aggregation.Sum => true,
            Ifak.Fast.Mediator.Aggregation.First => false,
            Ifak.Fast.Mediator.Aggregation.Last => false,
            _ => false
        };
    }

    public void Dispose() {
        lock (dbLock) {
            Close();
        }
        logger.Debug("HistoryAggregationCache disposed");
    }

    private void Close() {
        stmtGetVariableId?.Reset();
        stmtGetVariableId = null;

        stmtInsertVariable?.Reset();
        stmtInsertVariable = null;

        stmtGetCacheEntry?.Reset();
        stmtGetCacheEntry = null;

        stmtUpsertCacheEntry?.Reset();
        stmtUpsertCacheEntry = null;

        stmtDeleteCacheEntry?.Reset();
        stmtDeleteCacheEntry = null;

        stmtGetCompressedEntry?.Reset();
        stmtGetCompressedEntry = null;

        stmtUpsertCompressedEntry?.Reset();
        stmtUpsertCompressedEntry = null;

        stmtDeleteCompressedEntry?.Reset();
        stmtDeleteCompressedEntry = null;

        try { connection?.Close(); } catch { }
        try { connection?.Dispose(); } catch { }
        connection = null;
    }
}
