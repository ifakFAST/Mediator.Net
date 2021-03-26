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
    public class Test_VTTQ
    {
        private readonly ITestOutputHelper console;

        public Test_VTTQ(ITestOutputHelper console) {
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

                VTTQ_Serializer.Serialize(stream, listA, Common.CurrentBinaryVersion);

                sw.Stop();
                totalTicksSeri += sw.ElapsedTicks;
                Console.WriteLine($"{stream.Position / (double)listA.Count} {listA.Count}");
                //console.WriteLine($"{i} Dauer 1: {sw.ElapsedMilliseconds} ms {stream.Position} {listA.Count}");

                stream.Seek(0, SeekOrigin.Begin);
                sw.Restart();

                var listB = VTTQ_Serializer.Deserialize(stream);

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

        static List<VTTQ> MakeTestData(int n) {

            var list = new List<VTTQ>(n);
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

        static void AppendRegular(Timestamp t, int n, List<VTTQ> list) {
            Random rand = new Random(78412);
            for (int i = 0; i < n; ++i) {
                var vtq = VTTQ.Make(
                    DataValue.FromDouble(17.54321),
                    t + Duration.FromSeconds(i * 5),
                    t + Duration.FromSeconds(i * 5) + Duration.FromMilliseconds(2L * rand.Next(int.MinValue, int.MaxValue)),
                    Quality.Good);
                list.Add(vtq);
            }
        }

        static void AppendSemiRegular(Timestamp t, int n, List<VTTQ> list) {
            for (int i = 0; i < n; ++i) {
                var vtq = VTTQ.Make(
                    DataValue.FromDouble(0.54321 + i),
                    t + Duration.FromSeconds(i * 60),
                    t + Duration.FromSeconds(i * 60) + Duration.FromMilliseconds(i),
                    Quality.Good);
                list.Add(vtq);
            }
        }

        static void AppendRandom(Timestamp t, int n, List<VTTQ> list) {
            Random rand = new Random(2808);
            for (int i = 0; i < n; ++i) {
                var tt = t + Duration.FromMilliseconds(i * rand.Next(0, 5000000));
                var vtq = VTTQ.Make(
                    DataValue.FromDouble(rand.NextDouble()),
                    tt,
                    tt + Duration.FromMilliseconds(-1 * (i)),
                    Quality.Uncertain);
                list.Add(vtq);
            }
        }

        static void AppendAllSame(Timestamp t, int n, List<VTTQ> list) {
            for (int i = 0; i < n; ++i) {
                var vtq = VTTQ.Make(
                    DataValue.FromDouble(-17.1321),
                    t + Duration.FromSeconds(10),
                    t + Duration.FromSeconds(10),
                    Quality.Bad);
                list.Add(vtq);
            }
        }

        static void AppendSpecial(Timestamp t, List<VTTQ> list) {

            list.Add(VTTQ.Make(DataValue.FromString(""), t + Duration.FromSeconds(10), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString("A"), t + Duration.FromSeconds(11), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString("Ä"), t + Duration.FromSeconds(18), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString("AB"), t + Duration.FromSeconds(19), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString("ÖÜÄ"), t + Duration.FromSeconds(30), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString(new string('P', 255)), t + Duration.FromSeconds(34), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString(new string('a', 500)), t + Duration.FromSeconds(38), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.FromString(new string('ä', 9000)), t + Duration.FromMilliseconds(55005), t, Quality.Good));
            list.Add(VTTQ.Make(DataValue.Empty, t + Duration.FromSeconds(30), t, Quality.Good));
        }
    }
}
