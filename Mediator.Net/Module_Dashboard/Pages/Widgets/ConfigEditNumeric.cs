// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using MemberValues = System.Collections.Generic.List<Ifak.Fast.Mediator.MemberValue>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;
using System;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets {

    [IdentifyWidget(id: "ConfigEditNumeric")]
    public class ConfigEditNumeric : WidgetBaseWithConfig<ConfigEditConfig> {

        public override string DefaultHeight => "";

        public override string DefaultWidth => "100%";

        ConfigEditConfig configuration => Config;

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

        public sealed class ObjInfo {
            public string Name { get; set; } = "";
            public string[] Members { get; set; } = new string[0];
        }
        
        public async Task<ReqResult> UiReq_ToggleShowHeader() {
            configuration.ShowHeader = !configuration.ShowHeader;
            await Context.SaveWidgetConfiguration(configuration);
            return ReqResult.OK();
        }

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

            
            ObjectInfos infos;
            try {
                infos = await Connection.GetObjectsByID(usedObjects);
            }
            catch (Exception) {
                infos = new ObjectInfos(usedObjects.Length);
                for (int i = 0; i < usedObjects.Length; ++i) {
                    ObjectRef obj = usedObjects[i];
                    try {
                        infos.Add(await Connection.GetObjectByID(obj));
                    }
                    catch (Exception) {
                        infos.Add(new ObjectInfo(obj, "???", "???", "???"));
                    }
                }
            }

            View.ObjInfo[] res = await View.ReadObjectsWithMembers(Connection, infos);
            
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
        }

        private readonly HashSet<MemberRef> jsonMembers = new HashSet<MemberRef>();

        public async Task<ResultEntry[]> ReadValues() {

            jsonMembers.Clear();

            MemberRef[] members = UsedMemberRefs();

            if (members.Length > 0) {

                MemberValues memValues = await Connection.GetMemberValues(members);
                bool[] canEdit = await Connection.CanUpdateConfig(members);

                var res = new List<ResultEntry>();

                for (int i = 0; i < memValues.Count; i++) {

                    MemberValue it = memValues[i];
                    bool isStrValue = it.Value.JSON.StartsWith('"');

                    if (isStrValue) { // assume type of member is JSON/DataValue
                        jsonMembers.Add(it.Member);
                    }

                    var entry = new ResultEntry {
                        Object = it.Member.Object,
                        Member = it.Member.Name,
                        Value = isStrValue ? it.Value.GetString()! : it.Value.JSON,
                        CanEdit = canEdit[i],
                    };

                    res.Add(entry);
                }
                return res.ToArray();
            }
            else {
                return Array.Empty<ResultEntry>();
            }
        }

        public async Task<ReqResult> UiReq_WriteValue(string theObject, string member, string jsonValue, string displayValue, string oldValue) {

            DataValue dataValue = DataValue.FromJSON(jsonValue);
            MemberRef memberRef = MemberRef.Make(ObjectRef.FromEncodedString(theObject), member);

            bool isJSON = jsonMembers.Contains(memberRef);
            if (isJSON) {
                dataValue = DataValue.FromObject(dataValue);
            }

            MemberValue m = MemberValue.Make(memberRef, dataValue);
            await Connection.UpdateConfig(m);

            ConfigItem? item = ConfigItemByMemberRef(memberRef);
            string name = item != null ? item.Name : "???";
            Task _ = Context.LogPageAction($"{name}: {oldValue} 🡒 {displayValue}");

            return ReqResult.OK();
        }

        private ConfigItem? ConfigItemByMemberRef(MemberRef memberRef) {
            return configuration.Items.FirstOrDefault(x => x.Object == memberRef.Object && x.Member == memberRef.Name);
        }

        public async Task<ReqResult> UiReq_SaveItems(ConfigItem[] items) {

            foreach (ConfigItem item in items) {
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
        }

        public override async Task OnConfigChanged(List<ObjectRef> changedObjects) {
            ResultEntry[] entries = await ReadValues();
            await Context.SendEventToUI("OnValuesChanged", entries);
        }
    }

    public class ConfigEditConfig {
        public ConfigItem[] Items { get; set; } = new ConfigItem[0];
        public bool ShowHeader { get; set; } = true;
    }

    public sealed class ConfigItem {
        public string Name { get; set; } = "";
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
    }

    public enum ItemType {
        Range,
        Enum
    }

    public sealed class ResultEntry {
        public ObjectRef Object { get; set; } = ObjectRef.Make("", "");
        public string Member { get; set; } = "";
        public string Value { get; set; } = "";
        public bool CanEdit { get; set; } = false;
    }
}
