// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    [IdentifyWidget(id: "VarTable")]
    public class VarTable : WidgetBaseWithConfig<VarTableConfig>
    {
        private VariableRef[] Variables = new VariableRef[0];
        private bool IsLoaded = false;

        public override string DefaultHeight => "";

        public override string DefaultWidth => "";

        VarTableConfig configuration => Config;

        public override Task OnActivate() {

            Variables = configuration.Items.Select(it => it.Variable).ToArray();

            Task ignored1 = Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), Variables);
            Task ignored2 = Connection.EnableVariableHistoryChangedEvents(Variables);

            return Task.FromResult(true);
        }

        public Task<ReqResult> UiReq_GetItemsData() {
            ObjectRef[] usedObjects = configuration.Items.Select(it => it.Variable.Object).Distinct().ToArray();
            return Common.GetItemsData(Connection, usedObjects);
        }

        public async Task<ReqResult> UiReq_LoadData() {

            List<VariableValue> values = await Connection.ReadVariablesIgnoreMissing(Variables);

            var items = MakeValues(configuration, values);

            IsLoaded = true;

            foreach (var it in configuration.Items) {
                Task ignored = CalcTrend(it);
            }

            return ReqResult.OK(items);
        }

        private static VarVal[] MakeValues(VarTableConfig config, IList<VariableValue> values) {

            var res = new List<VarVal>();
            foreach (var it in config.Items) {

                VariableValue vv = values.FirstOrDefault(vv => vv.Variable == it.Variable);
                if (vv.Variable != it.Variable) continue;

                VTQ vtq = vv.Value;
                double? value = vtq.V.AsDouble();

                string warning = null;
                string alarm = null;
                if (value.HasValue) {
                    var v = value.Value;
                    if (IsBelow(v, it.AlarmBelow)) {
                        alarm = $"Value is below alarm limit {it.AlarmBelow.Value}";
                    }
                    else if (IsAbove(v, it.AlarmAbove)) {
                        alarm = $"Value is above alarm limit {it.AlarmAbove.Value}";
                    }
                    else if (IsBelow(v, it.WarnBelow)) {
                        warning = $"Value is below warn limit {it.WarnBelow.Value}";
                    }
                    else if (IsAbove(v, it.WarnAbove)) {
                        warning = $"Value is above warn limit {it.WarnAbove.Value}";
                    }
                }

                if (vtq.Q != Quality.Good) {
                    string q = $"Quality of variable is {vtq.Q}";
                    if (alarm != null) {
                        alarm += "; " + q;
                    }
                    else if (warning != null) {
                        warning += "; " + q;
                    }
                    else {
                        if (vtq.Q == Quality.Bad) {
                            alarm = "Quality of variable is Bad";
                        }
                        else if (vtq.Q == Quality.Uncertain) {
                            warning = "Quality of variable is Uncertain";
                        }
                    }
                }

                var itt = new VarVal() {
                    Name = it.Name,
                    Value = value.HasValue ? FormatDouble(value.Value, 2) : "",
                    Unit = it.Unit,
                    Time = FormatTime(vtq.T),
                    Trend = "?",
                    Warning = warning,
                    Alarm = alarm,
                };

                res.Add(itt);
            }

            return res.ToArray();
        }

        private static bool IsBelow(double v, double? comp) => comp.HasValue ? v < comp.Value : false;
        private static bool IsAbove(double v, double? comp) => comp.HasValue ? v > comp.Value : false;

        private static string FormatDouble(double v, int decimalPlaces) {
            try {
                decimal dec = (decimal)v;
                decimal value = Math.Round(dec, decimalPlaces, MidpointRounding.AwayFromZero);
                return value.ToString();
            }
            catch (Exception) {
                return v.ToString();
            }
        }

        private static string FormatTime(Timestamp t) {
            if (t.IsEmpty) return "";
            DateTime local = t.ToDateTime().ToLocalTime();
            return local.ToString("HH:mm:ss");
        }

        public async Task<ReqResult> UiReq_SaveItems(VarItem[] items) {

            VariableRef[] newVariables = items.Select(it => it.Variable).ToArray();
            bool reloadData = !Arrays.Equals(newVariables, Variables);

            Variables = newVariables;

            configuration.Items = items;

            await Context.SaveWidgetConfiguration(configuration);

            if (reloadData) {
                Task ignored = Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), Variables);
                Task ignored2 = Connection.EnableVariableHistoryChangedEvents(Variables);
            }

            return ReqResult.OK(new {
                ReloadData = reloadData
            });
        }

        public override async Task OnVariableValueChanged(List<VariableValue> variables) {
            if (IsLoaded) {
                var payload = MakeValues(configuration, variables);
                if (payload.Length > 0) {
                    await Context.SendEventToUI("OnVarChanged", payload);
                }
            }
        }

        public override async Task OnVariableHistoryChanged(List<HistoryChange> changes) {

            Task[] tasks = configuration
                .Items
                .Where(it => changes.Any(ch => ch.Variable == it.Variable))
                .Select(CalcTrend)
                .ToArray();

            await Task.WhenAll(tasks);
        }

        private async Task CalcTrend(VarItem it) {

            Timestamp end = Timestamp.Now;
            Timestamp start = end - it.TrendFrame;
            var vttqs = await Connection.HistorianReadRaw(it.Variable, start, end, 120, BoundingMethod.CompressToN, QualityFilter.ExcludeBad);

            double[] values = vttqs.Where(v => v.V.AsDouble().HasValue).Select(v => v.V.AsDouble().Value).ToArray();

            string trend;
            switch (values.Length) {
                case 0:
                    trend = "";
                    break;
                case 1:
                    trend = "";
                    break;
                case 2:
                    double a = values[0];
                    double b = values[1];
                    if (a < b) {
                        trend = "up";
                    }
                    else if (a > b) {
                        trend = "down";
                    }
                    else {
                        trend = "flat";
                    }
                    break;

                default:

                    double v1 = GetMeanOfThird(values, 1);
                    double v2 = GetMeanOfThird(values, 2);
                    double vLatest = GetMeanOfThird(values, 3);

                    // Console.WriteLine($"{it.Name}: {it.TrendFrame} {values.Length} {Math.Round(v1, 3)} {Math.Round(v2, 3)} {Math.Round(vLatest, 3)}");

                    if (vLatest > v1 && vLatest > v2) {
                        trend = "up";
                    }
                    else if (vLatest < v1 && vLatest < v2) {
                        trend = "down";
                    }
                    else {
                        trend = "flat";
                    }
                    break;
            }

            await Context.SendEventToUI("TrendUpdate", new {
                Name = it.Name,
                Trend = trend,
            });
        }

        private static double GetMeanOfThird(double[] values, int third) {
            int n = values.Length / 3;
            double[] effective;
            if (third == 1) {
                effective = values.Take(n).ToArray();
            }
            else if (third == 2) {
                effective = values.Skip(n).SkipLast(n).ToArray();
            }
            else if(third == 3) {
                effective = values.TakeLast(n).ToArray();
            }
            else {
                throw new ArgumentException("third");
            }
            double N = effective.Length;
            return N == 0 ? 0 : effective.Select(v => v / N).Sum();
        }
    }

    public class VarTableConfig
    {
        public VarItem[] Items { get; set; } = new VarItem[0];
    }

    public class VarItem
    {
        public string Name { get; set; } = "";
        public string Unit { get; set; } = "";
        public Duration TrendFrame { get; set; } = Duration.FromMinutes(5);
        public VariableRef Variable { get; set; }
        public double? WarnBelow { get; set; } = null;
        public double? WarnAbove { get; set; } = null;
        public double? AlarmBelow { get; set; } = null;
        public double? AlarmAbove { get; set; } = null;
    }

    public class VarVal
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Time { get; set; } = "";
        public string Trend { get; set; } = "";
        public string Warning { get; set; } = null;
        public string Alarm { get; set; } = null;
    }
}
