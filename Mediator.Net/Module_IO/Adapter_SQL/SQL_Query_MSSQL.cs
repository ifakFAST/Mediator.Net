// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL
{
    [Identify("SQL Query MS SQL")]
    public class SQL_Query_MSSQL : SQL_Query_Base
    {
        protected override DbCommand CreateCommand(DbConnection dbConnection, string cmdText) {
            return new SqlCommand(cmdText, (SqlConnection)dbConnection);
        }

        protected override DbConnection CreateConnection(string connectionString) {
            return new SqlConnection(connectionString);
        }

        protected override Task<bool> TestConnection(DbConnection dbConnection) {

            try {

                var con = (SqlConnection)dbConnection;

                ConnectionState state;
                try {
                    state = con.State;
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
}
