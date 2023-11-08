// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL
{
    [Identify("SQL Query Postgres")]
    public class SQL_Query_Postgres : SQL_Query_Base
    {
        protected override DbCommand CreateCommand(DbConnection dbConnection, string cmdText) {
            return new NpgsqlCommand(cmdText, (NpgsqlConnection)dbConnection);
        }

        protected override DbConnection CreateConnection(string connectionString, int timeoutSeconds) {
            bool hasTimeout = connectionString.ToLowerInvariant().Contains("timeout");
            if (!hasTimeout) {
                NpgsqlConnectionStringBuilder builder = new(connectionString);
                builder.Timeout = timeoutSeconds;
                connectionString = builder.ConnectionString;
            }
            return new NpgsqlConnection(connectionString);
        }

        protected override async Task<bool> TestConnection(DbConnection? dbConnection) {

            if (dbConnection == null) return false;

            try {

                var con = (NpgsqlConnection)dbConnection;

                ConnectionState state = con.FullState;
                if (state.HasFlag(ConnectionState.Broken) || state == ConnectionState.Closed) {
                    return false;
                }

                using var cmd = CreateCommand(con, "SELECT 1;");
                object? test = await cmd.ExecuteScalarAsync();
                if (test is int i && i == 1) { return true; }
                if (test is long l && l == 1) { return true; }

                return false;
            }
            catch (Exception) {
                return false;
            }
        }
    }
}
