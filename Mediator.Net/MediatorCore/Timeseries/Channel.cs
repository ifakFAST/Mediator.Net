// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Ifak.Fast.Mediator.Timeseries
{
    public abstract class Channel {

        public abstract ChannelInfo Info { get; }

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
        /// An Exception is thrown, if any DataSet has timestamps before the last saved data set or
        /// if the timestamps do not monotonically increase.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="ignoreOldDataSets">if true, datasets older than the last dataset in the channel
        /// will be ignored, so that only the new datasets are appended</param>
        public abstract void Append(VTQ[] data, bool ignoreOldDataSets = false);

        /// <summary>
        /// Prepare an append function that will be executed later. This function shall return null on success,
        /// error message otherwise.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public abstract Func<PrepareContext, string> PrepareAppend(VTQ data);

        public abstract VTTQ? GetLatest();

        public abstract VTTQ? GetLatestTimestampDB(Timestamp startInclusive, Timestamp endInclusive);

        public abstract IList<VTTQ> ReadData(Timestamp startInclusive, Timestamp endInclusive, int maxValues, BoundingMethod bounding);

        public abstract long DeleteData(Timestamp startInclusive, Timestamp endInclusive);

        /// <summary>
        /// Deletes data according to their timestamps. Non.existing timestamps are ignored.
        /// </summary>
        /// <param name="timestamps"></param>
        /// <returns>Number of deleted items</returns>
        public abstract long DeleteData(Timestamp[] timestamps);

        public abstract long DeleteAll();

        public abstract long CountAll();

        public abstract long CountData(Timestamp startInclusive, Timestamp endInclusive);

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
        CompressToN
    }
}
