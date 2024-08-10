// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MemberValues = System.Collections.Generic.List<Ifak.Fast.Mediator.MemberValue>;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets;

[IdentifyWidget(id: "ConfigEditNumeric2D")]
public class ConfigEditNumeric2D : WidgetBaseWithConfig<ConfigEditNumeric2DConfig>
{
    public override string DefaultHeight => "";
    public override string DefaultWidth => "100%";

    ConfigEditNumeric2DConfig configuration => Config;

    private readonly HashSet<MemberRef> jsonMembers = [];

    public override async Task OnActivate() {
        ObjectRef[] objs = UsedObjects();
        if (objs.Length > 0) {
            await Connection.EnableConfigChangedEvents(objs);
        }
    }

    private ObjectRef[] UsedObjects() {
        return configuration.Items
                        .Where(it => it.Object.HasValue && it.Member != null)
                        .Select(it => it.Object!.Value)
                        .Distinct()
                        .ToArray();
    }

    private MemberRef[] UsedMemberRefs() {
        return configuration.Items
                        .Where(it => it.Object.HasValue && it.Member != null)
                        .Select(it => MemberRef.Make(it.Object!.Value, it.Member!))
                        .Distinct()
                        .ToArray();
    }

    public async Task<ReqResult> UiReq_ReadValues() {
        return ReqResult.OK(await ReadValues());
    }

    /*
    public async Task<ReqResult> UiReq_GetItemsData() {

        ObjectRef[] usedObjects = configuration
            .Items.Where(it => it.Object.HasValue)
            .Select(it => it.Object!.Value)
            .Distinct()
            .ToArray();

        var modules = (await Connection.GetModules())
           .Select(m => new ModuleInfo() {
               ID = m.ID,
               Name = m.Name
           }).ToArray();

        ObjectInfos infos = await Connection.GetObjectsByID(usedObjects, ignoreMissing: true);

        View.ObjInfo[] res = await View.ReadObjects(Connection, infos, View.ObjectFilter.WithMembers);

        var objectMap = new Dictionary<string, ObjInfo>();

        foreach (var obj in res) {
            objectMap[obj.ID] = new ObjInfo() {
                Name = obj.Name,
                Members = obj.Members,
            };
        }

        return ReqResult.OK(new {
            ObjectMap = objectMap,
            Modules = modules,
        });
    } */

    public async Task<ResultEntry2D[]> ReadValues() {

        jsonMembers.Clear();

        MemberRef[] members = UsedMemberRefs();
        MemberValues memValues = await Connection.GetMemberValues(members, ignoreMissing: true);
        bool[] canEditArr = await Connection.CanUpdateConfig(members);

        Dictionary<MemberRef, bool> canEditMap = members.Zip(canEditArr, (m, e) => (m, e)).ToDictionary(t => t.m, t => t.e);
        Dictionary<MemberRef, MemberValue> map = memValues.ToDictionary(mv => mv.Member);

        MemberValue? ValueFromRef(MemberRef? memberRef) {
            if (!memberRef.HasValue) return null;
            return map.TryGetValue(memberRef.Value, out MemberValue mv) ? mv : null;
        }

        bool CanEditMember(MemberRef? memberRef) {
            if (!memberRef.HasValue) return false;
            return canEditMap.GetValueOrDefault(memberRef.Value, false);
        }

        var res = new List<ResultEntry2D>();
        foreach (ConfigItem2D it in configuration.Items) {
            
            MemberRef? memberRef = it.GetMemberRef();
            MemberValue? memberValue = ValueFromRef(memberRef);
            bool canEdit = CanEditMember(memberRef);

            string value = "";
            if (memberValue.HasValue) {
                MemberValue memVal = memberValue.Value;
                bool isStrValue = memVal.Value.JSON.StartsWith('"');
                if (isStrValue) { // assume type of member is JSON/DataValue
                    jsonMembers.Add(memVal.Member);
                }
                value = isStrValue ? memVal.Value.GetString()! : memVal.Value.JSON;
            }
            
            res.Add(new ResultEntry2D {
                IsEmpty = memberRef == null || memberValue == null,
                Value = value,
                Unit = it.Unit,
                CanEdit = canEdit,
            });
        }

        while (res.Count < configuration.Rows.Length * configuration.Columns.Length) {
            var itt = new ResultEntry2D {
                IsEmpty = true,
                Value = "",
                Unit = "",
                CanEdit = false,
            };
            res.Add(itt);
        }

        return res.ToArray();
    }

    public async Task<ReqResult> UiReq_WriteValue(string theObject, string member, string jsonValue, string displayValue) {

        DataValue dataValue = DataValue.FromJSON(jsonValue);
        MemberRef memberRef = MemberRef.Make(ObjectRef.FromEncodedString(theObject), member);

        bool isJSON = jsonMembers.Contains(memberRef);
        if (isJSON) {
            dataValue = DataValue.FromObject(dataValue);
        }

        MemberValue m = MemberValue.Make(memberRef, dataValue);
        await Connection.UpdateConfig(m);

        var  (rowIdx, colIdx) = GetRowColIdx(memberRef);
        if (rowIdx < 0 || colIdx < 0) {
            return ReqResult.OK();
        }
        string Row = configuration.Rows[rowIdx];
        string Col = configuration.Columns[colIdx];

        string name = $"{Row} {Col}";
        Task _ = Context.LogPageAction($"{name} = {displayValue}");

        return ReqResult.OK();
    }

    private (int rowIdx, int Colidx) GetRowColIdx(MemberRef member) {
        int ColCount = configuration.Columns.Length;
        int rowIdx = -1;
        int colIdx = -1;
        for (int i = 0; i < configuration.Items.Length; i++) {
            ConfigItem2D item = configuration.Items[i];
            if (item.Object == member.Object && item.Member == member.Name) {
                rowIdx = i / ColCount;
                colIdx = i % ColCount;
                break;
            }
        }
        return (rowIdx, colIdx);
    }

    /* public async Task<ReqResult> UiReq_SaveItems(ConfigItem2D[] items) {

        foreach (ConfigItem2D item in items) {
            item.Sanitize();
        }

        configuration.Items = items;

        await Context.SaveWidgetConfiguration(configuration);

        await Connection.DisableChangeEvents(
            disableVarValueChanges: false,
            disableVarHistoryChanges: false,
            disableConfigChanges: true);

        ObjectRef[] objs = UsedObjects();
        if (objs.Length > 0) {
            await Connection.EnableConfigChangedEvents(objs);
        }

        return ReqResult.OK();
    } */

    public override async Task OnConfigChanged(List<ObjectRef> changedObjects) {
        ResultEntry2D[] entries = await ReadValues();
        await Context.SendEventToUI("OnValuesChanged", entries);
    }
}

public class ConfigEditNumeric2DConfig
{
    public string[] Rows { get; set; } = [];
    public string[] Columns { get; set; } = [];
    public ConfigItem2D[] Items { get; set; } = []; // contains Rows.Length * Columns.Length items, ordered by rows
    public UnitRenderMode UnitRenderMode { get; set; } = UnitRenderMode.Hide;
}

public sealed class ConfigItem2D
{
    public string Unit { get; set; } = "";
    public ObjectRef? Object { get; set; } = null;
    public string? Member { get; set; } = null;
    public ItemType Type { get; set; } = ItemType.Range;
    public double? MinValue { get; set; } = null;
    public double? MaxValue { get; set; } = null;
    public string EnumValues { get; set; } = "";

    public void Sanitize() {
        if (Type == ItemType.Range) {
            EnumValues = "";
        }
        else if (Type == ItemType.Enum) {
            MinValue = null;
            MaxValue = null;
        }
    }

    public MemberRef? GetMemberRef() {
        if (Object.HasValue && Member != null) {
            return MemberRef.Make(Object!.Value, Member!);
        }
        return null;
    }
}

public sealed class ResultEntry2D
{
    public bool IsEmpty { get; set; } = false;
    public string Value { get; set; } = "";
    public string Unit { get; set; } = "";
    public bool CanEdit { get; set; } = false;
}

public sealed class ObjInfo
{
    public string Name { get; set; } = "";
    public string[] Members { get; set; } = [];
}
