// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Dashboard;
using System;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using System.Collections.Generic;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;

namespace Ifak.Fast.Mediator.Publish;

[Dashboard.Identify(id: "Publish", configType: typeof(ViewConfig), icon: "mdi-publish")]
public class View_Publish : ViewBase
{
    private ViewConfig configuration = new ViewConfig();
    private ObjectRef RootID;
    private string moduleID = "";

    public override Task OnActivate() {
        if (Config.NonEmpty) {
            configuration = Config.Object<ViewConfig>() ?? new ViewConfig();
        }
        return Task.FromResult(true);
    }

    public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

        bool hasModuleID = !string.IsNullOrEmpty(configuration.ModuleID);
        moduleID = hasModuleID ? configuration.ModuleID : "PUB";

        switch (command) {
            case "GetModel": {

                    ObjectInfo root = await Connection.GetRootObject(moduleID);
                    RootID = root.ID;

                    return await GetModelResult();
                }

            case "Save": {

                    SaveParams saveParams = parameters.Object<SaveParams>() ?? throw new Exception("SaveParams is null");
                    string objID = saveParams.ID;
                    IDictionary<string, JToken?> dict = saveParams.Obj;
                    MemberValue[] members = dict
                        .Where(kv => kv.Key != "ID")
                        .Select(entry => MakeMemberValue(objID, entry))
                        .ToArray();
                    await Connection.UpdateConfig(members);

                    return await GetModelResult();
                }

            case "Delete": {

                    ObjectRef obj = ObjectRef.Make(moduleID, parameters.GetString() ?? "");
                    await Connection.UpdateConfig(ObjectValue.Make(obj, DataValue.Empty));

                    return await GetModelResult();
                }

            case "AddConfig": {

                    AddConfigParams addParams = parameters.Object<AddConfigParams>() ?? throw new Exception("AddConfigParams is null");
                    DataValue dataValue = DataValue.FromObject(new {
                        ID = addParams.NewID,
                        Name = addParams.NewName
                    });
                    var element = AddArrayElement.Make(RootID, addParams.ParentMember, dataValue);
                    await Connection.UpdateConfig(element);

                    return await GetModelResult();
                }

            case "MoveConfig": {

                    var move = parameters.Object<MoveConfigParams>() ?? throw new Exception("MoveConfigParams is null");
                    bool up = move.Up;

                    ObjectRef obj = ObjectRef.Make(moduleID, move.ObjID);
                    ObjectInfo objInfo = await Connection.GetObjectByID(obj);
                    MemberRefIdx? parentMember = objInfo.Parent;

                    if (parentMember.HasValue) {
                        MemberValue value = await Connection.GetMemberValue(parentMember.Value.ToMemberRef());
                        DataValue v = value.Value;
                        if (v.IsArray) {
                            JArray array = (JArray)StdJson.JTokenFromString(v.JSON);
                            int index = parentMember.Value.Index;
                            if (up && index > 0) {

                                JToken tmp = array[index - 1];
                                array[index - 1] = array[index];
                                array[index] = tmp;

                                MemberValue mv = MemberValue.Make(parentMember.Value.ToMemberRef(), DataValue.FromObject(array));
                                await Connection.UpdateConfig(mv);
                            }
                            else if (!up && index < array.Count - 1) {

                                JToken tmp = array[index + 1];
                                array[index + 1] = array[index];
                                array[index] = tmp;

                                MemberValue mv = MemberValue.Make(parentMember.Value.ToMemberRef(), DataValue.FromObject(array));
                                await Connection.UpdateConfig(mv);
                            }
                        }
                    }

                    return await GetModelResult();
                }

            case "ReadModuleObjects": {

                    var pars = parameters.Object<ReadModuleObjectsParams>() ?? throw new Exception("ReadModuleObjectsParams is null");

                    ObjectInfos objects;

                    try {
                        objects = await Connection.GetAllObjects(pars.ModuleID);
                    }
                    catch (Exception) {
                        objects = new ObjectInfos();
                    }

                    return ReqResult.OK(new {
                        Items = objects.Select(o => new {
                            Type = o.ClassNameFull,
                            ID = o.ID.ToEncodedString(),
                            Name = o.Name,
                        }).ToArray()
                    });
                }

            default:
                return ReqResult.Bad("Unknown command: " + command);
        }
    }

    private async Task<ReqResult> GetModelResult() {

        ObjectValue v = await Connection.GetObjectValueByID(RootID);
        Model model = v.ToObject<Model>() ?? new Model();

        var mods = await Connection.GetModules();

        var res = new {
            model = model,
            moduleInfos = mods.Select(m => new {
                ID = m.ID,
                Name = m.Name
            }).ToArray(),
        };

        return ReqResult.OK(res, ignoreShouldSerializeMembers: true);
    }

    private MemberValue MakeMemberValue(string id, KeyValuePair<string, JToken?> entry) {
        JToken? value = entry.Value;
        DataValue dataValue = value == null ? DataValue.Empty : DataValue.FromObject(value);
        return MemberValue.Make(moduleID, id, entry.Key, dataValue);
    }

    public class ViewConfig
    {
        public string ModuleID { get; set; } = "";
    }

    public class SaveParams
    {
        public string ID { get; set; } = "";

        public JObject Obj { get; set; } = new JObject();
    }

    public class AddConfigParams
    {
        public string ParentMember { get; set; } = "";
        public string NewID { get; set; } = "";
        public string NewName { get; set; } = "";
    }

    public class MoveConfigParams
    {
        public string ObjID { get; set; } = "";
        public bool Up { get; set; }
    }

    public class ReadModuleObjectsParams
    {
        public string ModuleID { get; set; } = "";
    }
}
