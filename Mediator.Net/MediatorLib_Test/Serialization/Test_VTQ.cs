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
    public class Test_VTQ
    {
        private readonly ITestOutputHelper console;

        public Test_VTQ(ITestOutputHelper console) {
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
                List<VTQ> listA = MakeTestData(len);
                var sw = Stopwatch.StartNew();

                VTQ_Serializer.Serialize(stream, listA, Common.CurrentBinaryVersion);

                sw.Stop();
                totalTicksSeri += sw.ElapsedTicks;
                //console.WriteLine($"{i} Dauer 1: {sw.ElapsedMilliseconds} ms {stream.Position} {listA.Count}");
                Console.WriteLine($"{stream.Position / (double)listA.Count} {listA.Count}");

                stream.Seek(0, SeekOrigin.Begin);
                sw.Restart();
                List<VTQ> listB;

                listB = VTQ_Serializer.Deserialize(stream);

                sw.Stop();
                totalTicksDeseri += sw.ElapsedTicks;

                bool ok = listA.Count == listB.Count && Enumerable.Range(0, listA.Count).All(x => listA[x] == listB[x]);
                if (!ok) throw new Exception("Test failed!");
                //console.WriteLine($"{i} Dauer 2: {sw.ElapsedMilliseconds} ms {ok}\n");

                if (i == 0) {
                    totalTicksSeri = 0;
                    totalTicksDeseri = 0;
                }
            }

            console.WriteLine(Util.FormatDuration("Serial", totalTicksSeri, repeat));
            console.WriteLine(Util.FormatDuration("Deseri", totalTicksDeseri, repeat));
        }

        internal static List<VTQ> MakeTestData(int n) {

            var list = new List<VTQ>(n);
            Timestamp t = Timestamp.FromISO8601("2021-03-20T10:00:00Z");

            int N = n / 4;

            AppendRegular(t, N, list);
            AppendRandom(t, N, list);
            AppendAllSame(t, N, list);
            AppendSemiRegular(t, N, list);

            AppendRegular(t, 1, list);
            AppendRandom(t, 1, list);
            AppendRegular(t, 2, list);
            AppendRandom(t, 1, list);
            AppendRegular(t, 2, list);
            AppendAllSame(t, 2, list);
            AppendRegular(t, 2, list);
            AppendRandom(t, 1, list);
            AppendAllSame(t, 1, list);
            AppendSemiRegular(t, 5, list);
            AppendRandom(t, 2, list);
            AppendSpecial(t, list);
            return list;
        }

        static void AppendRegular(Timestamp t, int n, List<VTQ> list) {
            for (int i = 0; i < n; ++i) {
                var vtq = VTQ.Make(
                    17.54321,
                    t + Duration.FromSeconds(i * 5),
                    Quality.Good);
                list.Add(vtq);
            }
        }

        static void AppendSemiRegular(Timestamp t, int n, List<VTQ> list) {
            for (int i = 0; i < n; ++i) {
                var vtq = VTQ.Make(
                    0.54321 + i,
                    t + Duration.FromSeconds(i * 60),
                    Quality.Good);
                list.Add(vtq);
            }
        }

        static void AppendRandom(Timestamp t, int n, List<VTQ> list) {
            Random rand = new Random(2808);
            for (int i = 0; i < n; ++i) {
                var vtq = VTQ.Make(
                    rand.NextDouble(),
                    t + Duration.FromMilliseconds(i * rand.Next(0, 5000000)),
                    Quality.Uncertain);
                list.Add(vtq);
            }
        }

        static void AppendAllSame(Timestamp t, int n, List<VTQ> list) {
            for (int i = 0; i < n; ++i) {
                var vtq = VTQ.Make(
                    -17.1321,
                    t + Duration.FromSeconds(10),
                    Quality.Bad);
                list.Add(vtq);
            }
        }

        static void AppendSpecial(Timestamp t, List<VTQ> list) {

            list.Add(VTQ.Make("", t + Duration.FromSeconds(10), Quality.Good));
            list.Add(VTQ.Make("A", t + Duration.FromSeconds(11), Quality.Good));
            list.Add(VTQ.Make("Ä", t + Duration.FromSeconds(18), Quality.Good));
            list.Add(VTQ.Make("AB", t + Duration.FromSeconds(19), Quality.Good));
            list.Add(VTQ.Make("ÖÜÄ", t + Duration.FromSeconds(30), Quality.Good));
            list.Add(VTQ.Make(new string('P', 255), t + Duration.FromSeconds(34), Quality.Good));
            list.Add(VTQ.Make(new string('a', 500), t + Duration.FromSeconds(38), Quality.Good));
            list.Add(VTQ.Make(new string('ä', 9000), t + Duration.FromMilliseconds(55005), Quality.Good));
            list.Add(VTQ.Make(DataValue.Empty, t + Duration.FromSeconds(30), Quality.Good));
        }
    }
}
