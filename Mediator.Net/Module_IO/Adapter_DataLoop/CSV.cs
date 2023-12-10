using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Linq;

namespace Ifak.Fast.Mediator.IO.Adapter_DataLoop;

public record CsvContent(string[] Header, IList<Row> Rows) {

    public void FillGaps() {

        var rows = Rows;
        if (rows.Count < 2) return;

        DataValue[] values = rows[0].Values.Select(v => v).ToArray();

        for (int i = 1; i < rows.Count; ++i) {
            Row row = rows[i];
            for (int j = 0; j < Header.Length; ++j) {
                DataValue currVal = row.Values[j];
                if (currVal.IsEmpty) {
                    row.Values[j] = values[j];
                }
                else {
                    values[j] = currVal;
                }
            }
        }
    }

}

public record Row(
    DateTime Time,
    DataValue[] Values);

public enum TimeUnit {
    Second,
    Minute,
    Hour,
    Day,
}

public static class CSV {

    public static CsvContent ReadFromFile(string fileName, DateTime anchor, TimeUnit unit) {

        TimeSpan unitSpan = unit switch {
            TimeUnit.Second => TimeSpan.FromSeconds(1),
            TimeUnit.Minute => TimeSpan.FromMinutes(1),
            TimeUnit.Hour => TimeSpan.FromHours(1),
            TimeUnit.Day => TimeSpan.FromDays(1),
            _ => throw new Exception($"Invalid TimeUnit '{unit}'")
        };

        var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.InvariantCulture) {
            HasHeaderRecord = true,
            Delimiter = ",",
        };

        using var reader = new StreamReader(fileName, Encoding.UTF8);
        using var csv = new CsvHelper.CsvReader(reader, config);

        csv.Read();
        csv.ReadHeader();

        string[] header = csv.HeaderRecord ?? throw new Exception("Missing header");
        int N = header.Length - 1;

        var rows = new List<Row>();

        while (csv.Read()) {

            string time = csv.GetField(0)!.Trim();

            DateTime t;

            if (TryParseDateTime(time, out DateTime tt)) {
                t = tt;
            }
            else if (TryParseDouble(time, out double count)) {
                TimeSpan off = count * unitSpan;
                t = anchor + off;
            }
            else {
                throw new Exception($"Invalid time value '{time}'");
            }

            DataValue[] tagValues = new DataValue[N];
            for (int i = 1; i < header.Length; ++i) {
                string v = csv.GetField(i)!;
                DataValue dataValue = DataValue.FromJSON(v);
                tagValues[i - 1] = dataValue;
            }

            rows.Add(new Row(t, tagValues));
        }

        var cleanHeaders = header.Select(s => s.Trim()).Skip(1).ToArray();

        return new CsvContent(cleanHeaders, rows);
    }

    private static bool TryParseDouble(string s, out double d) {
        return double.TryParse(s, CultureInfo.InvariantCulture, out d);
    }

    public static bool TryParseDateTime(string s, out DateTime t) {
        bool ok = DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t);
        if (!ok) return false;
        if (t.Kind == DateTimeKind.Unspecified) {
            t = DateTime.SpecifyKind(t, DateTimeKind.Local);
        }
        return true;
    }
}