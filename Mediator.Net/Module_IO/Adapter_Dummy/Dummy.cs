// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.IO.Adapter_Dummy
{
    [Identify("Dummy")]
    public partial class Dummy : AdapterBase
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

        public override Task<string[]> BrowseDataItemAddress(string? idOrNull) {
            return Task.FromResult(new string[] { 
                "Sin(period=5 min, amplitude=5, offset=11)",
                "SinNoise(period=5 min, amplitude=5, offset=11, noise=1)",
                "3.1415"
            });
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

        partial class Function : ValueSource
        {
            readonly string func;
            readonly bool isJSON;

            public Function(DataItem di) {
                func = di.Address;
                isJSON = StdJson.IsValidJson(func);
            }

            private static readonly long BaseDate = Timestamp.FromDateTime(new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc)).JavaTicks;
            private readonly Random random = new();

            public override VTQ Get() {

                if (isJSON) {
                    return new VTQ(Timestamp.Now.TruncateMilliseconds(), Quality.Good, DataValue.FromJSON(func));
                }

                Match matchSinus = rgxSinus().Match(func);
                Match matchSinusNoise = rgxSinusNoise().Match(func);
                if (matchSinus.Success) {
                    Duration period = Duration.Parse(matchSinus.Groups[1].Value);
                    double amplitude = double.Parse(matchSinus.Groups[3].Value, CultureInfo.InvariantCulture);
                    double offset = double.Parse(matchSinus.Groups[4].Value, CultureInfo.InvariantCulture);
                    double periodMS = period.TotalMilliseconds;
                    long now = Timestamp.Now.JavaTicks;
                    double x = (now - BaseDate) % periodMS;
                    double radian = (x / periodMS) * 2.0 * Math.PI;
                    double v = amplitude * Math.Sin(radian) + offset;
                    return new VTQ(Timestamp.Now.TruncateMilliseconds(), Quality.Good, DataValue.FromFloat((float)v));
                }
                else if (matchSinusNoise.Success) {
                    Duration period = Duration.Parse(matchSinusNoise.Groups[1].Value);
                    double amplitude = double.Parse(matchSinusNoise.Groups[3].Value, CultureInfo.InvariantCulture);
                    double offset = double.Parse(matchSinusNoise.Groups[4].Value, CultureInfo.InvariantCulture);
                    double noiseStd = double.Parse(matchSinusNoise.Groups[5].Value, CultureInfo.InvariantCulture);
                    double periodMS = period.TotalMilliseconds;
                    long now = Timestamp.Now.JavaTicks;
                    double x = (now - BaseDate) % periodMS;
                    double radian = (x / periodMS) * 2.0 * Math.PI;
                    double signal = amplitude * Math.Sin(radian) + offset;
                    double noise = NextGaussian(random, 0.0, noiseStd);
                    double v = signal + noise;
                    return new VTQ(Timestamp.Now.TruncateMilliseconds(), Quality.Good, DataValue.FromFloat((float)v));
                }
                else {
                    return new VTQ(Timestamp.Now.TruncateMilliseconds(), Quality.Bad, DataValue.FromFloat(0));
                }
            }

            public static double NextGaussian(Random r, double mu = 0, double sigma = 1) {

                var u1 = 1.0 - r.NextDouble();
                var u2 = 1.0 - r.NextDouble();

                var rand_std_normal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                    Math.Sin(2.0 * Math.PI * u2);

                var rand_normal = mu + sigma * rand_std_normal;

                return rand_normal;
            }

            public override void Put(VTQ v) {
                // ignore
            }

            [GeneratedRegex("\\s*Sin\\s*\\(\\s*period\\s*=\\s*(\\d+\\s*(s|min|m|h|d))\\s*\\,\\s*amplitude\\s*=\\s*(\\d+\\.?\\d*)\\s*\\,\\s*offset\\s*=\\s*(-?\\d+\\.?\\d*)\\)\\s*", RegexOptions.IgnoreCase)]
            private static partial Regex rgxSinus();

            [GeneratedRegex("\\s*SinNoise\\s*\\(\\s*period\\s*=\\s*(\\d+\\s*(s|min|m|h|d))\\s*\\,\\s*amplitude\\s*=\\s*(\\d+\\.?\\d*)\\s*\\,\\s*offset\\s*=\\s*(-?\\d+\\.?\\d*)\\,\\s*noise\\s*=\\s*(-?\\d+\\.?\\d*)\\)\\s*", RegexOptions.IgnoreCase)]
            private static partial Regex rgxSinusNoise();
        }
    }
}
