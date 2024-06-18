// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_SQL;

public abstract class SQL_ImportHistoric_Base : SQL_Query_Base
{
    public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
        return Task.FromResult(Array.Empty<string>());
    }

    protected override async Task<VTQ> ReadDataItemFromDB(DataItem item, VTQ lastValue) {

        const int ChunkSize = 6000;

        while (true) {

            List<VTQ> rows = await ReadRows(item, lastValue, ChunkSize);
            int N = rows.Count;

            if (N == 0) {
                return lastValue;
            }

            DataItemValue[] values = new DataItemValue[N];

            for (int i = 0; i < N; ++i) {
                values[i] = new DataItemValue(item.ID, rows[i]);
            }

            callback?.Notify_DataItemsChanged(values);

            lastValue = rows[N - 1];

            if (N < ChunkSize) {
                return lastValue;
            }
        }
    }

    protected virtual async Task<List<VTQ>> ReadRows(DataItem item, VTQ lastValue, int maxRows) {

        string lastTime = LastTime(lastValue);
        string query = GetQuery(item, lastValue, lastTime, maxRows);

        if (string.IsNullOrEmpty(query)) {
            return [];
        }

        using DbCommand cmd = db.CreateCommand(dbConnection!, query);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using DbDataReader reader = await cmd.ExecuteReaderAsync();
        sw.Stop();

        Console.WriteLine($"Query took {sw.ElapsedMilliseconds} ms");

        var rows = new List<VTQ>(maxRows);

        while (reader.Read()) {
            Timestamp t = TimestampFromReader(reader);
            DataValue dv = DataValueFromReader(reader);
            Quality q = QualityFromReader(reader);
            rows.Add(VTQ.Make(dv, t, q));
        }

        return rows;
    }

    protected virtual string LastTime(VTQ lastValue) {
        Duration timeOffset = GetTimeOffset();
        Timestamp lastT = Timestamp.MaxOf(lastValue.T + timeOffset, GetStartTime());
        return GetTimestampFormat() switch {
            TimestampFormat.String => $"'{lastT.ToString()}'",
            TimestampFormat.UnixTime => (lastT.JavaTicks/1000L).ToString(CultureInfo.InvariantCulture),
            TimestampFormat.UnixTimeMS => lastT.JavaTicks.ToString(CultureInfo.InvariantCulture),
            TimestampFormat.DotNetTicks => lastT.DotNetTicks.ToString(CultureInfo.InvariantCulture),
            _ => throw new Exception("Invalid TimestampType")
        };
    }

    protected virtual TimestampFormat GetTimestampFormat() => TimestampFormat.String;

    protected abstract string GetQuery(DataItem item, VTQ lastValue, string lastTime, int maxRows);

    protected virtual Timestamp GetStartTime() => Timestamp.FromComponents(2020, 1, 1);

    protected virtual Duration GetTimeOffset() => Duration.FromHours(0);

    protected virtual Timestamp TimestampFromReader(DbDataReader reader) {
        DateTime time = reader.GetDateTime("Time");
        return Timestamp.FromDateTime(time) - GetTimeOffset();
    }

    protected virtual DataValue DataValueFromReader(DbDataReader reader) {
        object value = reader.GetValue("Value");
        return DataValue.FromObject(value);
    }

    protected virtual Quality QualityFromReader(DbDataReader reader) {
        return Quality.Good;
    }
}

public enum TimestampFormat { String, UnixTime, UnixTimeMS, DotNetTicks }