// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets;

[IdentifyWidget(id: "VarTable2D")]
public class VarTable2D : WidgetBaseWithConfig<VarTable2DConfig>
{
    private VariableRef[] Variables = [];
    private readonly Dictionary<VariableRef, string> mapVar2Unit = [];

    private bool IsLoaded = false;

    public override string DefaultHeight => "";

    public override string DefaultWidth => "";

    VarTable2DConfig configuration => Config;

    public override Task OnActivate() {

        Variables = configuration.Items.Select(it => it.Variable).ToArray();

        Task ignored1 = Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), Variables);

        return Task.FromResult(true);
    }

    public async Task<ReqResult> UiReq_LoadData() {
        var items = await LoadData();
        return ReqResult.OK(items);
    }

    public async Task<VarVal2D[]> LoadData() {

        VariableValues values = await Connection.ReadVariablesIgnoreMissing(Variables.ToList());
        ObjectInfos objs = await Connection.GetObjectsByID(Variables.Select(v => v.Object).Distinct().ToArray(), ignoreMissing: true);

        mapVar2Unit.Clear();
        foreach (ObjectInfo obj in objs) {
            foreach (Variable var in obj.Variables) {
                mapVar2Unit[VariableRef.Make(obj.ID, var.Name)] = var.Unit;
            }
        }

        var items = MakeValues(configuration, values, mapVar2Unit);

        IsLoaded = true;

        return items;
    }

    private static VarVal2D[] MakeValues(VarTable2DConfig config, IList<VariableValue> values, Dictionary<VariableRef, string> mapVar2Unit) {

        var res = new List<VarVal2D>();
        foreach (VarItem2D it in config.Items) {

            bool empty = false;

            VariableValue vv = values.FirstOrDefault(vv => vv.Variable == it.Variable);
            if (vv.Variable != it.Variable) {
                vv = VariableValue.Make(it.Variable, VTQ.Make("", Timestamp.Now, Quality.Bad));
                empty = true;
            }

            if (!mapVar2Unit.ContainsKey(it.Variable)) {
                mapVar2Unit[it.Variable] = "";
            }

            VTQ vtq = vv.Value;
            double? numericValue = vtq.V.AsDouble();

            string? warning = null;
            string? alarm = null;
            if (numericValue.HasValue) {
                var v = numericValue.Value;
                if (VarTable.IsBelow(v, it.AlarmBelow)) {
                    alarm = $"Value is below alarm limit {it.AlarmBelow!.Value}";
                }
                else if (VarTable.IsAbove(v, it.AlarmAbove)) {
                    alarm = $"Value is above alarm limit {it.AlarmAbove!.Value}";
                }
                else if (VarTable.IsBelow(v, it.WarnBelow)) {
                    warning = $"Value is below warn limit {it.WarnBelow!.Value}";
                }
                else if (VarTable.IsAbove(v, it.WarnAbove)) {
                    warning = $"Value is above warn limit {it.WarnAbove!.Value}";
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

            var itt = new VarVal2D() {
                IsEmpty = empty,
                Unit = mapVar2Unit[it.Variable],
                Time = VarTable.FormatTime(vtq.T),
                Warning = warning,
                Alarm = alarm,
            };

            if (numericValue.HasValue) {
                double numValue = numericValue.Value;
                itt.Value = VarTable.FormatDouble(numValue, 2);

                EnumValEntry[] enums = VarTable.ParseEnumValues(it.EnumValues);
                EnumValEntry? hitOrNulll = enums.FirstOrDefault(enumIt => enumIt.Num == numValue);
                if (hitOrNulll != null) {
                    itt.Value = hitOrNulll.Label;
                    itt.ValueColor = hitOrNulll.Color ?? "";
                }
            }
            else {

                string str = vtq.V.JSON;
                try {
                    str = vtq.V.GetString() ?? "";
                }
                catch { }

                itt.Value = str;
            }

            res.Add(itt);
        }

        while (res.Count < config.Rows.Length * config.Columns.Length) {
            var itt = new VarVal2D() {
                IsEmpty = true,
                Unit = "",
                Time = "",
                Warning = "",
                Alarm = "",
            };
            res.Add(itt);
        }

        return res.ToArray();
    }

    public override async Task OnVariableValueChanged(VariableValues variables) {
        if (IsLoaded) {
            var payload = MakeValues(configuration, variables, mapVar2Unit);
            if (payload.Length > 0) {
                await Context.SendEventToUI("OnVarChanged", payload);
            }
        }
    }
}

public enum UnitRenderMode
{
    Hide,
    Cell,
    ColumnLeft,
    ColumnRight,
    Row
}

public class VarTable2DConfig
{
    public string[] Rows { get; set; } = [];
    public string[] Columns { get; set; } = [];
    public VarItem2D[] Items { get; set; } = [];
    public UnitRenderMode UnitRenderMode { get; set; } = UnitRenderMode.Hide;
}

public class VarItem2D
{
    public VariableRef Variable { get; set; }
    public double? WarnBelow { get; set; } = null;
    public double? WarnAbove { get; set; } = null;
    public double? AlarmBelow { get; set; } = null;
    public double? AlarmAbove { get; set; } = null;
    public string EnumValues { get; set; } = "";
}

public class VarVal2D
{
    public bool IsEmpty { get; set; } = false;
    public string Value { get; set; } = "";
    public string ValueColor { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Time { get; set; } = "";
    public string? Warning { get; set; } = null;
    public string? Alarm { get; set; } = null;
}