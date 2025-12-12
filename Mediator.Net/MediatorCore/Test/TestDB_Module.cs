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
                await TestHistorianUpsert();
                timer.Print("Completed TestHistorianUpsert");
                await TestHistorianInsertUpdate();
                timer.Print("Completed TestHistorianInsertUpdate");
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

        private async Task TestHistorianUpsert() {

            if (con == null) throw new Exception("con == null");
            log.Info($"TestHistorianUpsert");

            // Use a fresh variable to avoid interference from previous tests
            var vFew = GetVarRef("Variable_197");
            var vMany = GetVarRef("Variable_196");

            // Clear any existing data
            await con.HistorianModify(vFew, ModifyMode.ReplaceAll);
            await con.HistorianModify(vMany, ModifyMode.ReplaceAll);

            // Test 1: Few data points (< 10) - uses transaction loop path
            log.Info("Testing Upsert with few data points (< 10)...");

            Timestamp tBase = Timestamp.FromISO8601("2024-01-01T00:00:00Z");
            
            // Initial upsert with 5 items
            VTQ vtq1 = new VTQ(tBase.AddMillis(100), Quality.Good, DataValue.FromDouble(10));
            VTQ vtq2 = new VTQ(tBase.AddMillis(200), Quality.Good, DataValue.FromDouble(20));
            VTQ vtq3 = new VTQ(tBase.AddMillis(300), Quality.Good, DataValue.FromDouble(30));
            VTQ vtq4 = new VTQ(tBase.AddMillis(400), Quality.Good, DataValue.FromDouble(40));
            VTQ vtq5 = new VTQ(tBase.AddMillis(500), Quality.Good, DataValue.FromDouble(50));

            await con.HistorianModify(vFew, ModifyMode.Upsert, vtq1, vtq2, vtq3, vtq4, vtq5);
            await ExpectCount(vFew, 5);
            await TestHistoryRaw(vFew, Timestamp.Empty, Timestamp.Max, 10, vtq1, vtq2, vtq3, vtq4, vtq5);

            // Upsert with mix: 2 existing (updated), 2 new
            VTQ vtq1Updated = new VTQ(vtq1.T, Quality.Uncertain, DataValue.FromDouble(100)); // Update existing
            VTQ vtq3Updated = new VTQ(vtq3.T, Quality.Bad, DataValue.FromDouble(300)); // Update existing
            VTQ vtq6 = new VTQ(tBase.AddMillis(600), Quality.Good, DataValue.FromDouble(60)); // New
            VTQ vtq7 = new VTQ(tBase.AddMillis(700), Quality.Good, DataValue.FromDouble(70)); // New

            await con.HistorianModify(vFew, ModifyMode.Upsert, vtq1Updated, vtq3Updated, vtq6, vtq7);
            await ExpectCount(vFew, 7);
            await TestHistoryRaw(vFew, Timestamp.Empty, Timestamp.Max, 10, vtq1Updated, vtq2, vtq3Updated, vtq4, vtq5, vtq6, vtq7);

            // Test 2: Many data points (>= 10) - uses bulk binary import path
            log.Info("Testing Upsert with many data points (>= 10)...");

            // Initial upsert with 15 items
            VTQ[] initialMany = new VTQ[15];
            for (int i = 0; i < 15; ++i) {
                initialMany[i] = new VTQ(tBase.AddMillis(1000 + i * 100), Quality.Good, DataValue.FromDouble(i * 10));
            }

            await con.HistorianModify(vMany, ModifyMode.Upsert, initialMany);
            await ExpectCount(vMany, 15);

            // Verify initial data
            VTTQs readInitial = await con.HistorianReadRaw(vMany, Timestamp.Empty, Timestamp.Max, 20, BoundingMethod.TakeFirstN);
            assert(readInitial.Count == 15, $"Initial data count == 15, got {readInitial.Count}");
            for (int i = 0; i < 15; ++i) {
                assert(readInitial[i].T == initialMany[i].T, $"Initial[{i}].T matches");
                assert(readInitial[i].V == initialMany[i].V, $"Initial[{i}].V matches");
            }

            // Upsert with mix: 5 existing (updated), 5 new
            VTQ[] updateMany = new VTQ[10];
            // Update existing items (indices 0, 2, 4, 6, 8)
            updateMany[0] = new VTQ(initialMany[0].T, Quality.Uncertain, DataValue.FromDouble(1000)); // Update
            updateMany[1] = new VTQ(initialMany[2].T, Quality.Bad, DataValue.FromDouble(2000)); // Update
            updateMany[2] = new VTQ(initialMany[4].T, Quality.Good, DataValue.FromDouble(3000)); // Update
            updateMany[3] = new VTQ(initialMany[6].T, Quality.Uncertain, DataValue.FromDouble(4000)); // Update
            updateMany[4] = new VTQ(initialMany[8].T, Quality.Good, DataValue.FromDouble(5000)); // Update
            // Add new items
            updateMany[5] = new VTQ(tBase.AddMillis(2500), Quality.Good, DataValue.FromDouble(250));
            updateMany[6] = new VTQ(tBase.AddMillis(2600), Quality.Good, DataValue.FromDouble(260));
            updateMany[7] = new VTQ(tBase.AddMillis(2700), Quality.Good, DataValue.FromDouble(270));
            updateMany[8] = new VTQ(tBase.AddMillis(2800), Quality.Good, DataValue.FromDouble(280));
            updateMany[9] = new VTQ(tBase.AddMillis(2900), Quality.Good, DataValue.FromDouble(290));

            await con.HistorianModify(vMany, ModifyMode.Upsert, updateMany);
            await ExpectCount(vMany, 20); // 15 original - 5 updated + 5 new = 15 + 5 = 20

            // Verify updated data - read all and check key points
            VTTQs readFinal = await con.HistorianReadRaw(vMany, Timestamp.Empty, Timestamp.Max, 25, BoundingMethod.TakeFirstN);
            assert(readFinal.Count == 20, $"Final data count == 20, got {readFinal.Count}");

            // Verify updates
            bool foundUpdate0 = false, foundUpdate1 = false, foundUpdate2 = false, foundUpdate3 = false, foundUpdate4 = false;
            // Verify new items
            bool foundNew0 = false, foundNew1 = false, foundNew2 = false, foundNew3 = false, foundNew4 = false;

            foreach (var vtq in readFinal) {
                if (vtq.T == updateMany[0].T && vtq.V == updateMany[0].V && vtq.Q == updateMany[0].Q) foundUpdate0 = true;
                if (vtq.T == updateMany[1].T && vtq.V == updateMany[1].V && vtq.Q == updateMany[1].Q) foundUpdate1 = true;
                if (vtq.T == updateMany[2].T && vtq.V == updateMany[2].V && vtq.Q == updateMany[2].Q) foundUpdate2 = true;
                if (vtq.T == updateMany[3].T && vtq.V == updateMany[3].V && vtq.Q == updateMany[3].Q) foundUpdate3 = true;
                if (vtq.T == updateMany[4].T && vtq.V == updateMany[4].V && vtq.Q == updateMany[4].Q) foundUpdate4 = true;
                if (vtq.T == updateMany[5].T && vtq.V == updateMany[5].V && vtq.Q == updateMany[5].Q) foundNew0 = true;
                if (vtq.T == updateMany[6].T && vtq.V == updateMany[6].V && vtq.Q == updateMany[6].Q) foundNew1 = true;
                if (vtq.T == updateMany[7].T && vtq.V == updateMany[7].V && vtq.Q == updateMany[7].Q) foundNew2 = true;
                if (vtq.T == updateMany[8].T && vtq.V == updateMany[8].V && vtq.Q == updateMany[8].Q) foundNew3 = true;
                if (vtq.T == updateMany[9].T && vtq.V == updateMany[9].V && vtq.Q == updateMany[9].Q) foundNew4 = true;
            }

            assert(foundUpdate0, "Update 0 found in final data");
            assert(foundUpdate1, "Update 1 found in final data");
            assert(foundUpdate2, "Update 2 found in final data");
            assert(foundUpdate3, "Update 3 found in final data");
            assert(foundUpdate4, "Update 4 found in final data");
            assert(foundNew0, "New item 0 found in final data");
            assert(foundNew1, "New item 1 found in final data");
            assert(foundNew2, "New item 2 found in final data");
            assert(foundNew3, "New item 3 found in final data");
            assert(foundNew4, "New item 4 found in final data");

            log.Info("TestHistorianUpsert completed successfully");
        }

        private async Task TestHistorianInsertUpdate() {

            if (con == null) throw new Exception("con == null");
            log.Info($"TestHistorianInsertUpdate");

            // Use fresh variables to avoid interference from previous tests
            var vInsertFew = GetVarRef("Variable_195");
            var vInsertMany = GetVarRef("Variable_194");
            var vUpdateFew = GetVarRef("Variable_193");
            var vUpdateMany = GetVarRef("Variable_192");

            // Clear any existing data
            await con.HistorianModify(vInsertFew, ModifyMode.ReplaceAll);
            await con.HistorianModify(vInsertMany, ModifyMode.ReplaceAll);
            await con.HistorianModify(vUpdateFew, ModifyMode.ReplaceAll);
            await con.HistorianModify(vUpdateMany, ModifyMode.ReplaceAll);

            Timestamp tBase = Timestamp.FromISO8601("2024-02-01T00:00:00Z");

            // =====================================================================
            // Test 1: Insert with few data points (< 10) - uses transaction loop
            // =====================================================================
            log.Info("Testing Insert with few data points (< 10)...");

            VTQ[] insertFewData = new VTQ[5];
            for (int i = 0; i < 5; ++i) {
                insertFewData[i] = new VTQ(tBase.AddMillis(i * 100), Quality.Good, DataValue.FromDouble(i * 10));
            }

            await con.HistorianModify(vInsertFew, ModifyMode.Insert, insertFewData);
            await ExpectCount(vInsertFew, 5);

            VTTQs readInsertFew = await con.HistorianReadRaw(vInsertFew, Timestamp.Empty, Timestamp.Max, 10, BoundingMethod.TakeFirstN);
            assert(readInsertFew.Count == 5, $"Insert few: count == 5, got {readInsertFew.Count}");
            for (int i = 0; i < 5; ++i) {
                assert(readInsertFew[i].T == insertFewData[i].T, $"Insert few [{i}].T matches");
                assert(readInsertFew[i].V == insertFewData[i].V, $"Insert few [{i}].V matches");
                assert(readInsertFew[i].Q == insertFewData[i].Q, $"Insert few [{i}].Q matches");
            }

            // Verify duplicate insert fails
            await ExpectException(() => con.HistorianModify(vInsertFew, ModifyMode.Insert, insertFewData[0]));

            // =====================================================================
            // Test 2: Insert with many data points (>= 10) - uses COPY binary protocol
            // =====================================================================
            log.Info("Testing Insert with many data points (>= 10)...");

            VTQ[] insertManyData = new VTQ[15];
            for (int i = 0; i < 15; ++i) {
                insertManyData[i] = new VTQ(tBase.AddMillis(1000 + i * 100), Quality.Good, DataValue.FromDouble(i * 100));
            }

            await con.HistorianModify(vInsertMany, ModifyMode.Insert, insertManyData);
            await ExpectCount(vInsertMany, 15);

            VTTQs readInsertMany = await con.HistorianReadRaw(vInsertMany, Timestamp.Empty, Timestamp.Max, 20, BoundingMethod.TakeFirstN);
            assert(readInsertMany.Count == 15, $"Insert many: count == 15, got {readInsertMany.Count}");
            for (int i = 0; i < 15; ++i) {
                assert(readInsertMany[i].T == insertManyData[i].T, $"Insert many [{i}].T matches");
                assert(readInsertMany[i].V == insertManyData[i].V, $"Insert many [{i}].V matches");
                assert(readInsertMany[i].Q == insertManyData[i].Q, $"Insert many [{i}].Q matches");
            }

            // Verify duplicate insert fails for bulk path
            await ExpectException(() => con.HistorianModify(vInsertMany, ModifyMode.Insert, insertManyData));

            // =====================================================================
            // Test 3: Update with few data points (< 10) - uses transaction loop
            // =====================================================================
            log.Info("Testing Update with few data points (< 10)...");

            // First insert data to update
            VTQ[] updateFewInitial = new VTQ[5];
            for (int i = 0; i < 5; ++i) {
                updateFewInitial[i] = new VTQ(tBase.AddMillis(2000 + i * 100), Quality.Good, DataValue.FromDouble(i));
            }
            await con.HistorianModify(vUpdateFew, ModifyMode.Insert, updateFewInitial);

            // Now update with new values and different qualities
            VTQ[] updateFewData = new VTQ[5];
            updateFewData[0] = new VTQ(updateFewInitial[0].T, Quality.Uncertain, DataValue.FromDouble(1000));
            updateFewData[1] = new VTQ(updateFewInitial[1].T, Quality.Bad, DataValue.FromDouble(2000));
            updateFewData[2] = new VTQ(updateFewInitial[2].T, Quality.Good, DataValue.FromDouble(3000));
            updateFewData[3] = new VTQ(updateFewInitial[3].T, Quality.Uncertain, DataValue.FromDouble(4000));
            updateFewData[4] = new VTQ(updateFewInitial[4].T, Quality.Good, DataValue.FromDouble(5000));

            await con.HistorianModify(vUpdateFew, ModifyMode.Update, updateFewData);
            await ExpectCount(vUpdateFew, 5); // Count unchanged after update

            VTTQs readUpdateFew = await con.HistorianReadRaw(vUpdateFew, Timestamp.Empty, Timestamp.Max, 10, BoundingMethod.TakeFirstN);
            assert(readUpdateFew.Count == 5, $"Update few: count == 5, got {readUpdateFew.Count}");
            for (int i = 0; i < 5; ++i) {
                assert(readUpdateFew[i].T == updateFewData[i].T, $"Update few [{i}].T matches");
                assert(readUpdateFew[i].V == updateFewData[i].V, $"Update few [{i}].V matches, expected {updateFewData[i].V}, got {readUpdateFew[i].V}");
                assert(readUpdateFew[i].Q == updateFewData[i].Q, $"Update few [{i}].Q matches, expected {updateFewData[i].Q}, got {readUpdateFew[i].Q}");
            }

            // Verify update of non-existing timestamp fails
            VTQ nonExisting = new VTQ(tBase.AddMillis(9999), Quality.Good, DataValue.FromDouble(999));
            await ExpectException(() => con.HistorianModify(vUpdateFew, ModifyMode.Update, nonExisting));

            // =====================================================================
            // Test 4: Update with many data points (>= 10) - uses temp table + bulk update
            // =====================================================================
            log.Info("Testing Update with many data points (>= 10)...");

            // First insert data to update
            VTQ[] updateManyInitial = new VTQ[15];
            for (int i = 0; i < 15; ++i) {
                updateManyInitial[i] = new VTQ(tBase.AddMillis(3000 + i * 100), Quality.Good, DataValue.FromDouble(i));
            }
            await con.HistorianModify(vUpdateMany, ModifyMode.Insert, updateManyInitial);

            // Now update with new values and different qualities
            VTQ[] updateManyData = new VTQ[15];
            for (int i = 0; i < 15; ++i) {
                Quality q = (i % 3) switch {
                    0 => Quality.Good,
                    1 => Quality.Uncertain,
                    _ => Quality.Bad
                };
                updateManyData[i] = new VTQ(updateManyInitial[i].T, q, DataValue.FromDouble((i + 1) * 1000));
            }

            await con.HistorianModify(vUpdateMany, ModifyMode.Update, updateManyData);
            await ExpectCount(vUpdateMany, 15); // Count unchanged after update

            VTTQs readUpdateMany = await con.HistorianReadRaw(vUpdateMany, Timestamp.Empty, Timestamp.Max, 20, BoundingMethod.TakeFirstN);
            assert(readUpdateMany.Count == 15, $"Update many: count == 15, got {readUpdateMany.Count}");
            for (int i = 0; i < 15; ++i) {
                assert(readUpdateMany[i].T == updateManyData[i].T, $"Update many [{i}].T matches");
                assert(readUpdateMany[i].V == updateManyData[i].V, $"Update many [{i}].V matches, expected {updateManyData[i].V}, got {readUpdateMany[i].V}");
                assert(readUpdateMany[i].Q == updateManyData[i].Q, $"Update many [{i}].Q matches, expected {updateManyData[i].Q}, got {readUpdateMany[i].Q}");
            }

            // Verify update fails when some timestamps don't exist (bulk path)
            VTQ[] partialUpdate = new VTQ[12];
            for (int i = 0; i < 10; ++i) {
                partialUpdate[i] = new VTQ(updateManyInitial[i].T, Quality.Good, DataValue.FromDouble(i));
            }
            // Add 2 non-existing timestamps
            partialUpdate[10] = new VTQ(tBase.AddMillis(8888), Quality.Good, DataValue.FromDouble(8888));
            partialUpdate[11] = new VTQ(tBase.AddMillis(9999), Quality.Good, DataValue.FromDouble(9999));
            await ExpectException(() => con.HistorianModify(vUpdateMany, ModifyMode.Update, partialUpdate));

            // Verify original data unchanged after failed update
            VTTQs readAfterFail = await con.HistorianReadRaw(vUpdateMany, Timestamp.Empty, Timestamp.Max, 20, BoundingMethod.TakeFirstN);
            assert(readAfterFail.Count == 15, $"After failed update: count == 15, got {readAfterFail.Count}");
            for (int i = 0; i < 15; ++i) {
                assert(readAfterFail[i].V == updateManyData[i].V, $"After failed update [{i}].V unchanged");
            }

            log.Info("TestHistorianInsertUpdate completed successfully");
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
