// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Ifak.Fast.Mediator.Timeseries
{
    public abstract class Channel {

        public abstract ChannelRef Ref { get; }

        /// <summary>
        /// update of existing data sets. if at least one data set does not exist
        /// for the key(measured time), then an exception is thrown. This check
        /// is done before any data is written.
        /// </summary>
        /// <param name="data"></param>
        public abstract void Update(VTQ[] data);

        /// <summary>
        /// An exception is thrown if any of the time stamps of the DataSets already exists.
        /// This check is performed before any writing of data.
        /// </summary>
        /// <param name="data"></param>
        public abstract void Insert(VTQ[] data);


        /// <summary>
        /// Insert or update data.
        /// </summary>
        /// <param name="data"></param>
        public abstract void Upsert(VTQ[] data);

        /// <summary>
        /// Replace all channel data.
        /// </summary>
        /// <param name="data"></param>
        public abstract void ReplaceAll(VTQ[] data);

        /// <summary>
        /// An Exception is thrown, if any DataSet has timestamps before the last saved data set or
        /// if the timestamps do not monotonically increase.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="ignoreOldDataSets">if true, datasets older than the last dataset in the channel
        /// will be ignored, so that only the new datasets are appended</param>
        public virtual void Append(VTQ[] data, bool ignoreOldDataSets = false) {

            if (data.Length == 0) return;

            CheckIncreasingTimestamps(data);

            VTTQ? lastItem = GetLatest();

            if (lastItem.HasValue && data[0].T <= lastItem.Value.T) {

                if (ignoreOldDataSets) {
                    Timestamp t = lastItem.Value.T;
                    VTQ[] filtered = data.Where(x => x.T > t).ToArray();
                    Insert(filtered);
                }
                else {
                    throw new Exception("Timestamp is smaller or equal than last dataset timestamp in channel DB!\n\tLastItem in Database: " + lastItem.Value.ToString() + "\n\tFirstItem to Append:  " + data[0].ToString());
                }
            }
            else {
                Insert(data);
            }
        }

        protected static void CheckIncreasingTimestamps(VTQ[] data) {
            Timestamp tPrev = Timestamp.Empty;
            for (int i = 0; i < data.Length; ++i) {
                Timestamp t = data[i].T;
                if (t <= tPrev) throw new Exception("Dataset timestamps are not monotonically increasing!");
                tPrev = t;
            }
        }

        /// <summary>
        /// Prepare an append function that will be executed later. This function shall return null on success,
        /// error message otherwise.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="allowOutOfOrder"></param>
        /// <returns></returns>
        public abstract Func<PrepareContext, string?> PrepareAppend(VTQ data, bool allowOutOfOrder);

        public abstract Timestamp? GetOldestTimestamp();

        public abstract VTTQ? GetLatest();

        public abstract VTTQ? GetLatestTimestampDB(Timestamp startInclusive, Timestamp endInclusive);

        public abstract List<VTTQ> ReadData(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding, QualityFilter filter);

        public abstract List<VTQ> ReadAggregatedIntervals(Timestamp[] intervalBounds, Aggregation aggregation, QualityFilter filter);

        public abstract long DeleteData(Timestamp startInclusive, Timestamp endInclusive);

        /// <summary>
        /// Deletes data according to their timestamps. Non.existing timestamps are ignored.
        /// </summary>
        /// <param name="timestamps"></param>
        /// <returns>Number of deleted items</returns>
        public abstract long DeleteData(Timestamp[] timestamps);

        public abstract long DeleteAll();

        public virtual void Truncate() {
            DeleteAll();
        }

        public abstract long CountAll();

        public abstract long CountData(Timestamp startInclusive, Timestamp endInclusive, QualityFilter filter);

    }

    public class PrepareContext
    {
        public readonly Timestamp TimeDB;

        public PrepareContext(Timestamp timeDB) {
            TimeDB = timeDB;
        }
    }

    public enum BoundingMethod
    {
        TakeFirstN,
        TakeLastN,
        //CompressToN
    }

    public enum QualityFilter
    {
        ExcludeNone,
        ExcludeBad,
        ExcludeNonGood,
    }

    internal sealed class QualityFilterHelper
    {
        private readonly bool IncludeAll;
        private readonly bool IncludeUncertain;

        private QualityFilterHelper(QualityFilter filter) {
            IncludeAll = filter == QualityFilter.ExcludeNone;
            IncludeUncertain = filter != QualityFilter.ExcludeNonGood;
        }

        internal static QualityFilterHelper Make(QualityFilter filter) {
            return new QualityFilterHelper(filter);
        }

        internal bool Include(Quality q) {
            return q == Quality.Good || IncludeAll || (IncludeUncertain && q == Quality.Uncertain);
        }
    }
}
