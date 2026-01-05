// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace Ifak.Fast.Mediator.Timeseries.SQLite
{
    public class SQLiteChannel : Channel
    {
        private readonly DbConnection connection;
        private readonly SQLiteTimeseriesDB parentDb;
        private readonly string table;

        private readonly PreparedStatement stmtUpdate;
        private readonly PreparedStatement stmtInsert;
        private readonly PreparedStatement stmtUpsert;
        private readonly PreparedStatement stmtDelete;
        private readonly PreparedStatement stmtDeleteOne;
        private readonly PreparedStatement stmtFirst;
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

        public SQLiteChannel(DbConnection connection, ChannelInfo info, string tableName, SQLiteTimeseriesDB parentDb) {
            this.connection = connection;
            this.parentDb = parentDb;
            this.table = "\"" + tableName + "\"";

            stmtUpdate          = new PreparedStatement(connection, $"UPDATE {table} SET diffDB = @1, quality = @2, data = @3 WHERE time = @4", 4);
            stmtInsert          = new PreparedStatement(connection, $"INSERT INTO {table} VALUES (@1, @2, @3, @4)", 4);
            stmtUpsert          = new PreparedStatement(connection, $"INSERT OR REPLACE INTO {table} VALUES (@1, @2, @3, @4)", 4);
            stmtDelete          = new PreparedStatement(connection, $"DELETE FROM {table} WHERE time BETWEEN @1 AND @2", 2);
            stmtDeleteOne       = new PreparedStatement(connection, $"DELETE FROM {table} WHERE time = @1", 1);
            stmtFirst           = new PreparedStatement(connection, $"SELECT * FROM {table} ORDER BY time ASC LIMIT 1", 0);
            stmtLast            = new PreparedStatement(connection, $"SELECT * FROM {table} ORDER BY time DESC LIMIT 1", 0);
            stmtLatestTimeDb         = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 ORDER BY (time + 1000 * diffDB) DESC LIMIT 1", 2);

            stmtRawFirstN_AllQuality = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 ORDER BY time ASC LIMIT @3", 3);
            stmtRawFirstN_NonBad     = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 AND quality <> 0 ORDER BY time ASC LIMIT @3", 3);
            stmtRawFirstN_Good       = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 AND quality  = 1 ORDER BY time ASC LIMIT @3", 3);

            stmtRawLastN_AllQuality  = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 ORDER BY time DESC LIMIT @3", 3);
            stmtRawLastN_NonBad      = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 AND quality <> 0 ORDER BY time DESC LIMIT @3", 3);
            stmtRawLastN_Good        = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 AND quality  = 1 ORDER BY time DESC LIMIT @3", 3);

            stmtRawFirst        = new PreparedStatement(connection, $"SELECT * FROM {table} WHERE time BETWEEN @1 AND @2 ORDER BY time ASC", 2);
            stmtCount           = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table}", 0);
            stmtCountAllQuality = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table} WHERE time BETWEEN @1 AND @2", 2);
            stmtCountNonBad     = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table} WHERE time BETWEEN @1 AND @2 AND quality <> 0", 2);
            stmtCountGood       = new PreparedStatement(connection, $"SELECT COUNT(*) FROM {table} WHERE time BETWEEN @1 AND @2 AND quality = 1", 2);
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
            stmt[0] = startInclusive.JavaTicks;
            stmt[1] = endInclusive.JavaTicks;
            return (long)stmt.ExecuteScalar()!;
        }

        public override long DeleteAll() {
            using (var command = Factory.MakeCommand($"DELETE FROM {table}", connection)) {
                return command.ExecuteNonQuery();
            }
        }

        public override long DeleteData(Timestamp[] timestamps) {
            return stmtDeleteOne.RunTransaction(stmt => {
                long counter = 0;
                for (int i = 0; i < timestamps.Length; ++i) {
                    stmt[0] = timestamps[i].JavaTicks;
                    counter += stmt.ExecuteNonQuery();
                }
                return counter;
            });
        }

        public override long DeleteData(Timestamp startInclusive, Timestamp endInclusive) {
            return stmtDelete.RunTransaction(stmt => {
                stmt[0] = startInclusive.JavaTicks;
                stmt[1] = endInclusive.JavaTicks;
                return stmt.ExecuteNonQuery();
            });
        }

        public VTTQ? GetFirst() => GetFirst(null);

        protected VTTQ? GetFirst(DbTransaction? transaction) {

            using (var reader = stmtFirst.ExecuteReader(transaction)) {
                if (reader.Read()) {
                    return ReadVTTQ(reader);
                }
                else {
                    return null;
                }
            }
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
            stmtLatestTimeDb[0] = startInclusive.JavaTicks;
            stmtLatestTimeDb[1] = endInclusive.JavaTicks;
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

            stmtInsert.RunTransaction(stmt => {

                for (int i = 0; i < data.Length; ++i) {
                    VTQ x = data[i];
                    WriteVTQ(stmt, x, timeDB);
                    stmt.ExecuteNonQuery();
                }
                return true;
            });

            parentDb.CheckAndApplyRetention();
        }

        public override void Upsert(VTQ[] data) {

            if (data.Length == 0) return;

            Timestamp timeDB = Timestamp.Now;

            stmtUpsert.RunTransaction(stmt => {

                for (int i = 0; i < data.Length; ++i) {
                    VTQ x = data[i];
                    WriteVTQ(stmt, x, timeDB);
                    stmt.ExecuteNonQuery();
                }
                return true;
            });

            parentDb.CheckAndApplyRetention();
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

            Timestamp timeDB = Timestamp.Now;

            stmtUpdate.RunTransaction(stmt => {

                for (int i = 0; i < data.Length; ++i) {
                    VTQ x = data[i];
                    stmt[0] = (timeDB - x.T).TotalMilliseconds / 1000L;
                    stmt[1] = (int)x.Q;
                    stmt[2] = x.V.JSON;
                    stmt[3] = x.T.JavaTicks;
                    int updatedRows = stmt.ExecuteNonQuery();
                    if (updatedRows != 1) {
                        throw new Exception("Update of missing timestamp '" + x.T + "' would fail.");
                    }
                }
                return true;
            });
        }

        public override Func<PrepareContext, string?> PrepareAppend(VTQ data, bool allowOutOfOrder) {

            if (allowOutOfOrder) {

                return (PrepareContext ctx) => {

                    var context = (SQLiteContext)ctx;

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

                var context = (SQLiteContext)ctx;

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

        public override List<VTTQ> ReadData(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter) {

            long N = CountData(startInclusive, endInclusive, filter);

            //double millis = (endInclusive - startInclusive).TotalMilliseconds + 1;
            //double avgInterval_MS = millis / N;
            //double requestInterval_MS = 60 * 1000;

            //double N_2 = N;
            //if (requestInterval_MS > avgInterval_MS) {
            //    N_2 = N / (requestInterval_MS / avgInterval_MS);
            //}

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

            statement[0] = startInclusive.JavaTicks;
            statement[1] = endInclusive.JavaTicks;
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

            string innerQuery = $"SELECT CAST(data AS REAL) AS val, data, time FROM {table} WHERE time >= @1 AND time < @2 {qualiFilter}";
            const string isNumeric = "val != 0.0 OR TRIM(data) IN ('0', '0.0', '0.00', '.0', '0e0', '+0')"; // we need this extra check because CAST('non-numeric-string' AS REAL) yields 0.0 in SQLite!

            string sql = aggregation switch {
                Aggregation.Count   => $"SELECT COUNT(val) FROM ({innerQuery}) WHERE {isNumeric}",
                Aggregation.Sum     => $"SELECT SUM(val)   FROM ({innerQuery}) WHERE {isNumeric}",
                Aggregation.Average => $"SELECT AVG(val)   FROM ({innerQuery}) WHERE {isNumeric}",
                Aggregation.Min     => $"SELECT MIN(val)   FROM ({innerQuery}) WHERE {isNumeric}",
                Aggregation.Max     => $"SELECT MAX(val)   FROM ({innerQuery}) WHERE {isNumeric}",
                Aggregation.First   => $"SELECT data       FROM ({innerQuery}) WHERE {isNumeric} ORDER BY time ASC LIMIT 1",
                Aggregation.Last    => $"SELECT data       FROM ({innerQuery}) WHERE {isNumeric} ORDER BY time DESC LIMIT 1",
                _ => throw new ArgumentException($"Unknown aggregation type: {aggregation}")
            };

            return new PreparedStatement(connection, sql, 2);
        }

        private static VTQ ComputeAggregation(PreparedStatement stmt, Aggregation aggregation, Timestamp start, Timestamp end) {
            stmt[0] = start.JavaTicks;
            stmt[1] = end.JavaTicks;

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

        private static VTTQ ReadVTTQ(DbDataReader reader) {
            Timestamp t = Timestamp.FromJavaTicks((long)reader["time"]);
            Timestamp tDB = t + Duration.FromSeconds((long)reader["diffDB"]);
            Quality q = (Quality)(int)(long)reader["quality"];
            string data = (string)reader["data"];
            return new VTTQ(t, tDB, q, DataValue.FromJSON(data));
        }

        private static void WriteVTQ(PreparedStatement stmt, VTQ data, Timestamp timeDB) {
            stmt[0] = data.T.JavaTicks;
            stmt[1] = (timeDB - data.T).TotalMilliseconds / 1000L;
            stmt[2] = (int)data.Q;
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
        private DbParameter[]? parameter = null;
        private static readonly string[] indices = ["@1", "@2", "@3", "@4", "@5", "@6", "@7", "@8"];

        internal PreparedStatement(DbConnection connection, string sql, int countParameters) {
            this.connection = connection;
            this.sql = sql;
            this.countParameters = countParameters;
        }

        public DbCommand GetCommand() {
            if (command != null) {
                return command;
            }
            command = Factory.MakeCommand(sql, connection);
            parameter = new DbParameter[countParameters];
            for (int i = 0; i < countParameters; ++i) {
                var p = Factory.MakeParameter(name: indices[i]);
                parameter[i] = p;
                command.Parameters.Add(p);
            }
            return command;
        }

        internal object this[int i]
        {
            set
            {
                GetCommand();
                parameter![i].Value = value;
            }
        }

        internal int ExecuteNonQuery(DbTransaction? transaction = null) {
            var command = GetCommand();
            if (transaction != null) {
                command.Transaction = transaction;
            }
            int res = command.ExecuteNonQuery();
            if (transaction != null) {
                command.Transaction = null;
            }
            return res;
        }

        internal object? ExecuteScalar(DbTransaction? transaction = null) {
            var command = GetCommand();
            if (transaction != null) {
                command.Transaction = transaction;
            }
            object? res = command.ExecuteScalar();
            if (transaction != null) {
                command.Transaction = null;
            }
            return res;
        }

        internal DbDataReader ExecuteReader(DbTransaction? transaction = null) {
            var command = GetCommand();
            if (transaction != null) {
                command.Transaction = transaction;
            }
            DbDataReader res = command.ExecuteReader();
            if (transaction != null) {
                command.Transaction = null;
            }
            return res;
        }

        internal T RunTransaction<T>(Func<PreparedStatement, T> operation) {

            var cmd = GetCommand();

            using (var transaction = connection.BeginTransaction()) {
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
        }

        internal void Reset() {
            if (command != null) {
                command.Transaction = null;
                command.Dispose();
                command = null;
            }
            parameter = null;
        }
    }
}
