// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NLog;
using System;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Test
{
    public class TestDB_Module : ModuleBase
    {
        private Logger log = LogManager.GetLogger("TestDB");

        private Connection con = null;
        private Notifier notifier = null;
        private string moduleID = "";
        private const int VariablesCount = 1000;

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
                new ObjectInfo(ObjectRef.Make(moduleID, "obj"), "Obj", "Test", null, variables)
            };
            return Task.FromResult(res);
        }

        public override async Task Run(Func<bool> shutdown) {

            try {
                await TestHistorianModify();
                await TestHistorianManyVariables();
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
        }

        private async Task TestHistorianManyVariables() {

            Timestamp tStart = Timestamp.Now;
            Timestamp last = tStart;
            const int N = 10;

            for (int n = 0; n < N; ++n) {

                Timestamp t = Timestamp.Now;
                while (t <= last) {
                    await Task.Delay(1);
                    t = Timestamp.Now;
                }

                var values = new VariableValue[VariablesCount];
                for (int i = 0; i < VariablesCount; ++i) {
                    VTQ vtq = new VTQ(t, Quality.Good, DataValue.FromDouble(i));
                    values[i] = new VariableValue(GetVarRef("Variable_" + i.ToString()), vtq);
                }
                notifier.Notify_VariableValuesChanged(values);
                last = t;
            }

            for (int i = 0; i < 60; ++i) {
                long c = await con.HistorianCount(GetVarRef("Variable_0"), tStart, Timestamp.Max);
                if (c < N) {
                    await Task.Delay(1000);
                }
            }

            for (int i = 0; i < VariablesCount; ++i) {
                var v = GetVarRef("Variable_" + i.ToString());
                long c = await con.HistorianCount(v, tStart, Timestamp.Max);
                assert(c == N, $"{v} count = {c} != {N}");
            }
        }

        private async Task ExpectCount(VariableRef v, long c) {
            long count = await con.HistorianCount(v, Timestamp.Empty, Timestamp.Max);
            assert(count == c, $"count == {c}");
        }

        private async Task TestHistoryRaw(VariableRef v, Timestamp tStart, Timestamp tEnd, int maxValues, params VTQ[] expectedData) {

            Action<VTTQ[]> test = (vttqs) => {
                assert(vttqs.Length == expectedData.Length, $"vttqs.Length == {expectedData.Length}");
                for (int i = 0; i < vttqs.Length; ++i) {
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
}
