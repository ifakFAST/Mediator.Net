﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using MySqlConnector;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL.DbProvider;

public class MySQLProvider : DatabaseProvider
{
    public override DbCommand CreateCommand(DbConnection dbConnection, string cmdText) {
        return new MySqlCommand(cmdText, (MySqlConnection)dbConnection);
    }

    public override DbConnection CreateConnection(string connectionString, int timeoutSeconds) {
        bool hasTimeout = connectionString.Contains("timeout", StringComparison.OrdinalIgnoreCase);
        if (!hasTimeout) {
            MySqlConnectionStringBuilder builder = new(connectionString);
            builder.ConnectionTimeout = (uint)timeoutSeconds;
            connectionString = builder.ConnectionString;
        }
        return new MySqlConnection(connectionString);
    }

    public override async Task<bool> TestConnection(DbConnection? dbConnection) {

        if (dbConnection == null) return false;

        try {

            var con = (MySqlConnection)dbConnection;

            ConnectionState state = con.State;
            if (state.HasFlag(ConnectionState.Broken) || state == ConnectionState.Closed) {
                return false;
            }

            return await con.PingAsync();
        }
        catch (Exception) {
            return false;
        }
    }
}
