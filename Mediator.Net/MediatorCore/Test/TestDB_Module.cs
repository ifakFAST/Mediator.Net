// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;
using VTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTQ>;

namespace Ifak.Fast.Mediator.Test
{
    public class TestDB_Module : ModuleBase
    {
        private Logger log = LogManager.GetLogger("TestDB");

        private Connection? con = null;
        private Notifier? notifier = null;
        private string moduleID = "";
        private const int VariablesCount = 200;

        public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {
            this.notifier = notifier;
            con = await HttpConnection.ConnectWithModuleLogin(info, listener: null, timeoutSeconds: 10 * 60);
            moduleID = info.ModuleID;
            log = LogManager.GetLogger(info.ModuleID);
        }

        public override Task<ObjectInfo[]> GetAllObjects() {
            var variables = new Variable[VariablesCount];
            for (int i = 0; i < VariablesCount; ++i) {
                variables[i] = new Variable("Variable_" + i.ToString(), DataType.Float64, DataValue.FromDouble(0), History.Complete);
            }
            var res = new ObjectInfo[] {
                new ObjectInfo(ObjectRef.Make(moduleID, "obj"), "Obj", "Test", "Test", null, variables)
            };
            return Task.FromResult(res);
        }

        public override async Task Run(Func<bool> shutdown) {

            TimeCode timer = new TimeCode(log);

            try {
                timer.Print("Completed Init");
                await TestHistorianModify();
                timer.Print("Completed TestHistorianModify");
                await TestHistorianManyVariables();
                timer.Print("Completed TestHistorianManyVariables");
                await TestHistorianAggregatedIntervals();
                timer.Print("Completed TestHistorianAggregatedIntervals");
                log.Info("Tests completed successfully");
            }
            catch (Exception exp) {
                log.Error(exp, exp.Message);
            }

            while (!shutdown()) {
                await Task.Delay(100);
            }
        }

        private async Task TestHistorianModify() {

            if (con == null) throw new Exception("con == null");
            log.Info($"TestHistorianModify");

            var varA = GetVarRef("Variable_0");

            await ExpectCount(varA, 0);

            VTQ vtq1 = new VTQ(Timestamp.Now, Quality.Good, DataValue.FromDouble(3.14));
            await con.HistorianModify(varA, ModifyMode.Insert, vtq1);
            await ExpectCount(varA, 1);
            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 10, vtq1);

            await ExpectException(() => {
                return con.HistorianModify(varA, ModifyMode.Insert, vtq1);
            });

            VTQ vtq2 = new VTQ(vtq1.T, Quality.Good, DataValue.FromDouble(100));
            await con.HistorianModify(varA, ModifyMode.Update, vtq2);

            await ExpectCount(varA, 1);
            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 10, vtq2);

            VTQ vtq3 = new VTQ(vtq1.T, Quality.Uncertain, DataValue.FromDouble(1000));
            await con.HistorianModify(varA, ModifyMode.Upsert, vtq3);
            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 10, vtq3);

            VTQ vtq4 = new VTQ(vtq1.T.AddMillis(1), Quality.Good, DataValue.FromDouble(1001));
            await con.HistorianModify(varA, ModifyMode.Upsert, vtq4);

            await ExpectCount(varA, 2);
            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 10, vtq3, vtq4);

            VTQ vtq5 = new VTQ(vtq1.T.AddMillis(2), Quality.Good, DataValue.FromDouble(777));
            await ExpectException(() => {
                return con.HistorianModify(varA, ModifyMode.Insert, vtq5, vtq3);
            });

            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 10, vtq3, vtq4);

            await con.HistorianModify(varA, ModifyMode.Delete, vtq3);
            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 10, vtq4);
            await con.HistorianModify(varA, ModifyMode.Delete, vtq4);
            await ExpectCount(varA, 0);

            await con.HistorianModify(varA, ModifyMode.Delete, vtq4); // no exception when not found

            await con.HistorianModify(varA, ModifyMode.Insert, vtq1);
            await con.HistorianModify(varA, ModifyMode.ReplaceAll, vtq3, vtq4);
            await TestHistoryRaw(varA, Timestamp.Empty, Timestamp.Max, 100, vtq3, vtq4);
        }

        private async Task TestHistorianManyVariables() {

            if (con == null) throw new Exception("con == null");

            TimeCode timer = new TimeCode(log);

            Timestamp tStart = Timestamp.Now;
            Timestamp last = tStart;
            const int N = 10;

            for (int n = 0; n < N; ++n) {

                Timestamp t = Timestamp.Now;
                while (t <= last) {
                    await Task.Delay(1);
                    t = Timestamp.Now;
                }

                var values = new List<VariableValue>(VariablesCount);
                for (int i = 0; i < VariablesCount; ++i) {
                    VTQ vtq = new VTQ(t, Quality.Good, DataValue.FromDouble(i));
                    values.Add(new VariableValue(GetVarRef("Variable_" + i.ToString()), vtq));
                }
                notifier!.Notify_VariableValuesChanged(values);
                last = t;
            }

            timer.Print("First Loop");

            for (int i = 0; i < 60; ++i) {
                long c = await con.HistorianCount(GetVarRef("Variable_0"), tStart, Timestamp.Max);
                if (c < N) {
                    log.Info($"Delay {c} < {N}");
                    await Task.Delay(1000);
                }
            }

            timer.Print("Second Loop");

            for (int i = 0; i < VariablesCount; ++i) {
                var v = GetVarRef("Variable_" + i.ToString());
                long c = await con.HistorianCount(v, tStart, Timestamp.Max);
                assert(c == N, $"{v} count = {c} != {N}");
            }

            timer.Print("Third Loop");

            var vi = GetVarRef("Variable_0");

            VTQ vtq1 = new VTQ(Timestamp.Now, Quality.Bad, DataValue.FromDouble(103.14));
            await con.HistorianModify(vi, ModifyMode.Insert, vtq1);

            long countNoBad = await con.HistorianCount(vi, Timestamp.Empty, Timestamp.Max, QualityFilter.ExcludeBad);
            long countNonGood = await con.HistorianCount(vi, Timestamp.Empty, Timestamp.Max, QualityFilter.ExcludeNonGood);
            long countNone = await con.HistorianCount(vi, Timestamp.Empty, Timestamp.Max, QualityFilter.ExcludeNone);

            assert(countNoBad == 12, $"countNoBad = {countNoBad} != 12");
            assert(countNonGood == 11, $"countNonGood = {countNonGood} != 11");
            assert(countNone == 13, $"countNone = {countNone} != 13");

            timer.Print("Count test");
            // log.Info($"Not Bad: {cNoBad} Only Good: {cNonGood} All: {cNone}");
        }

        private async Task TestHistorianAggregatedIntervals() {

            if (con == null) throw new Exception("con == null");
            log.Info($"TestHistorianAggregatedIntervals");

            // Use a fresh variable to avoid interference from previous tests
            var v = GetVarRef("Variable_199");

            // Clear any existing data
            await con.HistorianModify(v, ModifyMode.ReplaceAll);

            // Create test data with known values: 10, 20, 30, 40, 50
            Timestamp tBase = Timestamp.FromISO8601("2024-01-01T00:00:00Z");
            VTQ vtq1 = new VTQ(tBase.AddMillis(100), Quality.Good, DataValue.FromDouble(10));
            VTQ vtq2 = new VTQ(tBase.AddMillis(200), Quality.Good, DataValue.FromDouble(20));
            VTQ vtq3 = new VTQ(tBase.AddMillis(300), Quality.Good, DataValue.FromDouble(30));
            VTQ vtq4 = new VTQ(tBase.AddMillis(400), Quality.Good, DataValue.FromDouble(40));
            VTQ vtq5 = new VTQ(tBase.AddMillis(500), Quality.Good, DataValue.FromDouble(50));

            await con.HistorianModify(v, ModifyMode.Insert, vtq1, vtq2, vtq3, vtq4, vtq5);

            // Define interval bounds that cover all data points
            Timestamp[] bounds = new Timestamp[] { tBase, tBase.AddMillis(600) };

            // Test 1: All aggregation types with single interval
            log.Info("Testing all aggregation types...");

            VTQs avgResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.Average);
            assert(avgResult.Count == 1, "Average result count == 1");
            assert(avgResult[0].Q == Quality.Good, "Average quality is Good");
            assert(avgResult[0].T == tBase, "Average timestamp equals interval start");
            assert(Math.Abs(avgResult[0].V.AsDouble()!.Value - 30.0) < 0.001, $"Average == 30, got {avgResult[0].V.AsDouble()}");

            VTQs minResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.Min);
            assert(minResult.Count == 1, "Min result count == 1");
            assert(Math.Abs(minResult[0].V.AsDouble()!.Value - 10.0) < 0.001, $"Min == 10, got {minResult[0].V.AsDouble()}");

            VTQs maxResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.Max);
            assert(maxResult.Count == 1, "Max result count == 1");
            assert(Math.Abs(maxResult[0].V.AsDouble()!.Value - 50.0) < 0.001, $"Max == 50, got {maxResult[0].V.AsDouble()}");

            VTQs countResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.Count);
            assert(countResult.Count == 1, "Count result count == 1");
            assert(Math.Abs(countResult[0].V.AsDouble()!.Value - 5.0) < 0.001, $"Count == 5, got {countResult[0].V.AsDouble()}");

            VTQs sumResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.Sum);
            assert(sumResult.Count == 1, "Sum result count == 1");
            assert(Math.Abs(sumResult[0].V.AsDouble()!.Value - 150.0) < 0.001, $"Sum == 150, got {sumResult[0].V.AsDouble()}");

            VTQs firstResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.First);
            assert(firstResult.Count == 1, "First result count == 1");
            assert(Math.Abs(firstResult[0].V.AsDouble()!.Value - 10.0) < 0.001, $"First == 10, got {firstResult[0].V.AsDouble()}");

            VTQs lastResult = await con.HistorianReadAggregatedIntervals(v, bounds, Aggregation.Last);
            assert(lastResult.Count == 1, "Last result count == 1");
            assert(Math.Abs(lastResult[0].V.AsDouble()!.Value - 50.0) < 0.001, $"Last == 50, got {lastResult[0].V.AsDouble()}");

            // Test 2: Multiple intervals
            log.Info("Testing multiple intervals...");

            // Interval 1: [tBase, tBase+250) contains vtq1(10), vtq2(20) -> Avg=15, Count=2
            // Interval 2: [tBase+250, tBase+450) contains vtq3(30), vtq4(40) -> Avg=35, Count=2
            // Interval 3: [tBase+450, tBase+600) contains vtq5(50) -> Avg=50, Count=1
            Timestamp[] multiBounds = new Timestamp[] {
                tBase,
                tBase.AddMillis(250),
                tBase.AddMillis(450),
                tBase.AddMillis(600)
            };

            VTQs multiAvg = await con.HistorianReadAggregatedIntervals(v, multiBounds, Aggregation.Average);
            assert(multiAvg.Count == 3, $"Multiple intervals: result count == 3, got {multiAvg.Count}");
            assert(Math.Abs(multiAvg[0].V.AsDouble()!.Value - 15.0) < 0.001, $"Interval 1 Avg == 15, got {multiAvg[0].V.AsDouble()}");
            assert(Math.Abs(multiAvg[1].V.AsDouble()!.Value - 35.0) < 0.001, $"Interval 2 Avg == 35, got {multiAvg[1].V.AsDouble()}");
            assert(Math.Abs(multiAvg[2].V.AsDouble()!.Value - 50.0) < 0.001, $"Interval 3 Avg == 50, got {multiAvg[2].V.AsDouble()}");

            // Verify timestamps match interval starts
            assert(multiAvg[0].T == tBase, "Interval 1 timestamp == tBase");
            assert(multiAvg[1].T == tBase.AddMillis(250), "Interval 2 timestamp == tBase+250");
            assert(multiAvg[2].T == tBase.AddMillis(450), "Interval 3 timestamp == tBase+450");

            VTQs multiCount = await con.HistorianReadAggregatedIntervals(v, multiBounds, Aggregation.Count);
            assert(Math.Abs(multiCount[0].V.AsDouble()!.Value - 2.0) < 0.001, $"Interval 1 Count == 2, got {multiCount[0].V.AsDouble()}");
            assert(Math.Abs(multiCount[1].V.AsDouble()!.Value - 2.0) < 0.001, $"Interval 2 Count == 2, got {multiCount[1].V.AsDouble()}");
            assert(Math.Abs(multiCount[2].V.AsDouble()!.Value - 1.0) < 0.001, $"Interval 3 Count == 1, got {multiCount[2].V.AsDouble()}");

            // Test 3: Empty interval
            log.Info("Testing empty interval...");

            Timestamp[] emptyBounds = new Timestamp[] {
                tBase.AddMillis(700),
                tBase.AddMillis(800)
            };

            VTQs emptyCount = await con.HistorianReadAggregatedIntervals(v, emptyBounds, Aggregation.Count);
            assert(emptyCount.Count == 1, "Empty interval: result count == 1");
            assert(Math.Abs(emptyCount[0].V.AsDouble()!.Value - 0.0) < 0.001, $"Empty interval Count == 0, got {emptyCount[0].V.AsDouble()}");

            VTQs emptyAvg = await con.HistorianReadAggregatedIntervals(v, emptyBounds, Aggregation.Average);
            assert(emptyAvg.Count == 1, "Empty interval Average: result count == 1");
            assert(emptyAvg[0].V.IsEmpty, $"Empty interval Average returns Empty value");

            VTQs emptyMin = await con.HistorianReadAggregatedIntervals(v, emptyBounds, Aggregation.Min);
            assert(emptyMin[0].V.IsEmpty, "Empty interval Min returns Empty value");

            VTQs emptyMax = await con.HistorianReadAggregatedIntervals(v, emptyBounds, Aggregation.Max);
            assert(emptyMax[0].V.IsEmpty, "Empty interval Max returns Empty value");

            // Test 4: Quality filters
            log.Info("Testing quality filters...");

            // Add data with different qualities
            var vQual = GetVarRef("Variable_198");
            await con.HistorianModify(vQual, ModifyMode.ReplaceAll);

            VTQ vtqGood1 = new VTQ(tBase.AddMillis(100), Quality.Good, DataValue.FromDouble(100));
            VTQ vtqBad = new VTQ(tBase.AddMillis(200), Quality.Bad, DataValue.FromDouble(200));
            VTQ vtqUncertain = new VTQ(tBase.AddMillis(300), Quality.Uncertain, DataValue.FromDouble(300));
            VTQ vtqGood2 = new VTQ(tBase.AddMillis(400), Quality.Good, DataValue.FromDouble(400));

            await con.HistorianModify(vQual, ModifyMode.Insert, vtqGood1, vtqBad, vtqUncertain, vtqGood2);

            Timestamp[] qualBounds = new Timestamp[] { tBase, tBase.AddMillis(500) };

            // ExcludeNone: All 4 values included -> Count=4, Sum=1000, Avg=250
            VTQs filterNone = await con.HistorianReadAggregatedIntervals(vQual, qualBounds, Aggregation.Count, QualityFilter.ExcludeNone);
            assert(Math.Abs(filterNone[0].V.AsDouble()!.Value - 4.0) < 0.001, $"ExcludeNone Count == 4, got {filterNone[0].V.AsDouble()}");

            VTQs filterNoneSum = await con.HistorianReadAggregatedIntervals(vQual, qualBounds, Aggregation.Sum, QualityFilter.ExcludeNone);
            assert(Math.Abs(filterNoneSum[0].V.AsDouble()!.Value - 1000.0) < 0.001, $"ExcludeNone Sum == 1000, got {filterNoneSum[0].V.AsDouble()}");

            // ExcludeBad: 3 values (Good, Uncertain, Good) -> Count=3, Sum=800
            VTQs filterBad = await con.HistorianReadAggregatedIntervals(vQual, qualBounds, Aggregation.Count, QualityFilter.ExcludeBad);
            assert(Math.Abs(filterBad[0].V.AsDouble()!.Value - 3.0) < 0.001, $"ExcludeBad Count == 3, got {filterBad[0].V.AsDouble()}");

            VTQs filterBadSum = await con.HistorianReadAggregatedIntervals(vQual, qualBounds, Aggregation.Sum, QualityFilter.ExcludeBad);
            assert(Math.Abs(filterBadSum[0].V.AsDouble()!.Value - 800.0) < 0.001, $"ExcludeBad Sum == 800, got {filterBadSum[0].V.AsDouble()}");

            // ExcludeNonGood: 2 values (Good, Good) -> Count=2, Sum=500
            VTQs filterNonGood = await con.HistorianReadAggregatedIntervals(vQual, qualBounds, Aggregation.Count, QualityFilter.ExcludeNonGood);
            assert(Math.Abs(filterNonGood[0].V.AsDouble()!.Value - 2.0) < 0.001, $"ExcludeNonGood Count == 2, got {filterNonGood[0].V.AsDouble()}");

            VTQs filterNonGoodSum = await con.HistorianReadAggregatedIntervals(vQual, qualBounds, Aggregation.Sum, QualityFilter.ExcludeNonGood);
            assert(Math.Abs(filterNonGoodSum[0].V.AsDouble()!.Value - 500.0) < 0.001, $"ExcludeNonGood Sum == 500, got {filterNonGoodSum[0].V.AsDouble()}");

            log.Info("TestHistorianAggregatedIntervals completed successfully");
        }

        private async Task ExpectCount(VariableRef v, long c) {
            if (con == null) throw new Exception("con == null");
            long count = await con.HistorianCount(v, Timestamp.Empty, Timestamp.Max);
            assert(count == c, $"count == {c}");
        }

        private async Task TestHistoryRaw(VariableRef v, Timestamp tStart, Timestamp tEnd, int maxValues, params VTQ[] expectedData) {

            if (con == null) throw new Exception("con == null");

            Action<VTTQs> test = (vttqs) => {
                assert(vttqs.Count == expectedData.Length, $"vttqs.Length == {expectedData.Length}");
                for (int i = 0; i < vttqs.Count; ++i) {
                    assert(vttqs[i].T == expectedData[i].T, $"vttqs[{i}].T == {expectedData[i].T}");
                    assert(vttqs[i].Q == expectedData[i].Q, $"vttqs[{i}].Q == {expectedData[i].Q}");
                    assert(vttqs[i].V == expectedData[i].V, $"vttqs[{i}].V == {expectedData[i].V}");
                }
            };

            test(await con.HistorianReadRaw(v, tStart, tEnd, maxValues, BoundingMethod.TakeFirstN));
            test(await con.HistorianReadRaw(v, tStart, tEnd, maxValues, BoundingMethod.TakeLastN));
        }

        private VariableRef GetVarRef(string varName) {
            return VariableRef.Make(moduleID, "obj", varName);
        }

        private static void assert(bool v, string msg) {
            if (!v) throw new Exception("Assert failed: " + msg);
        }

        private static async Task ExpectException(Func<Task> f) {
            try {
                await f();
            }
            catch (Exception) {
                return;
            }
            assert(false, "No exception!");
        }
    }


    class TimeCode
    {
        private readonly Logger log;
        private readonly System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();

        public TimeCode(Logger log) {
            this.log = log;
        }

        public void Print(string txt) {
            long ms = watch.ElapsedMilliseconds;
            log.Info($"{txt}: {ms} ms");
            watch.Restart();
        }
    }
}
