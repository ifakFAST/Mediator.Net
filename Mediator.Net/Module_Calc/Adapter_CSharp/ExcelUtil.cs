// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace ExcelUtil;

public enum CellType
{
    String,
    FormattedString,
    Number,
    DateTime,
    Boolean,
}

public sealed class ExcelReader : IDisposable
{
    private readonly FileStream stream;
    private readonly XLWorkbook workbook;

    public ExcelReader(string fileName) {
        stream = new(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        workbook = new(stream);
    }

    public void Dispose() {
        workbook.Dispose();
        stream.Dispose();
    }

    public interface IColumnInfo
    {
        public string Name { get; }
        public CellType Type { get; }
    }

    internal record ColumnInfo(string Name, CellType Type) : IColumnInfo;

    public static IColumnInfo Column(string name, CellType type) {
        return new ColumnInfo(name, type);
    }

    public IEnumerable<object?[]> EnumerateRows(string sheetName, int skipFirstRows, IColumnInfo[] columns) {

        var worksheet = workbook.Worksheet(sheetName);
        var rows = worksheet.RowsUsed().Skip(skipFirstRows);

        object?[] objects = new object[columns.Length];

        foreach (var row in rows) {

            for (int i = 0; i < columns.Length; i++) {

                IColumnInfo col = columns[i];
                var cell = row.Cell(col.Name);

                if (cell.IsEmpty()) {
                    objects[i] = null;
                    continue;
                }

                try {
                    objects[i] = col.Type switch {
                        CellType.String => cell.GetString(),
                        CellType.FormattedString => cell.GetFormattedString(),
                        CellType.Number => cell.GetDouble(),
                        CellType.DateTime => cell.GetDateTime(),
                        CellType.Boolean => cell.GetBoolean(),
                        _ => throw new NotSupportedException()
                    };
                }
                catch {
                    objects[i] = null;
                }
            }

            yield return objects;
        }
    }

    public string[] GetSheetNames() {
        return workbook.Worksheets.Select(ws => ws.Name).ToArray();
    }

    public string[] GetColumnNames(string sheetName) {
        var worksheet = workbook.Worksheet(sheetName);
        var firstRow = worksheet.Row(1);
        return firstRow.Cells().Select(cell => cell.GetString()).ToArray();
    }

    public string[] GetDistinctStringValuesFromColumn(string sheetName, string column, int skipFirstRows = 1) {

        var worksheet = workbook.Worksheet(sheetName);

        var rows = worksheet.RowsUsed().Skip(skipFirstRows);
        var uniqueValues = new HashSet<string>();
        var result = new List<string>();

        foreach (var row in rows) {

            string cellValue;

            try {
                cellValue = row.Cell(column).GetString();
            }
            catch { continue; }

            if (!string.IsNullOrWhiteSpace(cellValue)) {
                if (!uniqueValues.Contains(cellValue)) {
                    uniqueValues.Add(cellValue);
                    result.Add(cellValue);
                }
            }
        }

        return result.ToArray();
    }
}
