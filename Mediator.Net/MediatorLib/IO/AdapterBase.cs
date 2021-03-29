// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO
{
    /// <summary>
    /// Each Adapter is run on its own thread. Therefore, if no data item groups are used,
    /// a simple blocking implementation is sufficient.
    /// </summary>
    public abstract class AdapterBase
    {
        /// <summary>
        /// If this property returns false then no calls to ReadDataItems will happen,
        /// i.e. changes to readable DataItems must be reported using AdapterCallback.Notify_DataItemsChanged.
        /// </summary>
        public abstract bool SupportsScheduledReading { get; }

        /// <summary>
        /// Called to initialize the adapter instance given its configuration.
        /// May return an empty array of groups, when there are no independent
        /// DataItems in the IO_Adapter configuration.
        /// </summary>
        /// <param name="config">The configuration for this adapter instance</param>
        /// <param name="callback">Used to notify DataItem changes or other events/alarms</param>
        /// <param name="itemInfos">Provides additional information about all DataItems with Read == true,
        /// e.g. the latest available value</param>
        public abstract Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos);

        /// <summary>
        /// Only relevant for 'active' adapters that perform background activities. Signals that the IO module
        /// is now up and running so that data notifications via AdapterCallback.Notify_DataItemsChanged may be send.
        /// The default implementation is a no op.
        /// </summary>
        public virtual void StartRunning() { }

        /// <summary>
        /// Called by the IO module to read data items of a specific group. If individual data item reads fail,
        /// this should be indicated by Quality.Bad in the corresponding VTQ entry of the returned array.
        /// Any exception is considered an error and leads to adapter restart.
        /// </summary>
        /// <param name="group">The group of this read request. Should be ignored when no groups are used.</param>
        /// <param name="items">The ids of the data items to read including the last value</param>
        /// <param name="timeout">An optional timeout</param>
        public abstract Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout);

        /// <summary>
        /// Called by the IO module to write data items of a specific group. If individual data item writes fail,
        /// this must be indicated by the WriteDataItemsResult return value.
        /// Any exception is considered an error and leads to adapter restart.
        /// </summary>
        /// <param name="group">The group of this write request. Should be ignored when no groups are used.</param>
        /// <param name="values">The data items to write</param>
        /// <param name="timeout">An optional timeout</param>
        public abstract Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> values, Duration? timeout);

        /// <summary>
        /// This method is called by the IO module to query possible values for the Address property of the Adapter.
        /// If Browsing is not supported for Adapter.Address, return a string array of length zero (new string[0]).
        /// </summary>
        public abstract Task<string[]> BrowseAdapterAddress();

        /// <summary>
        /// This method is called by the IO module to query possible values for the Address property of a DataItem.
        /// If Browsing is not supported for DataItem.Address, return a string array of length zero (new string[0]).
        /// </summary>
        /// <param name="idOrNull">The id of the DataItem for browsing the Address</param>
        /// <returns></returns>
        public abstract Task<string[]> BrowseDataItemAddress(string? idOrNull);

        /// <summary>
        /// Called by the IO module to shutdown this Adapter instance.
        /// </summary>
        public abstract Task Shutdown();
    }

    public struct DataItemInfo
    {
        public DataItemInfo(string id, VTQ? lastValue, bool scheduling) {
            ID = id;
            LatestValue = lastValue;
            ConfiguredForScheduling = scheduling;
        }

        public string ID { get; private set; }
        public VTQ? LatestValue { get; private set; }
        public bool ConfiguredForScheduling { get; private set; }
    }

    public interface AdapterCallback
    {
        void Notify_NeedRestart(string reason);
        void Notify_AlarmOrEvent(AdapterAlarmOrEvent eventInfo);
        void Notify_DataItemsChanged(DataItemValue[] values); // Only used for events
    }

    public class AdapterAlarmOrEvent
    {
        public Timestamp Time { get; set; } = Timestamp.Now;
        public Severity Severity { get; set; } = Severity.Info;

        /// <summary>
        /// Adapter specific category e.g. SensorFailure, ModuleRestart, CommunicationLoss
        /// </summary>
        public string Type { get; set; } = "";

        /// <summary>
        /// If true, indicates that a previous alarm of this type returned to normal (is not active anymore)
        /// </summary>
        public bool ReturnToNormal { get; set; } = false;

        /// <summary>
        /// Should contain all relevant information in one line of text
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// Optional additional information potentially in multiple lines of text (e.g. StackTrace)
        /// </summary>
        public string Details { get; set; } = "";

        /// <summary>
        /// Optional specification of the affected data item(s)
        /// </summary>
        public string[] AffectedDataItems { get; set; } = new string[0]; // optional, specifies which data items are affected

        public override string ToString() => Message;

        public static AdapterAlarmOrEvent Info(string type, string message, params string[] affectedDataItems) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Info,
                Type = type,
                Message = message,
                AffectedDataItems = affectedDataItems
            };
        }

        public static AdapterAlarmOrEvent Warning(string type, string message, params string[] affectedDataItems) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Warning,
                Type = type,
                Message = message,
                AffectedDataItems = affectedDataItems
            };
        }

        public static AdapterAlarmOrEvent Alarm(string type, string message, params string[] affectedDataItems) {
            return new AdapterAlarmOrEvent() {
                Severity = Severity.Alarm,
                Type = type,
                Message = message,
                AffectedDataItems = affectedDataItems
            };
        }
    }

    /// <summary>
    /// A group indicates a set of data items that originate from the same source,
    /// i.e. a communication problem while reading/writing one data item likely also
    /// affects reads/writes to other data items of the same group.
    /// </summary>
    public struct Group
    {
        public Group(string id, string[] dataItemIDs) {
            ID = id;
            DataItemIDs = dataItemIDs;
        }

        public string ID { get; private set; }
        public string[] DataItemIDs { get; private set; }
    }

    public struct ReadRequest
    {
        public ReadRequest(string id, VTQ lastValue) {
            ID = id;
            LastValue = lastValue;
        }

        public string ID { get; private set; }
        public VTQ LastValue { get; private set; }
    }

    public struct DataItemValue
    {
        public DataItemValue(string id, VTQ value) {
            ID = id;
            Value = value;
        }

        public string ID { get; private set; }
        public VTQ Value { get; private set; }
    }

    public struct WriteDataItemsResult
    {
        private WriteDataItemsResult(FailedDataItemWrite[]? failures) {
            FailedDataItems = failures;
        }

        public FailedDataItemWrite[]? FailedDataItems { get; set; }

        public bool IsOK() => FailedDataItems == null || FailedDataItems.Length == 0;

        public bool Failed() => !IsOK();

        public static WriteDataItemsResult OK => new WriteDataItemsResult(null);

        public static WriteDataItemsResult Failure(FailedDataItemWrite[] failures) => new WriteDataItemsResult(failures);

        public static WriteDataItemsResult FromResults(IEnumerable<WriteDataItemsResult> list) {
            if (list.All(r => r.IsOK())) return OK;
            return Failure(list.Where(r => r.Failed()).SelectMany(x => x.FailedDataItems).ToArray());
        }
    }

    public struct FailedDataItemWrite
    {
        public FailedDataItemWrite(string id, string error) {
            ID = id;
            Error = error;
        }
        public string ID { get; private set; }
        public string Error { get; private set; }
    }

}
