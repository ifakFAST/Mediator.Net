// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Npgsql;
using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Publish.SQL;

internal class SQLPubVar_Postgres : SQLPubVar {

    public SQLPubVar_Postgres(string dataFolder, SQLConfig config) 
        : base(dataFolder, config) {

    }

    protected override DbBatch CreateBatch(DbConnection dbConnection) {
        return new NpgsqlBatch((NpgsqlConnection)dbConnection);
    }

    protected override DbCommand CreateCommand(DbConnection dbConnection, string cmdText) {
        return new NpgsqlCommand(cmdText, (NpgsqlConnection)dbConnection);
    }

    protected override DbConnection CreateConnection(string connectionString) {
        return new NpgsqlConnection(connectionString);
    }

    protected override DbParameter CreateParameter(string name, object value) {
        return new NpgsqlParameter(name, value);
    }

    protected override Task<bool> TestConnection(DbConnection dbConnection) {

        try {

            var con = (NpgsqlConnection)dbConnection;

            ConnectionState state;
            try {
                state = con.FullState;
            }
            catch (Exception) {
                state = ConnectionState.Broken;
            }

            if (state.HasFlag(ConnectionState.Broken) || state == ConnectionState.Closed) {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
        catch (Exception) {
            return Task.FromResult(false);
        }
    }
}
