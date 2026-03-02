using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Ifak.Fast.Mediator.Timeseries;

namespace Ifak.Fast.Mediator;

public static class CompressedTimeseriesReader
{
    /// <summary>
    /// Reads a compressed set of time series values from the specified channel within the given time range, applying
    /// the specified quality filter and limiting the number of returned values (preserving representative values such as minimum, maximum, and median).
    /// </summary>
    /// <param name="channel">The channel from which to read time series data.</param>
    /// <param name="startInclusive">The start of the time range, inclusive. Only values with timestamps greater than or equal to this value are
    /// included.</param>
    /// <param name="endInclusive">The end of the time range, inclusive. Only values with timestamps less than or equal to this value are included.</param>
    /// <param name="maxValues">The maximum number of values to return. Must be greater than zero.</param>
    /// <param name="filter">The quality filter to apply when selecting values from the channel.</param>
    /// <returns>A list of compressed time series values (VTTQ) that match the specified criteria. The list contains at most the
    /// specified maximum number of values and may be empty if no values match.</returns>
    public static List<VTTQ> ReadCompressed(Channel channel,
                                            Timestamp startInclusive,
                                            Timestamp endInclusive,
                                            int maxValues,
                                            Timeseries.QualityFilter filter) {
        if (maxValues <= 0) {
            throw new ArgumentOutOfRangeException(nameof(maxValues), "maxValues must be greater than zero.");
        }
        if (startInclusive > endInclusive || maxValues == 0) {
            return [];
        }
        BufferedChannelReader reader = new(channel, startInclusive, endInclusive, filter);
        //int countNumeric = CountNumericValuesInInterval(channel, startInclusive, endInclusive, filter);
        List<VTTQ> res = ReadCompressed(reader.AllValues() /*, countNumeric*/, maxValues);
        return res;
    }

    /// <summary>
    /// Reads a compressed set of time series values using the cache for complete UTC days.
    /// For complete days within the range, cached compressed values are used (or computed and cached if missing).
    /// Partial intervals at the start and end are read directly from the channel.
    /// </summary>
    /// <param name="channel">The channel from which to read time series data.</param>
    /// <param name="variable">The variable reference for cache lookup.</param>
    /// <param name="startInclusive">The start of the time range, inclusive.</param>
    /// <param name="endInclusive">The end of the time range, inclusive.</param>
    /// <param name="maxValues">The maximum number of values to return.</param>
    /// <param name="filter">The quality filter to apply.</param>
    /// <param name="cache">The aggregation cache to use for storing/retrieving compressed day values.</param>
    /// <returns>A list of compressed time series values (VTTQ).</returns>
    public static List<VTTQ> ReadCompressedWithCache(Channel channel,
                                                     VariableRef variable,
                                                     Timestamp startInclusive,
                                                     Timestamp endInclusive,
                                                     int maxValues,
                                                     Timeseries.QualityFilter filter,
                                                     HistoryAggregationCache cache) {

        List<VTTQ> first = channel.ReadData(Timestamp.Empty, Timestamp.Max, 1, Timeseries.BoundingMethod.TakeFirstN, filter);
        if (first.Count == 0) {
            return [];
        }
        if (startInclusive < first[0].T) {
            startInclusive = first[0].T;
        }

        VTTQ? latest = channel.GetLatest();
        if (!latest.HasValue) {
            return [];
        }
        if (endInclusive > latest.Value.T) {
            endInclusive = latest.Value.T;
        }

        if (startInclusive > endInclusive) {
            return [];
        }

        Duration diff = endInclusive - startInclusive;
        double relDay = diff.TotalSeconds / Duration.FromDays(1).TotalSeconds;
        int estimatedDataPointsInCache = (int)(relDay * HistoryAggregationCache.CompressedValuesPerDay);
        bool useCache = 0.8 * maxValues < estimatedDataPointsInCache;
        if (!useCache) {
            return ReadCompressed(channel, startInclusive, endInclusive, maxValues, filter);
        }

        Duration totalLenDay = Duration.FromDays(1);

        // Convert endInclusive to exclusive for GetCompleteDaysInRange
        Timestamp endExclusive = endInclusive.AddMillis(1);
        List<Timestamp> completeDays = HistoryAggregationCache.GetCompleteDaysInRange(startInclusive, endExclusive);

        // If no complete days, fall back to direct read
        if (completeDays.Count == 0) {
            return ReadCompressed(channel, startInclusive, endInclusive, maxValues, filter);
        }

        QualityFilter mediatorFilter = MapToMediatorFilter(filter);
        List<VTTQ> allValues = [];

        Timestamp firstCompleteDayStart = completeDays[0];
        Timestamp lastCompleteDayEnd = completeDays[completeDays.Count - 1].AddDays(1);

        // Read partial interval at start (startInclusive to firstCompleteDayStart)
        if (startInclusive < firstCompleteDayStart) {
            Timestamp partialEnd = firstCompleteDayStart.AddMillis(-1);
            Duration len = (partialEnd - startInclusive) + Duration.FromMilliseconds(1);
            double rel = ((double)len.TotalSeconds) / ((double)totalLenDay.TotalSeconds);
            int maxValuesRelative = (int)Math.Ceiling(rel * HistoryAggregationCache.CompressedValuesPerDay);
            List<VTTQ> partialStart = ReadCompressed(channel, startInclusive, partialEnd, maxValuesRelative, filter);
            allValues.AddRange(partialStart);
        }

        // Get cached compressed values for each complete day
        foreach (Timestamp day in completeDays) {
            List<VTTQ> dayValues = GetOrComputeDayCompressed(channel, variable, day, filter, mediatorFilter, cache);
            allValues.AddRange(dayValues);
        }

        // Read partial interval at end (lastCompleteDayEnd to endInclusive)
        if (lastCompleteDayEnd <= endInclusive) {
            Duration len = (endInclusive - lastCompleteDayEnd) + Duration.FromMilliseconds(1);
            double rel = ((double)len.TotalSeconds) / ((double)totalLenDay.TotalSeconds);
            int maxValuesRelative = (int)Math.Ceiling(rel * HistoryAggregationCache.CompressedValuesPerDay);
            List <VTTQ> partialEnd = ReadCompressed(channel, lastCompleteDayEnd, endInclusive, maxValuesRelative, filter);
            allValues.AddRange(partialEnd);
        }

        // Re-compress all values to requested maxValues
        if (allValues.Count <= maxValues) {
            return allValues;
        }

        return ReadCompressed(allValues, allValues.Count, maxValues);
    }

    private static List<VTTQ> GetOrComputeDayCompressed(Channel channel,
                                                        VariableRef variable,
                                                        Timestamp dayStart,
                                                        Timeseries.QualityFilter filter,
                                                        QualityFilter mediatorFilter,
                                                        HistoryAggregationCache cache) {

        // Try to get from cache
        if (cache.TryGetCompressed(variable, mediatorFilter, dayStart, out List<VTTQ>? cached)) {
            return cached ?? [];
        }

        // Compute and cache
        Timestamp dayEnd = dayStart.AddDays(1).AddMillis(-1);
        List<VTTQ> dayValues = ReadCompressed(channel, dayStart, dayEnd, HistoryAggregationCache.CompressedValuesPerDay, filter);
        cache.SetCompressed(variable, mediatorFilter, dayStart, dayValues);

        return dayValues;
    }

    private static QualityFilter MapToMediatorFilter(Timeseries.QualityFilter filter) {
        return filter switch {
            Timeseries.QualityFilter.ExcludeNone => QualityFilter.ExcludeNone,
            Timeseries.QualityFilter.ExcludeBad => QualityFilter.ExcludeBad,
            _ => QualityFilter.ExcludeNone
        };
    }

    //private static int CountNumericValuesInInterval(Channel channel,
    //                                                Timestamp startInclusive,
    //                                                Timestamp endInclusive,
    //                                                Timeseries.QualityFilter filter) {

    //    List<VTQ> count = channel.ReadAggregatedIntervals(
    //        intervalBounds: [startInclusive, endInclusive.AddMillis(1)],
    //        aggregation: Aggregation.Count,
    //        filter: filter
    //    );

    //    return count[0].V.GetInt();
    //}

    /// <summary>
    /// Returns a compressed list of VTTQ values from the specified collection, limited to the specified maximum
    /// number of values.
    /// </summary>
    /// <remarks>
    /// Values with empty payloads and values that are literal <c>NaN</c>/<c>Infinity</c>/<c>-Infinity</c> are excluded.
    /// Remaining values are returned as-is when their count is less than or equal to <paramref name="maxValues"/>;
    /// otherwise, a compressed representative subset is returned.
    /// The method uses an internal pooled buffer to reduce allocations for large input sequences.
    /// </remarks>
    /// <param name="values">The collection of VTTQ values to process.</param>
    /// <param name="maxValues">The maximum number of values to include in the returned list. Must be greater than zero.</param>
    /// <returns>
    /// A list of VTTQ values containing up to <paramref name="maxValues"/> entries after filtering.
    /// </returns>
    public static List<VTTQ> ReadCompressed(IEnumerable<VTTQ> values, int maxValues) {

        VTTQ[] buffer = ArrayPool<VTTQ>.Shared.Rent(30*1440);
        try {
            int idx = 0;
            foreach (VTTQ vttq in values) {
                if (vttq.V.IsEmpty || vttq.V.IsInfinityOrNaN) continue;
                if (idx >= buffer.Length) {
                    VTTQ[] bufferNew = ArrayPool<VTTQ>.Shared.Rent(buffer.Length * 2);
                    Array.Copy(buffer, bufferNew, buffer.Length);
                    ArrayPool<VTTQ>.Shared.Return(buffer);
                    buffer = bufferNew;
                }
                buffer[idx++] = vttq;
            }

            int count = idx; // actual number of items

            ArraySegment<VTTQ> filteredValues = new(buffer, 0, count);

            if (count <= maxValues) { // No compression needed, read all values
                var res = new List<VTTQ>(count);
                res.AddRange(filteredValues);
                return res;
            }
            else {
                return ReadCompressed(filteredValues, count, maxValues);
            }
        }
        finally {
            ArrayPool<VTTQ>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Compresses a sequence of VTTQ values to a target maximum number of samples.
    /// </summary>
    /// <remarks>
    /// Values with empty payloads and values that are literal <c>NaN</c>/<c>Infinity</c>/<c>-Infinity</c> are ignored.
    /// For numeric intervals, representative values are selected using min/median/max by value (then ordered by time).
    /// For intervals containing only non-numeric values, only the last value of that interval is kept.
    /// </remarks>
    /// <param name="values">The sequence of VTTQ values to compress.</param>
    /// <param name="count">
    /// The caller-provided item count used to size compression intervals; typically the number of input items after basic filtering.
    /// Must be greater than or equal to zero.
    /// </param>
    /// <param name="maxValues">The target maximum number of values in the compressed result. Must be greater than zero.</param>
    /// <returns>A compressed list of representative VTTQ values derived from <paramref name="values"/>.</returns>
    public static List<VTTQ> ReadCompressed(IEnumerable<VTTQ> values, int count, int maxValues) {

        List<VTTQ> res = new(maxValues);

        if (count <= maxValues) {
            // No compression needed, read all values
            foreach (VTTQ vttq in values) {
                if (vttq.V.IsEmpty || vttq.V.IsInfinityOrNaN) continue;
                res.Add(vttq);
            }
            return res;
        }

        // count > maxValues => compression needed:

        int maxIntervals = maxValues / 3; // Retain 3 values per interval (min, max, median)
        int itemsPerInterval = (maxValues < 6) ? (int)count : (int)Math.Ceiling(((double)count) / maxIntervals);

        List<VTTQ_D> buffer = new(itemsPerInterval);
        bool anyNumericInBuffer = false;
        bool anyNonNumericInBuffer = false;

        void FlushBuffer() {

            if (anyNumericInBuffer && anyNonNumericInBuffer) { 
                // remove NaN values:
                buffer.RemoveAll(x => double.IsNaN(x.D));
                anyNonNumericInBuffer = false; // after removing NaN, we have no non-numeric in buffer
            }

            if (anyNonNumericInBuffer) { // => all non-numeric; can not sort, only add the last item:
                res.Add(buffer.Last().V);
            }
            else { // => all numeric; sort and add min, median, max:

                int N = buffer.Count;
                if (N > 3) {
                    buffer.Sort(CompareVTTQs);
                    if (maxValues >= 3) {
                        VTTQ a = buffer[0].V;
                        VTTQ b = buffer[N / 2].V;
                        VTTQ c = buffer[N - 1].V;
                        AddByTime(res, a, b, c);
                    }
                    else {
                        res.Add(buffer[N / 2].V);
                    }
                }
                else {
                    res.AddRange(buffer.Select(y => y.V));
                }
            }

            buffer.Clear();
            anyNumericInBuffer = false;
            anyNonNumericInBuffer = false;
        }

        foreach (VTTQ vttq in values) {
            if (vttq.V.IsEmpty || vttq.V.IsInfinityOrNaN) continue;
            double? d = vttq.V.AsDouble();
            anyNumericInBuffer    |=  d.HasValue;
            anyNonNumericInBuffer |= !d.HasValue;
            double v = d ?? double.NaN;
            buffer.Add(new VTTQ_D(vttq, v));
            if (buffer.Count >= itemsPerInterval) {
                FlushBuffer();
            }
        }

        if (buffer.Count > 0) {
            FlushBuffer();
        }

        return res;
    }

    struct VTTQ_D
    {
        internal VTTQ V;
        internal double D;

        internal VTTQ_D(VTTQ x, double d) {
            V = x;
            D = d;
        }
    }

    private static int CompareVTTQs(VTTQ_D a, VTTQ_D b) => a.D.CompareTo(b.D);

    private static void AddByTime(List<VTTQ> result, VTTQ a, VTTQ b, VTTQ c) {
        if (a.T < b.T && a.T < c.T) {
            result.Add(a);
            if (b.T < c.T) {
                result.Add(b);
                result.Add(c);
            }
            else {
                result.Add(c);
                result.Add(b);
            }
        }
        else if (b.T < a.T && b.T < c.T) {
            result.Add(b);
            if (a.T < c.T) {
                result.Add(a);
                result.Add(c);
            }
            else {
                result.Add(c);
                result.Add(a);
            }
        }
        else {
            result.Add(c);
            if (a.T < b.T) {
                result.Add(a);
                result.Add(b);
            }
            else {
                result.Add(b);
                result.Add(a);
            }
        }
    }

    private sealed class BufferedChannelReader(Channel channel, Timestamp startInclusive, Timestamp endInclusive, Timeseries.QualityFilter filter)
    {
        private readonly Channel channel = channel;
        private readonly Timestamp startInclusive = startInclusive;
        private readonly Timestamp endInclusive = endInclusive;
        private readonly Timeseries.QualityFilter filter = filter;
        private List<VTTQ> buffer = [];
        private int index = 0;

        private VTTQ? ReadNext() {
            if (index >= buffer.Count) {
                index = 0;
                Timestamp tStart = (buffer.Count > 0) ? buffer[^1].T.AddMillis(1) : startInclusive;
                buffer = channel.ReadData(tStart, endInclusive, 9000, Timeseries.BoundingMethod.TakeFirstN, filter);
                if (buffer.Count == 0) {
                    return null;
                }
            }
            return buffer[index++];
        }

        public IEnumerable<VTTQ> AllValues() {
            while (true) {
                VTTQ? vttq = ReadNext();
                if (!vttq.HasValue) {
                    yield break;
                }
                yield return vttq.Value;
            }
        }
    }
}
