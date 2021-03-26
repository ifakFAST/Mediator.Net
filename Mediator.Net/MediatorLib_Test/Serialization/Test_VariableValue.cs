using Ifak.Fast.Mediator;
using Ifak.Fast.Mediator.BinSeri;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MediatorLib_Test.Serialization
{
    public class Test_VariableValue
    {
        private readonly ITestOutputHelper console;

        public Test_VariableValue(ITestOutputHelper console) {
            this.console = console;
        }

        [Fact]
        public void Test() {

            const int len = 10000;
            const int repeat = 100;

            var stream = new MemoryStream(750 * 1024);

            long totalTicksSeri = 0;
            long totalTicksDeseri = 0;

            for (int i = 0; i < repeat; ++i) {

                stream.Seek(0, SeekOrigin.Begin);
                var listA = MakeTestData(len);
                var sw = Stopwatch.StartNew();

                VariableValue_Serializer.Serialize(stream, listA, Common.CurrentBinaryVersion);
                //StdJson.ObjectToStream(listA, stream);

                sw.Stop();
                totalTicksSeri += sw.ElapsedTicks;
                //Console.WriteLine($"{i} Dauer 1: {sw.ElapsedMilliseconds} ms {stream.Position} {listA.Count}");
                Console.WriteLine($"{stream.Position / (double)listA.Count} {listA.Count}");

                stream.Seek(0, SeekOrigin.Begin);
                sw.Restart();

                List<VariableValue> listB;
                listB = VariableValue_Serializer.Deserialize(stream);

                //using (var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true)) {
                //    listB = StdJson.ObjectFromReader<List<VariableValue>>(reader);
                //}

                sw.Stop();
                totalTicksDeseri += sw.ElapsedTicks;

                bool ok = listA.Count == listB.Count && Enumerable.Range(0, listA.Count).All(x => listA[x] == listB[x]);
                if (!ok) throw new Exception("Test failed!");
                //Console.WriteLine($"{i} Dauer 2: {sw.ElapsedMilliseconds} ms {ok}\n");

                if (i == 0) {
                    totalTicksSeri = 0;
                    totalTicksDeseri = 0;
                }
            }

            console.WriteLine(Util.FormatDuration("Serial", totalTicksSeri, repeat));
            console.WriteLine(Util.FormatDuration("Deseri", totalTicksDeseri, repeat));
        }

        public static List<VariableValue> MakeTestData(int n) {

            List<VTQ> vtqs = Test_VTQ.MakeTestData(n);
            List<VariableValue> res = new List<VariableValue>(vtqs.Count);

            List<VariableRef> varRefs = MakeVarRefs();

            for (int i = 0; i < vtqs.Count; ++i) {
                VTQ vtq = vtqs[i];
                VariableRef varRef = varRefs[i % varRefs.Count];
                res.Add(VariableValue.Make(varRef, vtq));
            }

            return res;
        }

        private static List<VariableRef> MakeVarRefs() {

            return new List<VariableRef>() {
                VariableRef.Make("IO", "ObjectName1", "Value"),
                VariableRef.Make("IO", "ObjectName2", "Value"),
                VariableRef.Make("IO", "ObjectName3", "Value"),
                //VariableRef.Make("IO", "INCTRL_WSBDN_FP_BB1_DENI_NOXC_FallbackSP", "Value"),
                VariableRef.Make("CALC", "Signal_Object_Name1", "Value"),
                VariableRef.Make("CALC", "Signal_Object_Name2", "Value"),
                VariableRef.Make("CALC", "Signal_Object_Name3", "Value"),
                //VariableRef.Make("CALC", "Signal_INCTRL_WSBDN_FP_BB1_DENI_NOXC_ymin_Min_PumpCapacity", "Value"),

                VariableRef.Make("IO2", "ObjectName1", "Value1"),
                VariableRef.Make("IO3", "ObjectName1", "Value2"),
                //VariableRef.Make("IO4", "ObjectName1", "Value3"),
                //VariableRef.Make("IO5", "ObjectName1", "Value4"),
                //VariableRef.Make("IO6", "ObjectName1", "Value5"),
                //VariableRef.Make("IO7", "ObjectName1", "Value6"),
                //VariableRef.Make("IO8", "ObjectName1", "Value7"),
                //VariableRef.Make("IO9", "ObjectName1", "Value8"),
                // VariableRef.Make("IO2", "ObjectName1","Value9"),
                //VariableRef.Make("IO3", "ObjectName1", "Value10"),
                //VariableRef.Make("IO4", "ObjectName1", "Value11"),
                //VariableRef.Make("IO5", "ObjectName1", "Value12"),
                //VariableRef.Make("IO6", "ObjectName1", "Value13"),
                //VariableRef.Make("IO7", "ObjectName1", "Value14"),
                //VariableRef.Make("IO8", "ObjectName1", "Value15"),
                //VariableRef.Make("IO9", "ObjectName1", "Value16"),
                //VariableRef.Make("IO9", "ObjectName1", "Value17"),
                //VariableRef.Make("IO9", "ObjectName1", "Value18"),
                //VariableRef.Make("IO9", "ObjectName1", "Value19"),
                //VariableRef.Make("IO9", "ObjectName1", "Value20"),
                //VariableRef.Make("IO9", "ObjectName1", "Value21"),
                //VariableRef.Make("IO9", "ObjectName1", "Value22"),
                //VariableRef.Make("IO9", "ObjectName1", "Value23"),
                //VariableRef.Make("IO9", "ObjectName1", "Value24"),
                //VariableRef.Make("IO9", "ObjectName1", "Value25"),
                //VariableRef.Make("IO9", "ObjectName1", "Value26"),
                //VariableRef.Make("IO9", "ObjectName1", "Value27"),
                //VariableRef.Make("IO9", "ObjectName1", "Value28"),
                //VariableRef.Make("IO9", "ObjectName1", "Value29"),
                //VariableRef.Make("IO9", "ObjectName1", "Value30"),
                //VariableRef.Make("IO9", "ObjectName1", "Value31"),
                //VariableRef.Make("IO9", "ObjectName1", "Value32"),
                //VariableRef.Make("IO9", "ObjectName1", "Value33"),
            };
        }
    }
}
