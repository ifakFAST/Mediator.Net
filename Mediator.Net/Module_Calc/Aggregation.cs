// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;

namespace Ifak.Fast.Mediator.Calc;

public static class AggregationUtils
{
    public static List<VTQs> Aggregate( IEnumerable<VTQs> listHistories,
                                        Aggregation aggregation,
                                        Duration resolution,
                                        bool skipEmptyIntervals) {

        return listHistories.Select(h => Aggregate(h, aggregation, resolution, skipEmptyIntervals)).ToList();
    }

    public static VTQs Aggregate(       VTQs values,
                                        Aggregation aggregation,
                                        Duration resolution,
                                        bool skipEmptyIntervals) {

        if (values == null || values.Count == 0) {
            return [];
        }

        var result = new VTQs();

        long resMillis = resolution.TotalMilliseconds;

        Timestamp currentIntervalStart = Timestamp.FromJavaTicks((values[0].T.JavaTicks / resMillis) * resMillis);
        List<double> currentIntervalValues = [];

        foreach (VTQ value in values) {

            double? vv = value.V.AsDouble();
            if (!vv.HasValue) {
                continue;
            }

            Timestamp intervalStart = Timestamp.FromJavaTicks((value.T.JavaTicks / resMillis) * resMillis);
            if (intervalStart != currentIntervalStart) {
                // Aggregate the current interval
                result.Add(AggregateCurrentInterval(currentIntervalStart, currentIntervalValues, aggregation));
                // Move to the next interval

                while (!skipEmptyIntervals && currentIntervalStart + resolution < intervalStart) {
                    currentIntervalStart += resolution;
                    result.Add(AggregateCurrentInterval(currentIntervalStart, [], aggregation));
                }

                currentIntervalStart = intervalStart;
                currentIntervalValues.Clear();
            }
            currentIntervalValues.Add(vv.Value);
        }

        // Aggregate the last interval
        if (currentIntervalValues.Count > 0) {
            result.Add(AggregateCurrentInterval(currentIntervalStart, currentIntervalValues, aggregation));
        }

        return result;
    }

    private static VTQ AggregateCurrentInterval( Timestamp intervalStart, 
                                                 List<double> values,
                                                 Aggregation aggregation) {

        if (values.Count == 0 && aggregation != Aggregation.Count) {
            return VTQ.Make(DataValue.Empty, intervalStart, Quality.Good);
        }

        var aggregatedValue = aggregation switch {
            Aggregation.Average => values.Average(),
            Aggregation.Min => values.Min(),
            Aggregation.Max => values.Max(),
            Aggregation.First => values.First(),
            Aggregation.Last => values.Last(),
            Aggregation.Count => values.Count,
            Aggregation.Sum => values.Sum(),
            _ => throw new ArgumentOutOfRangeException(nameof(aggregation), "Invalid aggregation method."),
        };
        return VTQ.Make(DataValue.FromDouble(aggregatedValue), intervalStart, Quality.Good);
    }

    public static TimeAlignedMatrix ExportToMatrix(IEnumerable<VTQs> variables) {

        // Create readers for each variable's history
        HistReader[] readers = variables.Select(v => new HistReader(v)).ToArray();

        // First pass: build list of timestamps
        var timestamps = new List<Timestamp>();
        bool hasData = readers.Any(r => r.HasValue);

        while (hasData) {
            // Find the earliest timestamp among all readers
            Timestamp earliestTime = Timestamp.Max;
            foreach (var reader in readers) {
                if (reader.HasValue && reader.Time < earliestTime) {
                    earliestTime = reader.Time;
                }
            }

            timestamps.Add(earliestTime);

            // Move readers forward for this timestamp
            for (int i = 0; i < readers.Length; i++) {
                var reader = readers[i];
                if (reader.HasValue && reader.Time == earliestTime) {
                    reader.MoveNext();
                }
            }

            hasData = readers.Any(r => r.HasValue);
        }

        // Reset all readers for second pass
        foreach (var reader in readers) {
            reader.Reset();
        }

        // Second pass: fill the matrix directly using pre-built timestamps
        int rowCount = timestamps.Count;
        var matrix = new double[rowCount, readers.Length];

        for (int row = 0; row < rowCount; row++) {

            Timestamp time = timestamps[row];

            // Fill values for each variable at this timestamp
            for (int i = 0; i < readers.Length; i++) {
                var reader = readers[i];
                if (reader.HasValue && reader.Time == time) {
                    double? val = reader.Value.AsDouble();
                    matrix[row, i] = val ?? double.NaN;
                    reader.MoveNext();
                }
                else {
                    matrix[row, i] = double.NaN; // Missing data for this variable at this timestamp
                }
            }
        }

        return new TimeAlignedMatrix {
            Timestamps = timestamps.ToArray(),
            TimestampsPosixSeconds = timestamps.Select(t => t.PosixSeconds).ToArray(),
            Values = matrix
        };
    }

    internal sealed class HistReader(VTQs data)
    {
        private readonly VTQs data = data;
        private int index = 0;

        public Timestamp Time => (index < data.Count) ? data[index].T : Timestamp.Max;

        public DataValue Value => data[index].V;

        public void MoveNext() {
            index++;
        }

        public void Reset() {
            index = 0;
        }

        public bool HasValue => index < data.Count;
    }
}

public class TimeAlignedMatrix
{
    public Timestamp[] Timestamps { get; set; } = [];
    public double[] TimestampsPosixSeconds { get; set; } = [];
    public double[,] Values { get; set; } = new double[0, 0];

    public void Print(int numDigits = 3) {
        int rows = Values.GetLength(0);
        int cols = Values.GetLength(1);

        if (rows == 0 || cols == 0) {
            Console.WriteLine("(empty matrix)");
            return;
        }

        // Calculate optimal width for each column
        int[] colWidths = new int[cols];

        for (int col = 0; col < cols; col++) {
            // Start with header width
            int headerWidth = $"Col{col}".Length;
            int maxDataWidth = 0;

            // Check width of each value in this column
            for (int row = 0; row < rows; row++) {
                double value = Values[row, col];
                string formattedValue = double.IsNaN(value) ? "NaN" : value.ToString($"F{numDigits}", CultureInfo.InvariantCulture);
                maxDataWidth = Math.Max(maxDataWidth, formattedValue.Length);
            }

            // Set column width to the maximum of header and data widths, with minimum padding
            colWidths[col] = Math.Max(headerWidth, maxDataWidth) + 2; // +2 for padding
        }

        // Calculate timestamp column width
        int timestampWidth = 20; // Default minimum
        if (Timestamps.Length > 0) {
            int maxTimestampWidth = Timestamps.Max(t => t.ToString().Length);
            timestampWidth = Math.Max(timestampWidth, maxTimestampWidth + 2);
        }

        // Print header with column indices
        Console.Write("Timestamp".PadRight(timestampWidth));
        for (int col = 0; col < cols; col++) {
            Console.Write($"Col{col}".PadLeft(colWidths[col]));
        }
        Console.WriteLine();

        // Print separator line
        Console.Write(new string('-', timestampWidth));
        for (int col = 0; col < cols; col++) {
            Console.Write(new string('-', colWidths[col]));
        }
        Console.WriteLine();

        // Print data rows
        for (int row = 0; row < rows; row++) {
            // Print timestamp
            Console.Write(Timestamps[row].ToString().PadRight(timestampWidth));

            // Print values for this row
            for (int col = 0; col < cols; col++) {
                double value = Values[row, col];
                string formattedValue = double.IsNaN(value) ? "NaN" : value.ToString($"F{numDigits}", CultureInfo.InvariantCulture);
                Console.Write(formattedValue.PadLeft(colWidths[col]));
            }
            Console.WriteLine();
        }
        Console.WriteLine("   ");
    }
}
