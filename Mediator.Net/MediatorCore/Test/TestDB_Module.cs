// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NLog;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;

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
