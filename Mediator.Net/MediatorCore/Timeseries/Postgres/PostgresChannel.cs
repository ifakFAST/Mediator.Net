// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using NpgsqlTypes;
using Npgsql;

namespace Ifak.Fast.Mediator.Timeseries.Postgres
{
    public class PostgresChannel : Channel
    {
        // private static NLog.Logger logger = NLog.LogManager.GetLogger("PostgresChannel");

        private readonly DbConnection connection;
        private readonly ChannelInfo info;
        private readonly string table;

        private readonly PreparedStatement stmtUpdate;
        private readonly PreparedStatement stmtInsert;
        private readonly PreparedStatement stmtUpsert;
        private readonly PreparedStatement stmtDelete;
        private readonly PreparedStatement stmtDeleteOne;
        private readonly PreparedStatement stmtLast;
        private readonly PreparedStatement stmtLatestTimeDb;

        private readonly PreparedStatement stmtRawFirstN_AllQuality;
        private readonly PreparedStatement stmtRawFirstN_NonBad;
        private readonly PreparedStatement stmtRawFirstN_Good;

        private readonly PreparedStatement stmtRawLastN_AllQuality;
        private readonly PreparedStatement stmtRawLastN_NonBad;
        private readonly PreparedStatement stmtRawLastN_Good;

        private readonly PreparedStatement stmtRawFirst;
        private readonly PreparedStatement stmtCount;
        private readonly PreparedStatement stmtCountAllQuality;
        private readonly PreparedStatement stmtCountNonBad;
        private readonly PreparedStatement stmtCountGood;

        public PostgresChannel(DbConnection connection, ChannelInfo info, string tableName) {
            this.connection = connection;
            this.info = info;
            this.table = "\"" + tableName + "\"";

            NpgsqlDbType time = NpgsqlDbType.Timestamp;
            NpgsqlDbType diffDB = NpgsqlDbType.Integer;
            NpgsqlDbType quality = NpgsqlDbType.Smallint;
            NpgsqlDbType data = NpgsqlDbType.Text;

            stmtUpdate          = new PreparedStatement(connection, $"UPDATE {table} SET diffDB = $1, quality = $2, data = $3 WHERE time = $4", diffDB, quality, data, time);
            stmtInsert          = new PreparedStatement(connection, $"INSERT INTO {table} VALUES ($1, $2, $3, $4)", time, diffDB, quality, data);
            stmtUpsert          = new PreparedStatement(connection, $"INSERT INTO {table} VALUES ($1, $2, $3, $4) ON CONFLICT (time) DO UPDATE SET diffDB = $2, quality = $3, data = $4", time, diffDB, quality, data);
            stmtDelete          = new PreparedStatement(connection, $"DELETE FROM {table} WHERE time BETWEEN $1 AND $2", time, time);
            stmtDeleteOne       = new PreparedStatement(connection, $"DELETE FROM {table} WHERE time = $1", time);
            stmtLast            = new PreparedStatement(connection, $"SELECT * FROM {table} ORDER BY time DESC LIMIT 1");
            stmtLatestTimeDb    = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 ORDER BY (time + 1000 * diffDB) DESC LIMIT 1", time, time);

            stmtRawFirstN_AllQuality = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 ORDER BY time ASC LIMIT $3", time, time, NpgsqlDbType.Integer);
            stmtRawFirstN_NonBad     = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 AND quality <> 0 ORDER BY time ASC LIMIT $3", time, time, NpgsqlDbType.Integer);
            stmtRawFirstN_Good       = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 AND quality  = 1 ORDER BY time ASC LIMIT $3", time, time, NpgsqlDbType.Integer);

            stmtRawLastN_AllQuality  = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 ORDER BY time DESC LIMIT $3", time, time, NpgsqlDbType.Integer);
            stmtRawLastN_NonBad      = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 AND quality <> 0 ORDER BY time DESC LIMIT $3", time, time, NpgsqlDbType.Integer);
            stmtRawLastN_Good        = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 AND quality  = 1 ORDER BY time DESC LIMIT $3", time, time, NpgsqlDbType.Integer);

            stmtRawFirst        = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN $1 AND $2 ORDER BY time ASC", time, time);
            stmtCount           = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table}");
            stmtCountAllQuality = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table} WHERE time BETWEEN $1 AND $2", time, time);
            stmtCountNonBad     = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table} WHERE time BETWEEN $1 AND $2 AND quality <> 0", time, time);
            stmtCountGood       = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table} WHERE time BETWEEN $1 AND $2 AND quality = 1", time, time);
        }

        public override long CountAll() {
            return (long)stmtCount.ExecuteScalar()!;
        }

        public override long CountData(Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter) {
            PreparedStatement stmt = stmtCountAllQuality;
            if (filter == QualityFilter.ExcludeBad) {
                stmt = stmtCountNonBad;
            }
            else if (filter == QualityFilter.ExcludeNonGood) {
                stmt = stmtCountGood;
            }
            stmt[0] = startInclusive.ToDateTimeUnspecified();
            stmt[1] = endInclusive.ToDateTimeUnspecified();
            return (long)stmt.ExecuteScalar()!;
        }

        public override long DeleteAll() {
            using (var command = Factory.MakeCommand($"DELETE FROM {table}", connection)) {
                return command.ExecuteNonQuery();
            }
        }

        public override void Truncate() {
            using var command = Factory.MakeCommand($"TRUNCATE TABLE {table}", connection);
            command.ExecuteNonQuery();
        }

        public override long DeleteData(Timestamp[] timestamps) {
            return stmtDeleteOne.RunTransaction(stmt => {
                long counter = 0;
                for (int i = 0; i < timestamps.Length; ++i) {
                    stmt[0] = timestamps[i].ToDateTimeUnspecified();
                    counter += stmt.ExecuteNonQuery();
                }
                return counter;
            });
        }

        public override long DeleteData(Timestamp startInclusive, Timestamp endInclusive) {
            return stmtDelete.RunTransaction(stmt => {
                stmt[0] = startInclusive.ToDateTimeUnspecified();
                stmt[1] = endInclusive.ToDateTimeUnspecified();
                return stmt.ExecuteNonQuery();
            });
        }

        public override VTTQ? GetLatest() => GetLatest(null);

        protected VTTQ? GetLatest(DbTransaction? transaction) {

            using (var reader = stmtLast.ExecuteReader(transaction)) {
                if (reader.Read()) {
                    return ReadVTTQ(reader);
                }
                else {
                    return null;
                }
            }
        }

        public override VTTQ? GetLatestTimestampDB(Timestamp startInclusive, Timestamp endInclusive) {
            stmtLatestTimeDb[0] = startInclusive.ToDateTimeUnspecified();
            stmtLatestTimeDb[1] = endInclusive.ToDateTimeUnspecified();
            using (var reader = stmtLatestTimeDb.ExecuteReader()) {
                if (reader.Read()) {
                    return ReadVTTQ(reader);
                }
                else {
                    return null;
                }
            }
        }

        public override void Insert(VTQ[] data) {

            if (data.Length == 0) return;

            Timestamp timeDB = Timestamp.Now;

            if (data.Length < 10) {

                stmtInsert.RunTransaction(stmt => {

                    for (int i = 0; i < data.Length; ++i) {
                        VTQ x = data[i];
                        WriteVTQ(stmt, x, timeDB);
                        stmt.ExecuteNonQuery();
                    }
                    return true;
                });

            }
            else {

                var conn = (NpgsqlConnection)connection;

                using var transaction = conn.BeginTransaction();

                // COPY data directly into table using binary protocol (very fast)
                using (var writer = conn.BeginBinaryImport($"COPY {table} (time, diffDB, quality, data) FROM STDIN (FORMAT BINARY)")) {
                    for (int i = 0; i < data.Length; ++i) {
                        VTQ x = data[i];
                        writer.StartRow();
                        writer.Write(x.T.ToDateTimeUnspecified(), NpgsqlDbType.Timestamp);
                        writer.Write((int)((timeDB - x.T).TotalMilliseconds / 1000L), NpgsqlDbType.Integer);
                        writer.Write((short)(int)x.Q, NpgsqlDbType.Smallint);
                        writer.Write(x.V.JSON, NpgsqlDbType.Text);
                    }
                    writer.Complete();
                }

                transaction.Commit();
            }
        }

        public override void Upsert(VTQ[] data) {

            if (data.Length == 0) return;

            Timestamp timeDB = Timestamp.Now;

            if (data.Length < 10) {

                stmtUpsert.RunTransaction(stmt => {

                    for (int i = 0; i < data.Length; ++i) {
                        VTQ x = data[i];
                        WriteVTQ(stmt, x, timeDB);
                        stmt.ExecuteNonQuery();
                    }
                    return true;
                });

            }
            else {

                var conn = (NpgsqlConnection)connection;
                const string tempTable = "tmp_upsert";

                using var transaction = conn.BeginTransaction();

                using (var cmd = new NpgsqlCommand($"CREATE TEMP TABLE {tempTable} (LIKE {table} INCLUDING DEFAULTS) ON COMMIT DROP", conn, transaction)) {
                    cmd.ExecuteNonQuery();
                }

                // 2. COPY data into temp table using binary protocol (very fast)
                using (var writer = conn.BeginBinaryImport($"COPY {tempTable} (time, diffDB, quality, data) FROM STDIN (FORMAT BINARY)")) {
                    for (int i = 0; i < data.Length; ++i) {
                        VTQ x = data[i];
                        writer.StartRow();
                        writer.Write(x.T.ToDateTimeUnspecified(), NpgsqlDbType.Timestamp);
                        writer.Write((int)((timeDB - x.T).TotalMilliseconds / 1000L), NpgsqlDbType.Integer);
                        writer.Write((short)(int)x.Q, NpgsqlDbType.Smallint);
                        writer.Write(x.V.JSON, NpgsqlDbType.Text);
                    }
                    writer.Complete();
                }

                // 3. Upsert from temp table to real table (single SQL statement)
                using (var cmd = new NpgsqlCommand($"INSERT INTO {table} SELECT * FROM {tempTable} ON CONFLICT (time) DO UPDATE SET diffDB = EXCLUDED.diffDB, quality = EXCLUDED.quality, data = EXCLUDED.data", conn, transaction)) {
                    cmd.ExecuteNonQuery();
                }

                transaction.Commit();
            }
        }

        public override void ReplaceAll(VTQ[] data) {

            Timestamp timeDB = Timestamp.Now;

            stmtInsert.RunTransaction(stmt => {

                using (var command = Factory.MakeCommand($"DELETE FROM {table}", connection)) {
                    command.Transaction = stmt.GetCommand().Transaction;
                    command.ExecuteNonQuery();
                }

                for (int i = 0; i < data.Length; ++i) {
                    VTQ x = data[i];
                    WriteVTQ(stmt, x, timeDB);
                    stmt.ExecuteNonQuery();
                }
                return true;
            });
        }

        public override void Update(VTQ[] data) {
            if (data.Length == 0) return;

            Timestamp timeDB = Timestamp.Now;

            if (data.Length < 10) {

                stmtUpdate.RunTransaction(stmt => {

                    for (int i = 0; i < data.Length; ++i) {
                        VTQ x = data[i];
                        stmt[0] = (timeDB - x.T).TotalMilliseconds / 1000L;
                        stmt[1] = (int)x.Q;
                        stmt[2] = x.V.JSON;
                        stmt[3] = x.T.ToDateTimeUnspecified();
                        int updatedRows = stmt.ExecuteNonQuery();
                        if (updatedRows != 1) {
                            throw new Exception("Update of missing timestamp '" + x.T + "' would fail.");
                        }
                    }
                    return true;
                });

            }
            else {

                var conn = (NpgsqlConnection)connection;
                const string tempTable = "tmp_update";

                using var transaction = conn.BeginTransaction();

                // 1. Create temp table
                using (var cmd = new NpgsqlCommand($"CREATE TEMP TABLE {tempTable} (LIKE {table} INCLUDING DEFAULTS) ON COMMIT DROP", conn, transaction)) {
                    cmd.ExecuteNonQuery();
                }

                // 2. COPY data into temp table using binary protocol
                using (var writer = conn.BeginBinaryImport($"COPY {tempTable} (time, diffDB, quality, data) FROM STDIN (FORMAT BINARY)")) {
                    for (int i = 0; i < data.Length; ++i) {
                        VTQ x = data[i];
                        writer.StartRow();
                        writer.Write(x.T.ToDateTimeUnspecified(), NpgsqlDbType.Timestamp);
                        writer.Write((int)((timeDB - x.T).TotalMilliseconds / 1000L), NpgsqlDbType.Integer);
                        writer.Write((short)(int)x.Q, NpgsqlDbType.Smallint);
                        writer.Write(x.V.JSON, NpgsqlDbType.Text);
                    }
                    writer.Complete();
                }

                // 3. Update main table from temp table
                int updatedRows;
                using (var cmd = new NpgsqlCommand($"UPDATE {table} t SET diffDB = s.diffDB, quality = s.quality, data = s.data FROM {tempTable} s WHERE t.time = s.time", conn, transaction)) {
                    updatedRows = cmd.ExecuteNonQuery();
                }

                // 4. Validate all rows were updated
                if (updatedRows != data.Length) {
                    transaction.Rollback();
                    throw new Exception($"Update failed: expected {data.Length} rows but only {updatedRows} timestamps exist in the database.");
                }

                transaction.Commit();
            }
        }

        public override Func<PrepareContext, string?> PrepareAppend(VTQ data, bool allowOutOfOrder) {

            if (allowOutOfOrder) {

                return (PrepareContext ctx) => {

                    var context = (PostgresContext)ctx;

                    PreparedStatement stmt = stmtUpsert;
                    try {
                        WriteVTQ(stmt, data, context.TimeDB);
                        stmt.ExecuteNonQuery(context.Transaction);
                        return null;
                    }
                    catch (Exception exp) {
                        stmt.Reset();
                        return table + ": " + exp.Message;
                    }
                };
            }

            return (PrepareContext ctx) => {

                var context = (PostgresContext)ctx;

                VTTQ? lastItem = GetLatest(context.Transaction);

                if (lastItem.HasValue && data.T <= lastItem.Value.T) {
                    return table + ": Timestamp is smaller or equal than last dataset timestamp in channel DB!\n\tLastItem in Database: " + lastItem.Value.ToString() + "\n\t  The Item to Append: " + data.ToString();
                }

                PreparedStatement stmt = stmtInsert;
                try {
                    WriteVTQ(stmt, data, context.TimeDB);
                    stmt.ExecuteNonQuery(context.Transaction);
                    return null;
                }
                catch (Exception exp) {
                    stmt.Reset();
                    return table + ": " + exp.Message;
                }
            };
        }

        public override List<VTQ> ReadAggregatedIntervals(Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter) {

            if (intervalBounds == null || intervalBounds.Length < 2) {
                return [];
            }

            int numIntervals = intervalBounds.Length - 1;
            var result = new List<VTQ>(numIntervals);

            // Create PreparedStatement ONCE for all intervals
            PreparedStatement stmt = CreateAggregationStatement(aggregation, filter);
            try {
                // Reuse the same statement for all intervals
                for (int i = 0; i < numIntervals; i++) {
                    Timestamp intervalStart = intervalBounds[i];
                    Timestamp intervalEnd = intervalBounds[i + 1];

                    VTQ vtq = ComputeAggregation(stmt, aggregation, intervalStart, intervalEnd);
                    result.Add(vtq);
                }
            }
            finally {
                stmt.Reset();
            }
            return result;
        }

        private PreparedStatement CreateAggregationStatement(Aggregation aggregation, QualityFilter filter) {

            string qualiFilter = filter switch {
                QualityFilter.ExcludeBad => "AND quality <> 0",
                QualityFilter.ExcludeNonGood => "AND quality = 1",
                _ => ""
            };

            // PostgreSQL throws error on invalid cast, so filter numeric values BEFORE casting using regex
            const string isNumeric = "(data ~ '^[-]?\\d+(\\.\\d+)?$' OR data ~ '^\\s*[+-]?(\\d+(\\.\\d+)?|\\.\\d+)([eE][+-]?\\d+)?\\s*$')";

            string sql;
            if (aggregation == Aggregation.First || aggregation == Aggregation.Last) {
                // For First/Last, no cast needed - just return the original data string
                string whereClause = $"FROM {table} WHERE time >= $1 AND time < $2 {qualiFilter} AND {isNumeric}";
                sql = aggregation == Aggregation.First
                    ? $"SELECT data {whereClause} ORDER BY time ASC LIMIT 1"
                    : $"SELECT data {whereClause} ORDER BY time DESC LIMIT 1";
            }
            else {
                // For numeric aggregations, apply aggregation directly (no subquery needed)
                string whereClause = $"FROM {table} WHERE time >= $1 AND time < $2 {qualiFilter} AND {isNumeric}";
                sql = aggregation switch {
                    Aggregation.Count   => $"SELECT COUNT(*)                    {whereClause}",
                    Aggregation.Sum     => $"SELECT SUM(data::DOUBLE PRECISION) {whereClause}",
                    Aggregation.Average => $"SELECT AVG(data::DOUBLE PRECISION) {whereClause}",
                    Aggregation.Min     => $"SELECT MIN(data::DOUBLE PRECISION) {whereClause}",
                    Aggregation.Max     => $"SELECT MAX(data::DOUBLE PRECISION) {whereClause}",
                    _ => throw new ArgumentException($"Unknown aggregation type: {aggregation}")
                };
            }

            return new PreparedStatement(connection, sql, NpgsqlDbType.Timestamp, NpgsqlDbType.Timestamp);
        }

        private static VTQ ComputeAggregation(PreparedStatement stmt, Aggregation aggregation, Timestamp start, Timestamp end) {
            stmt[0] = start.ToDateTimeUnspecified();
            stmt[1] = end.ToDateTimeUnspecified();

            if (aggregation == Aggregation.Count) {
                long count = (long)stmt.ExecuteScalar()!;
                return new VTQ(start, Quality.Good, DataValue.FromLong(count));
            }
            else if (aggregation == Aggregation.First || aggregation == Aggregation.Last) {
                using (var reader = stmt.ExecuteReader()) {
                    if (reader.Read()) {
                        string data = (string)reader["data"];
                        return new VTQ(start, Quality.Good, DataValue.FromJSON(data));
                    }
                }
                return new VTQ(start, Quality.Good, DataValue.Empty);
            }
            else { // Sum, Average, Min, Max
                object? result = stmt.ExecuteScalar();
                if (result == null || result is DBNull) {
                    return new VTQ(start, Quality.Good, DataValue.Empty);
                }
                return new VTQ(start, Quality.Good, DataValue.FromDouble(Convert.ToDouble(result)));
            }
        }

        public override List<VTTQ> ReadData(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {

            long N = CountData(startInclusive, endInclusive, filter);

            PreparedStatement statement;

            switch (bounding) {
                case BoundingMethod.TakeFirstN:
                    switch (filter) {
                        case QualityFilter.ExcludeBad:
                            statement = stmtRawFirstN_NonBad;
                            break;
                        case QualityFilter.ExcludeNonGood:
                            statement = stmtRawFirstN_Good;
                            break;
                        default:
                            statement = stmtRawFirstN_AllQuality;
                            break;
                    }
                    break;

                case BoundingMethod.TakeLastN:
                    switch (filter) {
                        case QualityFilter.ExcludeBad:
                            statement = stmtRawLastN_NonBad;
                            break;
                        case QualityFilter.ExcludeNonGood:
                            statement = stmtRawLastN_Good;
                            break;
                        default:
                            statement = stmtRawLastN_AllQuality;
                            break;
                    }
                    break;

                default:
                    throw new Exception($"Unknown BoundingMethod: {bounding}");
            }

            statement[0] = startInclusive.ToDateTimeUnspecified();
            statement[1] = endInclusive.ToDateTimeUnspecified();
            statement[2] = maxValues;

            int initSize = N < maxValues ? (int)N : maxValues;
            var res = new List<VTTQ>(initSize);
            using (var reader = statement.ExecuteReader()) {
                while (reader.Read()) {
                    VTTQ vttq = ReadVTTQ(reader);
                    res.Add(vttq);
                }
            }

            if (bounding == BoundingMethod.TakeLastN) {
                res.Reverse();
            }

            return res;
        }

        private static VTTQ ReadVTTQ(DbDataReader reader) {
            Timestamp t = Timestamp.FromDateTime(DateTime.SpecifyKind((DateTime)reader["time"], DateTimeKind.Utc));
            Timestamp tDB = t + Duration.FromSeconds((int)reader["diffDB"]);
            Quality q = (Quality)(short)reader["quality"];
            string data = (string)reader["data"];
            return new VTTQ(t, tDB, q, DataValue.FromJSON(data));
        }

        private static void WriteVTQ(PreparedStatement stmt, VTQ data, Timestamp timeDB) {
            stmt[0] = data.T.ToDateTimeUnspecified();
            stmt[1] = (int)((timeDB - data.T).TotalMilliseconds / 1000L);
            stmt[2] = (short)(int)data.Q;
            stmt[3] = data.V.JSON;
        }
        // TODO Close or Dispose ?
    }

    class PreparedStatement
    {
        private readonly DbConnection connection;
        private readonly string sql;
        private readonly int countParameters;

        private DbCommand? command = null;
        private readonly NpgsqlDbType[] types;

        internal PreparedStatement(DbConnection connection, string sql, params NpgsqlDbType[] types) {
            this.connection = connection;
            this.sql = sql;
            this.countParameters = types.Length;
            this.types = types;
        }

        public DbCommand GetCommand() {
            if (command != null) {
                return command;
            }
            var cmd = (Npgsql.NpgsqlCommand)Factory.MakeCommand(sql, connection);
            for (int i = 0; i < countParameters; ++i) {
                var pp = new NpgsqlParameter(parameterName: null, parameterType: types[i]);
                cmd.Parameters.Add(pp);
            }
            cmd.Prepare();

            command = cmd;
            return command;
        }

        internal object this[int i]
        {
            set
            {
                var cmd = GetCommand();
                cmd.Parameters[i].Value = value;
            }
        }

        internal int ExecuteNonQuery(DbTransaction? transaction = null) {
            var command = GetCommand();
            if (transaction != null) {
                command.Transaction = transaction;
            }
            return command.ExecuteNonQuery();
        }

        internal object? ExecuteScalar(DbTransaction? transaction = null) {
            var command = GetCommand();
            if (transaction != null) {
                command.Transaction = transaction;
            }
            return command.ExecuteScalar();
        }

        internal DbDataReader ExecuteReader(DbTransaction? transaction = null) {
            var command = GetCommand();
            if (transaction != null) {
                command.Transaction = transaction;
            }
            return command.ExecuteReader();
        }

        internal T RunTransaction<T>(Func<PreparedStatement, T> operation) {

            var cmd = GetCommand();

            using var transaction = connection.BeginTransaction();
            try {
                cmd.Transaction = transaction;
                T res = operation(this);
                transaction.Commit();
                cmd.Transaction = null;
                return res;
            }
            catch (Exception) {

                try {
                    transaction.Rollback();
                }
                catch (Exception) { }

                Reset();
                //logger.Error(ex, "Creating channels failed: " + ex.Message);
                throw;
            }
        }

        internal void Reset() {
            if (command != null) {
                command.Transaction = null;
                command.Dispose();
                command = null;
            }
        }
    }
}
