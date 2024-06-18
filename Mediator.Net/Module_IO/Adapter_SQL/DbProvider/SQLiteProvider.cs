// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL.DbProvider;

public class SQLiteProvider : DatabaseProvider
{
    public override DbCommand CreateCommand(DbConnection dbConnection, string cmdText) {
        return new SqliteCommand(cmdText, (SqliteConnection)dbConnection);
    }

    public override DbConnection CreateConnection(string connectionString, int timeoutSeconds) {
        var connection = new SqliteConnection(connectionString);
        return connection;
    }

    public override async Task<bool> TestConnection(DbConnection? dbConnection) {

        if (dbConnection == null) return false;

        try {

            var con = (SqliteConnection)dbConnection;

            ConnectionState state = con.State;
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
