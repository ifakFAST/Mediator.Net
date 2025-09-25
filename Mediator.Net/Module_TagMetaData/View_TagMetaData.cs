// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using Ifak.Fast.Mediator.Dashboard;
using Ifak.Fast.Mediator.TagMetaData.Config;
using ModuleInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ModuleInfo>;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;
using VTTQs = System.Collections.Generic.List<Ifak.Fast.Mediator.VTTQ>;

namespace Ifak.Fast.Mediator.TagMetaData;

[Identify(
    id: "TagMetaData", 
    bundle: "TagMetaData", 
    path: "tagmetadata.html", 
    configType: null, 
    icon: "mdi-tag-multiple-outline")]
public class View_TagMetaData : ViewBase
{
    private const string ModuleID = "TAG";
    private string imagePath = "";
    private string moduleTypePath = "";

    public override async Task OnActivate() {
        imagePath = await GetImagePath();
        moduleTypePath = await GetModuleTypePath();
        Context.SetGetRequestMapping("/block_images/", imagePath);
        Context.SetGetRequestMapping("/view_tagmetadata/moduletype/", moduleTypePath);
    }

    public async Task<ReqResult> UiReq_Init() {

        //await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), GetRootID());

        //Tag[] tags = await GetTags();
        MetaModel metaModel = await GetMetaModel();
        TagMetaData_Model model = await GetModel();
        //ObjInfo[] objInfos = await GetReferencedObjInfos(tags);
        ModuleInfos modules = await Connection.GetModules();
        string blockLib = await GetBlockLib();
        
        return ReqResult.OK(new {
            MetaModel = metaModel,
            FlowModel = MakeJRawWithCamelCase(model),
            //ObjectInfos = objInfos,
            ModuleInfos = modules
                .Where(m => m.ID.StartsWith("IO") || m.ID.StartsWith("CALC"))
                .Select(m => new {
                    ID = m.ID,
                    Name = m.Name
                }).ToArray(),
            BlockLibrary = new JRaw(blockLib),
        });
    }

    private static readonly JsonSerializerOptions JsonSerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonSerializerOptions IndentedJsonSerializerOptions = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static JRaw MakeJRawWithCamelCase(object obj) {
        return new JRaw(MakeJsonWithCamelCase(obj));
    }

    private static string MakeJsonWithCamelCase(object obj, bool indented = false) {
        return JsonSerializer.Serialize(obj, indented ? IndentedJsonSerializerOptions : JsonSerializerOptions);
    }

    private static T ObjectFromCamelCaseJSON<T>(string json) {
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions)!;
    }

    public async Task<ReqResult> UiReq_GetTagsForModule(string moduleID) {
        ObjectInfos objs = await Connection.GetAllObjectsWithVariablesOfType(moduleID, [.. DataTypeSets.Numerics]);
        var res = objs
            //.Where(obj => obj.Variables.Any(v => v.Name == "Value"))
            .Where(obj => obj.ClassNameShort == "DataItem" || obj.ClassNameShort == "Signal")
            .Select(obj => new {
                Name = obj.Name,
                ID = obj.ID.ToEncodedString(),
            }).ToArray();
        return ReqResult.OK(res);
    }

    private async Task<TagMetaData_Model> GetModel() {
        ObjectValue objRoot = await Connection.GetObjectValueByID(GetRootID());
        return objRoot.Value.Object<TagMetaData_Model>()!;
    }

    private async Task<MetaModel> GetMetaModel() {
        DataValue model = await Connection.CallMethod(ModuleID, "GetMetaModel");
        return model.Object<MetaModel>()!;
    }

    private async Task<string> GetBlockLib() {
        DataValue value = await Connection.CallMethod(ModuleID, "GetBlockLib");
        return value.JSON;
    }

    private async Task SetBlockLib(string json) {
        NamedValue nv = new NamedValue("BlockLibJson", json);
        await Connection.CallMethod(ModuleID, "SaveBlockLib", nv);
    }

    private async Task<string> GetImagePath() {
        DataValue value = await Connection.CallMethod(ModuleID, "GetImagePath");
        return value.GetString() ?? "";
    }

    private async Task<string> GetModuleTypePath() {
        DataValue value = await Connection.CallMethod(ModuleID, "GetModuleTypePath");
        return value.GetString() ?? "";
    }

    private static ObjectRef GetRootID() => ObjectRef.Make(ModuleID, "Root");

    private static bool IsNumeric(Variable v) => v.IsNumeric;

    class ObjInfo
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public string[] Variables { get; set; } = [];
    }

    private async Task<ObjInfo[]> GetReferencedObjInfos(Tag[] tags) {

        ObjectRef[] objects = tags.SelectMany(GetReferencedObjects).Distinct().ToArray();

        ObjectInfos infos;
        try {
            infos = await Connection.GetObjectsByID(objects);
        }
        catch (Exception) {
            infos = new ObjectInfos(objects.Length);
            for (int i = 0; i < objects.Length; ++i) {
                ObjectRef obj = objects[i];
                try {
                    infos.Add(await Connection.GetObjectByID(obj));
                }
                catch (Exception) {
                    infos.Add(new ObjectInfo(obj, "???", "???", "???"));
                }
            }
        }

        var objectInfos = new List<ObjInfo>();
        foreach (ObjectInfo info in infos) {
            var numericVariables = info.Variables.Where(IsNumeric).Select(v => v.Name).ToArray();
            objectInfos.Add(new ObjInfo() {
                ID = info.ID.ToEncodedString(),
                Name = info.Name,
                Variables = numericVariables
            });
        }
        return objectInfos.ToArray();
    }

    private static List<ObjectRef> GetReferencedObjects(Tag tag) {
        VariableRef? tagVar = tag.GetSourceTagVarRef();
        return tagVar.HasValue ? [tagVar.Value.Object] : [];
    }

    public async Task<ReqResult> UiReq_SaveModel(string modelJson) {
        try {
            var model = ObjectFromCamelCaseJSON<TagMetaData_Model>(modelJson);
            DataValue dataValue = DataValue.FromObject(model);
            ObjectValue objValue = ObjectValue.Make(GetRootID(), dataValue);
            await Connection.UpdateConfig(objValue);
            return ReqResult.OK("Model saved successfully");
        }
        catch (Exception ex) {
            return ReqResult.Bad($"Failed to save Model: {ex.Message}");
        }
    }

    public async Task<ReqResult> UiReq_GetTagNameFromID(string tagID) {
        try {
            ObjectRef objRef = ObjectRef.FromEncodedString(tagID);
            ObjectInfo objInfo = await Connection.GetObjectByID(objRef);
            return ReqResult.OK(new { Name = objInfo.Name });
        }
        catch (Exception ex) {
            return ReqResult.Bad($"Failed to get Tag Name from ID '{tagID}': {ex.Message}");
        }
    }

    public async Task<ReqResult> UiReq_GetModuleTypes() {
        try {
            DataValue result = await Connection.CallMethod(ModuleID, "GetModuleTypes");
            string[] moduleTypeIds = result.Object<string[]>() ?? [];
            return ReqResult.OK(moduleTypeIds);
        }
        catch (Exception ex) {
            return ReqResult.Bad($"Failed to get module types: {ex.Message}");
        }
    }

    public async Task<ReqResult> UiReq_SaveBlockLibrary(string modelJson) {
        try {

            var model = ObjectFromCamelCaseJSON<TagMetaData_Model>(modelJson);
            modelJson = MakeJsonWithCamelCase(model, indented: true);

            await SetBlockLib(modelJson);
            return ReqResult.OK("Block library saved successfully");
        }
        catch (Exception ex) {
            return ReqResult.Bad($"Failed to save block library: {ex.Message}");
        }
    }
}
