// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL.DbProvider;

public abstract class DatabaseProvider
{
    public abstract Task<bool> TestConnection(DbConnection? dbConnection);
    public abstract DbConnection CreateConnection(string connectionString, int timeoutSeconds);
    public abstract DbCommand CreateCommand(DbConnection dbConnection, string cmdText);

    public enum DatabaseType
    {
        SQLite,
        PostgreSQL,
        MySQL,
        MSSQL,
    }

    public static DatabaseProvider Create(DatabaseType type) {
        return type switch {
            DatabaseType.SQLite => new SQLiteProvider(),
            DatabaseType.PostgreSQL => new PostgreSQLProvider(),
            DatabaseType.MySQL => new MySQLProvider(),
            DatabaseType.MSSQL => new MSSQLProvider(),
            _ => throw new Exception($"Invalid DatabaseType '{type}'")
        };
    }
}
