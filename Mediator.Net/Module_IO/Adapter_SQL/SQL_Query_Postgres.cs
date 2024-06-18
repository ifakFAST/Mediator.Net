// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.IO.Adapter_SQL.DbProvider;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL;

[Identify("SQL Query Postgres")]
public class SQL_Query_Postgres : SQL_Query_Base
{
    protected override DatabaseProvider.DatabaseType DatabaseType => DatabaseProvider.DatabaseType.PostgreSQL;
}
