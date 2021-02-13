// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using NLog;

namespace Ifak.Fast.Mediator.Timeseries.PostgresFlat
{
    public class PostgresFlatTimeseriesDB : TimeSeriesDB
    {
        private static Logger logger = LogManager.GetLogger("PostgresFlatTimeseriesDB");

        protected Npgsql.NpgsqlConnection connection = null;

        public PostgresFlatTimeseriesDB() {
            // Npgsql.Logging.NpgsqlLogManager.Provider = new Npgsql.Logging.ConsoleLoggingProvider(Npgsql.Logging.NpgsqlLogLevel.Debug, true);
        }

        public override bool IsOpen => connection != null;

        public override void Open(string name, string connectionString, string[] settings = null) {

            if (IsOpen) throw new Invalid​Operation​Exception("DB already open");

            if (string.IsNullOrEmpty(connectionString)) {
                throw new Application​Exception($"Missing ConnectionString for Postgres DB {name}");
            }

            try {
                var connection = Factory.MakeConnection(connectionString);
                connection.Open();
                this.connection = connection;
            }
            catch (Exception exp) {
                throw new Application​Exception($"Opening Postgres database {name} failed: " + exp.Message);
            }

            if (settings != null) {
                foreach (string setting in settings) {
                    var (settingName, value) = SplitSetting(setting);
                    switch (settingName.ToLowerInvariant()) {
                        default:
                            logger.Info($"Invalid setting '{settingName}' for Postgres timeseries DB {name}");
                            break;
                    }
                }
            }

            CheckDbChannelInfoOrCreate();
        }

        private void CheckDbChannelInfoOrCreate() {
            using (var command = Factory.MakeCommand("CREATE TABLE IF NOT EXISTS channel_defs (obj TEXT not null, var TEXT not null, type TEXT not null, varID SERIAL UNIQUE, primary key (obj, var));", connection)) {
                command.ExecuteNonQuery();
            }
            using (var command = Factory.MakeCommand("CREATE TABLE IF NOT EXISTS channel_data (varID INTEGER REFERENCES channel_defs(varID), time timestamp, diffDB INTEGER NOT NULL, quality smallint NOT NULL, data TEXT NOT NULL, primary key (varID, time));", connection)) {
                command.ExecuteNonQuery();
            }
        }

        public override void Close() {

            if (!IsOpen) return;

            try {
                connection.Close();
            }
            catch (Exception exp) {
                logger.Warn(exp, "Closing database failed: " + exp.Message);
            }
            finally {
                connection = null;
            }
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
            return new PostgresFlatChannel(connection, entry.Value.MakeInfo(), entry.Value.VarID, $"{objectID}.{variable}");
        }

        public override bool RemoveChannel(string objectID, string variable) {
            CheckDbOpen();

            ChannelEntry? entry = GetChannelDescription(objectID, variable);
            if (!entry.HasValue) return false;

            var ch = GetChannel(objectID, variable) as PostgresFlatChannel;
            ch.DeleteAll();

            using (var command = Factory.MakeCommand($"DELETE FROM channel_defs WHERE obj = @obj AND var = @var", connection)) {
                command.Parameters.Add(Factory.MakeParameter("obj", objectID));
                command.Parameters.Add(Factory.MakeParameter("var", variable));
                command.ExecuteNonQuery();
            }
            return true;
        }

        public override ChannelInfo[] GetAllChannels() {
            CheckDbOpen();
            var res = new List<ChannelInfo>();

            using (var command = Factory.MakeCommand($"SELECT * FROM channel_defs", connection)) {
                using (var reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        string obj = (string)reader["obj"];
                        string variable = (string)reader["var"];
                        string strType = (string)reader["type"];
                        DataType type = (DataType)Enum.Parse(typeof(DataType), strType, ignoreCase: true);
                        res.Add(new ChannelInfo(obj, variable, type));
                    }
                }
            }
            return res.ToArray();
        }

        public override Channel[] CreateChannels(ChannelInfo[] channels) {
            const int Chunck = 500;
            var res = new List<Channel>(channels.Length);
            for (int i = 0; i < channels.Length; i += Chunck) {
                int len = Math.Min(Chunck, channels.Length - i);
                Span<ChannelInfo> span = channels.AsSpan(i, len);
                res.AddRange(DoCreateChannels(span));
            }
            return res.ToArray();
        }

        public Channel[] DoCreateChannels(Span<ChannelInfo> channels) {

            using (var transaction = connection.BeginTransaction()) {

                try {

                    var res = new List<Channel>();

                    foreach (ChannelInfo ch in channels) {

                        using (var command = Factory.MakeCommand("INSERT INTO channel_defs VALUES (@obj, @var, @type)", connection)) {
                            command.Transaction = transaction;
                            command.Parameters.Add(Factory.MakeParameter("obj", ch.Object));
                            command.Parameters.Add(Factory.MakeParameter("var", ch.Variable));
                            command.Parameters.Add(Factory.MakeParameter("type", ch.Type.ToString()));
                            command.ExecuteNonQuery();
                        }

                        ChannelEntry? entry = GetChannelDescription(ch.Object, ch.Variable);
                        if (!entry.HasValue) {
                            throw new Exception($"Failed to create channel entry for {ch.Object}.{ch.Variable}");
                        }

                        var channel = new PostgresFlatChannel(connection, ch, entry.Value.VarID, $"{ch.Object}.{ch.Variable}");
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
                        logger.Warn("DoCreateChannels: transaction.Rollback failed: " + exp.Message);
                    }
                    throw;
                }
            }
        }

        public override string[] BatchExecute(Func<PrepareContext, string>[] updateActions) {

            Timestamp timeDb = Timestamp.Now;

            using (var transaction = connection.BeginTransaction()) {

                try {

                    var errors = new List<string>();
                    var context = new PostgresContext(timeDb, transaction);

                    foreach (var action in updateActions) {
                        string error = action(context);
                        if (error != null) {
                            errors.Add(error);
                        }
                    }

                    transaction.Commit();

                    return errors.ToArray();
                }
                catch (Exception) {

                    try {
                        transaction.Rollback();
                    }
                    catch (Exception exp) {
                        logger.Warn("BatchExecute: transaction.Rollback failed: " + exp.Message);
                    }

                    throw;
                }
            }
        }

        protected void CheckDbOpen() {

            if (!IsOpen) throw new Exception("Invalid operation on closed database");

            ConnectionState state;
            try {
                state = connection.FullState;
            }
            catch (Exception exp) {
                state = ConnectionState.Broken;
                logger.Warn($"CheckDbOpen: Exception when reading FullState of PostgresDB connection: {exp.Message}");
            }

            if (state.HasFlag(ConnectionState.Broken) || state == ConnectionState.Closed) {
                logger.Warn($"CheckDbOpen: Connection to PostgresDB is {state}. Trying to reopen...");
                try {
                    connection.Close();
                }
                catch (Exception) { }
                connection.Open();
                logger.Info("CheckDbOpen: Reopening connection to PostgresDB succeeded. ");
            }
        }

        protected ChannelEntry? GetChannelDescription(string objectID, string variable) {
            using (var command = Factory.MakeCommand($"SELECT * FROM channel_defs WHERE obj = @obj AND var = @var", connection)) {
                command.Parameters.Add(Factory.MakeParameter("obj", objectID));
                command.Parameters.Add(Factory.MakeParameter("var", variable));
                using (var reader = command.ExecuteReader()) {
                    if (!reader.Read()) return null;
                    string type = (string)reader["type"];
                    return new ChannelEntry() {
                        Object = objectID,
                        Variable = variable,
                        VarID = (int)reader["varID"],
                        Type = (DataType)Enum.Parse(typeof(DataType), type, ignoreCase: true)
                    };
                }
            }
        }

        public struct ChannelEntry
        {
            public string Object { get; set; }
            public string Variable { get; set; }
            public int VarID { get; set; }
            public DataType Type { get; set; }

            public ChannelInfo MakeInfo() => new ChannelInfo(Object, Variable, Type);
        }
    }

    public class PostgresContext : PrepareContext
    {
        public readonly DbTransaction Transaction;

        public PostgresContext(Timestamp timeDB, DbTransaction transaction) : base(timeDB) {
            Transaction = transaction;
        }
    }

    static class Factory
    {
        internal static Npgsql.NpgsqlConnection MakeConnection(string connectionString) {
            return new Npgsql.NpgsqlConnection(connectionString);
        }

        internal static DbCommand MakeCommand(string commandText, DbConnection connection) {
            return new Npgsql.NpgsqlCommand(commandText, (Npgsql.NpgsqlConnection)connection);
        }

        internal static DbParameter MakeParameter(string name, object value) {
            return new Npgsql.NpgsqlParameter(name, value);
        }
    }
}
