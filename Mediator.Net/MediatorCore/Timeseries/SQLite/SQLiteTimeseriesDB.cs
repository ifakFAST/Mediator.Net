// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using NLog;

namespace Ifak.Fast.Mediator.Timeseries.SQLite
{
    public class SQLiteTimeseriesDB : TimeSeriesDB
    {
        private static Logger logger = LogManager.GetLogger("SQLiteTimeseriesDB");

        protected DbConnection? connection = null;
        private string tableSeparator = "$";
        private PreparedStatement? stmtSelectChannel;
        private Duration? retentionTime = null;
        private Duration retentionCheckInterval = Duration.FromHours(1);  // default 1 hour
        private Timestamp lastRetentionCheck = Timestamp.Empty;

        public override bool IsOpen => connection != null;

        public override void ClearDatabase(OpenParams parameter) {

            cacheChannelEntry.Clear();

            string name = parameter.Name;
            string connectionString = parameter.ConnectionString;

            if (string.IsNullOrEmpty(connectionString)) {
                logger.Warn($"ClearDatabase: ConnectionString is empty for SQLite database {name}");
            }
            else {
                var parser = new DbConnectionStringBuilder();
                parser.ConnectionString = connectionString;
                string? file = "";
                if (parser.ContainsKey("Filename")) {
                    file = parser["Filename"] as string;
                }
                else if (parser.ContainsKey("Data Source")) {
                    file = parser["Data Source"] as string;
                }
                else if (parser.ContainsKey("DataSource")) {
                    file = parser["DataSource"] as string;
                }
                else {
                    logger.Warn($"ClearDatabase: Failed to extract file name from connection string {connectionString} of SQLite database {name}");
                }

                if (!string.IsNullOrEmpty(file) && File.Exists(file)) {
                    try {
                        File.Delete(file);
                    }
                    catch (Exception) {
                        logger.Info($"ClearDatabase: Failed to delete SQLite DB file {file}. Reverting to default implementation.");
                        base.ClearDatabase(parameter);
                    }
                }
            }
        }

        public override void Open(OpenParams parameter) {

            if (IsOpen) throw new Invalid​Operation​Exception("DB already open");

            bool readOnly = parameter.ReadWriteMode == Mode.ReadOnly;
            string name = parameter.Name;
            string connectionString = parameter.ConnectionString;
            string[]? settings = parameter.Settings;
            retentionTime = parameter.RetentionTime;
            retentionCheckInterval = parameter.RetentionCheckInterval ?? Duration.FromHours(1);

            try {
                if (string.IsNullOrEmpty(connectionString)) {
                    connectionString = $"Filename=\"{name}\";";
                    logger.Warn($"No ConnectionString configured for SQLite database {name}. Assuming: {connectionString}");
                }
                if (readOnly) {
                    if (!connectionString.EndsWith(';')) {
                        connectionString += ";";
                    }
                    connectionString += "Mode=ReadOnly;";
                }
                var connection = Factory.MakeConnection(connectionString);
                connection.Open();
                this.connection = connection;
                stmtSelectChannel = new PreparedStatement(connection, $"SELECT * FROM channel_defs WHERE obj = @1 AND var = @2", 2);
            }
            catch (Exception exp) {
                throw new Application​Exception($"Opening SQLite database {name} failed: " + exp.Message);
            }

            if (settings != null && settings.Length > 0) {

                foreach (string setting in settings) {
                    var (settingName, value) = SplitSetting(setting);
                    switch (settingName.ToLowerInvariant()) {
                        case "table-name-separator":
                            tableSeparator = value;
                            break;
                        default:
                            string sql = "PRAGMA " + setting + ";";
                            using (var command = Factory.MakeCommand(sql, connection)) {
                                command.ExecuteNonQuery();
                            }
                            break;
                    }
                }
            }

            CheckDbChannelInfoOrCreate();
        }

        private void CheckDbChannelInfoOrCreate() {

            using (var command = Factory.MakeCommand("SELECT tbl_name FROM sqlite_master WHERE type = 'table' AND tbl_name = 'channel_defs';", connection!)) {
                if (command.ExecuteScalar() != null) {
                    return;
                }
            }

            using (var command = Factory.MakeCommand("CREATE TABLE channel_defs (obj TEXT not null, var TEXT not null, type TEXT not null, table_name TEXT not null, primary key (obj, var));", connection!)) {
                command.ExecuteNonQuery();
            }
        }

        public override void Close() {

            if (!IsOpen) return;

            try {
                connection!.Close();
            }
            catch (Exception exp) {
                logger.Warn(exp, "Closing database failed: " + exp.Message);
            }
            finally {
                connection = null;
            }
            cacheChannelEntry.Clear();
        }

        public override bool ExistsChannel(string objectID, string variable) {
            CheckDbOpen();
            ChannelEntry? entry = GetChannelDescription(objectID, variable);
            return entry.HasValue;
        }

        public override Channel GetChannel(string objectID, string variable) {
            CheckDbOpen();
            ChannelEntry? entry = GetChannelDescription(objectID, variable);
            if (!entry.HasValue) throw new ArgumentException($"No channel found with obj={objectID} avr={variable}");
            return new SQLiteChannel(connection!, entry.Value.MakeInfo(), entry.Value.DataTableName, this);
        }

        public override bool RemoveChannel(string objectID, string variable) {
            CheckDbOpen();

            ChannelEntry? entry = GetChannelDescription(objectID, variable);
            if (!entry.HasValue) return false;
            string table = entry.Value.DataTableName;

            using (var command = Factory.MakeCommand($"DROP TABLE \"{table}\"", connection!)) {
                command.ExecuteNonQuery();
            }
            using (var command = Factory.MakeCommand($"DELETE FROM channel_defs WHERE obj = @obj AND var = @var", connection!)) {
                command.Parameters.Add(Factory.MakeParameter("obj", objectID));
                command.Parameters.Add(Factory.MakeParameter("var", variable));
                command.ExecuteNonQuery();
            }
            cacheChannelEntry.Clear();
            return true;
        }

        public override ChannelInfo[] GetAllChannels() {
            CheckDbOpen();
            var res = new List<ChannelInfo>();

            using (var command = Factory.MakeCommand($"SELECT * FROM channel_defs", connection!)) {
                using var reader = command.ExecuteReader();
                while (reader.Read()) {
                    string obj = (string)reader["obj"];
                    string variable = (string)reader["var"];
                    string strType = (string)reader["type"];
                    DataType type = (DataType)Enum.Parse(typeof(DataType), strType, ignoreCase: true);
                    res.Add(new ChannelInfo(obj, variable, type));
                }
            }
            return res.ToArray();
        }

        public override Channel[] CreateChannels(ChannelInfo[] channels) {
            CheckDbOpen();

            var lowerCaseTableNames = new HashSet<string>();
            using (var command = Factory.MakeCommand($"SELECT table_name FROM channel_defs", connection!)) {
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        lowerCaseTableNames.Add(((string)reader["table_name"]).ToLowerInvariant());
                    }
                }
            }

            using (var transaction = connection!.BeginTransaction()) {

                try {

                    var res = new List<Channel>();

                    foreach (ChannelInfo ch in channels) {

                        string tableName = MakeValidTableName(ch.Object, ch.Variable, lowerCaseTableNames);

                        using (var command = Factory.MakeCommand("INSERT INTO channel_defs VALUES (@obj, @var, @type, @table_name)", connection)) {
                            command.Transaction = transaction;
                            command.Parameters.Add(Factory.MakeParameter("obj", ch.Object));
                            command.Parameters.Add(Factory.MakeParameter("var", ch.Variable));
                            command.Parameters.Add(Factory.MakeParameter("type", ch.Type.ToString()));
                            command.Parameters.Add(Factory.MakeParameter("table_name", tableName));
                            command.ExecuteNonQuery();
                        }

                        using (var command = Factory.MakeCommand($"CREATE TABLE \"{tableName}\" (time INTEGER PRIMARY KEY, diffDB INTEGER, quality INTEGER, data TEXT)", connection)) {
                            command.Transaction = transaction;
                            command.ExecuteNonQuery();
                        }

                        var channel = new SQLiteChannel(connection, ch, tableName, this);
                        res.Add(channel);
                    }

                    transaction.Commit();

                    return res.ToArray();
                }
                catch (Exception) {
                    try {
                        transaction.Rollback();
                    }
                    catch (Exception exp) {
                        logger.Warn("CreateChannels: transaction.Rollback failed: " + exp.Message);
                    }
                    throw;
                }
            }
        }

        public override string[] BatchExecute(Func<PrepareContext, string?>[] updateActions) {

            CheckDbOpen();
            Timestamp timeDb = Timestamp.Now;

            // The transaction will automatically rollback if not completed successfully on Dispose
            using var transaction = connection!.BeginTransaction();
            var errors = new List<string>();
            var context = new SQLiteContext(timeDb, transaction);

            foreach (var action in updateActions) {
                string? error = action(context);
                if (error != null) {
                    errors.Add(error);
                }
            }

            transaction.Commit();

            CheckAndApplyRetention();

            return errors.ToArray();
        }

        public void CheckAndApplyRetention() {
            if (retentionTime == null || connection == null) return;

            Timestamp now = Timestamp.Now;
            if (now - lastRetentionCheck < retentionCheckInterval) return;  // Not time yet

            lastRetentionCheck = now;
            var sw = Stopwatch.StartNew();
            ApplyRetention(now - retentionTime.Value);
            sw.Stop();
            logger.Info($"Applied retention policy for SQLite Timeseries DB in {sw.ElapsedMilliseconds} ms");
        }

        private void ApplyRetention(Timestamp cutoff) {
            // The transaction will automatically rollback if not completed successfully on Dispose
            using var transaction = connection!.BeginTransaction();
            using (var command = Factory.MakeCommand($"SELECT * FROM channel_defs", connection!)) {
                command.Transaction = transaction;
                using var reader = command.ExecuteReader();
                while (reader.Read()) {
                    string tableName = (string)reader["table_name"];
                    string table = "\"" + tableName + "\""; ;
                    using var cmd = Factory.MakeCommand($"DELETE FROM {table} WHERE time < @cutoff", connection!);
                    cmd.Transaction = transaction;
                    cmd.Parameters.Add(Factory.MakeParameter("cutoff", cutoff.JavaTicks));
                    cmd.ExecuteNonQuery();
                }
            }
            transaction.Commit();
        }

        protected void CheckDbOpen() {
            if (!IsOpen) throw new Exception("Invalid operation on closed database");
        }

        private readonly Dictionary<ChannelRef, ChannelEntry> cacheChannelEntry = new Dictionary<ChannelRef, ChannelEntry>();

        protected ChannelEntry? GetChannelDescription(string objectID, string variable) {

            var channelRef = ChannelRef.Make(objectID, variable);
            if (cacheChannelEntry.TryGetValue(channelRef, out var mapEnt)) {
                return mapEnt;
            }

            var stmtSelectChannel = this.stmtSelectChannel;

            if (stmtSelectChannel == null) {
                return null;
            }

            stmtSelectChannel[0] = objectID;
            stmtSelectChannel[1] = variable;

            using (var reader = stmtSelectChannel.ExecuteReader()) {
                if (!reader.Read()) return null;
                string type = (string)reader["type"];
                ChannelEntry chEntry = new ChannelEntry() {
                    Object = objectID,
                    Variable = variable,
                    DataTableName = (string)reader["table_name"],
                    Type = (DataType)Enum.Parse(typeof(DataType), type, ignoreCase: true)
                };
                cacheChannelEntry[channelRef] = chEntry;
                return chEntry;
            }
        }

        protected string MakeValidTableName(string obj, string varName, HashSet<string> existingNamesLowerCase) {
            obj = obj.Replace('\"', '\'');
            varName = varName.Replace('\"', '\'');
            string res = obj + tableSeparator + varName;
            int i = 1;
            while (existingNamesLowerCase.Contains(res.ToLowerInvariant())) {
                res = obj + tableSeparator + varName + "_" + string.Format("{0:000}", i);
                i += 1;
            }
            return res;
        }

        public struct ChannelEntry
        {
            public string Object { get; set; }
            public string Variable { get; set; }
            public string DataTableName { get; set; }
            public DataType Type { get; set; }

            public ChannelInfo MakeInfo() => new ChannelInfo(Object, Variable, Type);
        }
    }

    public class SQLiteContext : PrepareContext
    {
        public readonly DbTransaction Transaction;

        public SQLiteContext(Timestamp timeDB, DbTransaction transaction) : base(timeDB) {
            Transaction = transaction;
        }
    }

    public static class Factory
    {
        public static DbConnection MakeConnection(string connectionString) {
            //return new System.Data.SQLite.SQLiteConnection(connectionString);
            return new Microsoft.Data.Sqlite.SqliteConnection(connectionString);
        }

        public static DbCommand MakeCommand(string commandText, DbConnection connection) {
            //return new System.Data.SQLite.SQLiteCommand(commandText, (System.Data.SQLite.SQLiteConnection)connection);
            return new Microsoft.Data.Sqlite.SqliteCommand(commandText, (Microsoft.Data.Sqlite.SqliteConnection)connection);
        }

        public static DbParameter MakeParameter(string name, object? value = null) {
            //return new System.Data.SQLite.SQLiteParameter(name, value);
            return new Microsoft.Data.Sqlite.SqliteParameter(name, value);
        }
    }
}
