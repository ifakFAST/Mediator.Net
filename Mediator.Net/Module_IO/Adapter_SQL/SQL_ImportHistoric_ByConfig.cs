// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.IO.Adapter_SQL.DbProvider;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL;

[Identify("SQL Import Historic")]
public class SQL_ImportHistoric_ByConfig : SQL_ImportHistoric_Base
{
    private DatabaseProvider.DatabaseType databaseType = DatabaseProvider.DatabaseType.PostgreSQL;
    private string query = "";
    private string sampleDataItemAddress = "";
    private Duration offset = Duration.FromHours(0);
    private Timestamp startTime = Timestamp.FromComponents(2020, 1, 1);
    private TimestampFormat timestampFormat = TimestampFormat.String;

    protected override DatabaseProvider.DatabaseType DatabaseType => databaseType;
    protected override Timestamp GetStartTime() => startTime;
    protected override Duration GetTimeOffset() => offset;
    protected override TimestampFormat GetTimestampFormat() => timestampFormat;

    public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

        string strDbType = config.GetConfigByName("Type", "PostgreSQL");

        if (!Enum.TryParse(strDbType, ignoreCase: true, out databaseType)) {
            PrintErrorLine($"Invalid Type: {strDbType}. Assuming PostgreSQL...");
        }

        query = config.GetConfigByName("Query", "");
        if (!ValidateQuery(query, out string err)) {
            PrintErrorLine($"Invalid Query: {err}");
            query = "";
        }
        sampleDataItemAddress = config.GetConfigByName("SampleAddress", "");

        string strOffset = config.GetConfigByName("TimeOffset", "0 h");
        if (!Duration.TryParse(strOffset, out offset)) {
            PrintErrorLine($"Invalid value for config parameter 'TimeOffset': '{strOffset}'");
        }

        string strStartTime = config.GetConfigByName("StartTime", "2020-01-01");
        if (!Timestamp.TryParse(strStartTime, out startTime)) {
            PrintErrorLine($"Invalid value for config parameter 'StartTime': '{strStartTime}'");
        }

        string strTimestampFormat = config.GetConfigByName("TimestampFormat", "String");
        if (!Enum.TryParse(strTimestampFormat, ignoreCase: true, out timestampFormat)) {
            PrintErrorLine($"Invalid TimestampFormat: {strTimestampFormat}. Assuming String...");
        }

        return base.Initialize(config, callback, itemInfos);
    }

    const string MaxRows = "{MaxRows}";
    const string LastTimestamp = "{LastTimestamp}";
    const string Address = "{Address}";

    protected static bool ValidateQuery(string query, out string err) {

        if (!query.Contains("SELECT", StringComparison.OrdinalIgnoreCase)) {
            err = "Missing keyword 'SELECT'";
            return false;
        }

        if (!query.Contains(MaxRows, StringComparison.Ordinal)) {
            err = $"Missing placeholder {MaxRows}";
            return false;
        }

        if (!query.Contains(LastTimestamp, StringComparison.Ordinal)) {
            err = $"Missing placeholder {LastTimestamp}";
            return false;
        }

        if (!query.Contains(Address, StringComparison.Ordinal)) {
            err = $"Missing placeholder {Address}";
            return false;
        }

        err = "";
        return true;
    }

    public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
        return Task.FromResult(string.IsNullOrEmpty(sampleDataItemAddress)
            ? Array.Empty<string>()
            : [sampleDataItemAddress]);
    }

    protected override string GetQuery(DataItem item, VTQ lastValue, string lastTimestamp, int maxRows) {
        string strMaxRows = maxRows.ToString(CultureInfo.InvariantCulture);
        return query
            .Replace(MaxRows, strMaxRows)
            .Replace(LastTimestamp, lastTimestamp)
            .Replace(Address, item.Address);
    }
}
