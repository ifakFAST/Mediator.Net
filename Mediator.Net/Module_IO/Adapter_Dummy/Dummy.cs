// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ifak.Fast.Mediator;

namespace Ifak.Fast.Mediator.IO.Adapter_Dummy
{
    [Identify("Dummy")]
    public class Dummy : AdapterBase
    {
        private readonly Dictionary<string, ValueSource> values = new Dictionary<string, ValueSource>();

        public override bool SupportsScheduledReading => true;

        public override Task<Group[]> Initialize(Adapter config, AdapterCallback callback, DataItemInfo[] itemInfos) {

            List<DataItem> dataItems = config.GetAllDataItems();

            var t = Timestamp.Now.TruncateMilliseconds();
            foreach (DataItem di in dataItems) {
                if (string.IsNullOrEmpty(di.Address))
                    values[di.ID] = new ValueStore(t, di);
                else
                    values[di.ID] = new Function(di);
            }

            return Task.FromResult(new Group[0]);
        }

        public override void StartRunning() {
            // nothing to do
        }

        public override Task<string[]> BrowseDataItemAddress(string idOrNull) {
            return Task.FromResult(new string[] { "Sin(period=5 min, amplitude=5, offset=11)" });
        }

        public override Task<string[]> BrowseAdapterAddress() {
            return Task.FromResult(new string[0]);
        }

        public override Task Shutdown() {
            values.Clear();
            return Task.FromResult(true);
        }

        public override Task<VTQ[]> ReadDataItems(string group, IList<ReadRequest> items, Duration? timeout) {

            int N = items.Count;
            VTQ[] res = new VTQ[N];

            for (int i = 0; i < N; ++i) {
                ReadRequest request = items[i];
                string id = request.ID;
                if (values.ContainsKey(id)) {
                    res[i] = values[id].Get();
                }
                else {
                    res[i] = new VTQ(Timestamp.Now, Quality.Bad, request.LastValue.V);
                }
            }

            return Task.FromResult(res);
        }

        public override Task<WriteDataItemsResult> WriteDataItems(string group, IList<DataItemValue> writeValues, Duration? timeout) {

            var failed = new List<FailedDataItemWrite>();

            for (int i = 0; i < writeValues.Count; ++i) {
                DataItemValue write = writeValues[i];
                string id = write.ID;
                if (values.ContainsKey(id)) {
                    values[id].Put(write.Value);
                }
                else {
                    failed.Add(new FailedDataItemWrite(id, $"No data item with id '{id}' found."));
                }
            }

            if (failed.Count == 0)
                return Task.FromResult(WriteDataItemsResult.OK);
            else
                return Task.FromResult(WriteDataItemsResult.Failure(failed.ToArray()));
        }

        abstract class ValueSource
        {
            public abstract VTQ Get();
            public abstract void Put(VTQ v);
        }

        class ValueStore : ValueSource
        {
            VTQ value;

            public ValueStore(Timestamp t, DataItem di) {
                value = new VTQ(t, Quality.Good, di.GetDefaultValue());
            }

            public override VTQ Get() => value;

            public override void Put(VTQ v) {
                value = v;
            }
        }

        class Function : ValueSource
        {
            string func;

            public Function(DataItem di) {
                func = di.Address;
            }

            private static Regex rgxSinus = new Regex(@"\s*Sin\s*\(\s*period\s*=\s*(\d+\s*(s|min|m|h|d))\s*\,\s*amplitude\s*=\s*(\d+\.?\d*)\s*\,\s*offset\s*=\s*(-?\d+\.?\d*)\)\s*", RegexOptions.IgnoreCase);
            private static long BaseDate = Timestamp.FromDateTime(new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc)).JavaTicks;

            public override VTQ Get() {
                Match match = rgxSinus.Match(func);
                if (match.Success) {
                    Duration period = Duration.Parse(match.Groups[1].Value);
                    double amplitude = double.Parse(match.Groups[3].Value);
                    double offset = double.Parse(match.Groups[4].Value);
                    double periodMS = period.TotalMilliseconds;
                    long now = Timestamp.Now.JavaTicks;
                    double x = (now - BaseDate) % periodMS;
                    double radian = (x / periodMS) * 2.0 * Math.PI;
                    double v = amplitude * Math.Sin(radian) + offset;
                    return new VTQ(Timestamp.Now.TruncateMilliseconds(), Quality.Good, DataValue.FromFloat((float)v));
                }
                else {
                    return new VTQ(Timestamp.Now.TruncateMilliseconds(), Quality.Bad, DataValue.FromFloat(0));
                }
            }

            public override void Put(VTQ v) {
                // ignore
            }
        }
    }
}
