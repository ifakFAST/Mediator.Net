// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ifak.Fast.Json.Linq;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;
using Ifak.Fast.Mediator.Util;

namespace Ifak.Fast.Mediator.Dashboard
{
    [Identify(id: "GenericModuleConfig", bundle: "Generic", path: "generic.html", configType: typeof(ViewConfig), icon: "mdi-swap-vertical")]
    public class View_GenericConfig : ViewBase
    {
        private ViewConfig configuration = new ViewConfig();

        private readonly Dictionary<string, ClassInfo> objTypes = new Dictionary<string, ClassInfo>();
        private readonly Dictionary<string, EnumInfo> enumTypes = new Dictionary<string, EnumInfo>();
        private readonly Dictionary<string, StructInfo> structTypes = new Dictionary<string, StructInfo>();
        private ObjectInfos objects = new ObjectInfos();

        private readonly Dictionary<VariableRef, VTQ> mapVariables = new Dictionary<VariableRef, VTQ>();
        private bool hasLocations = false;
        private LocationRef? rootLocation = null;

        public override Task OnActivate() {

            if (Config.NonEmpty) {
                configuration = Config.Object<ViewConfig>() ?? new ViewConfig();
            }

            return Task.FromResult(true);
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            bool hasModuleID = !(configuration == null || string.IsNullOrEmpty(configuration.ModuleID));
            string moduleID = hasModuleID ? configuration!.ModuleID : "IO";

            switch (command) {

                case "GetModel": {

                        objects = await Connection.GetAllObjects(moduleID);

                        mapVariables.Clear();
                        ObjectInfo root = objects.First(o => !o.Parent.HasValue);
                        List<VariableValue> variables = await Connection.ReadAllVariablesOfObjectTree(root.ID);
                        await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), root.ID);

                        foreach (VariableValue vv in variables) {
                            mapVariables[vv.Variable] = vv.Value;
                        }

                        TreeNode node = TransformModel(objects);

                        MetaInfos types = await Connection.GetMetaInfos(moduleID);

                        objTypes.Clear();
                        foreach (ClassInfo ci in types.Classes) {
                            objTypes[ci.FullName] = ci;
                        }
                        enumTypes.Clear();
                        foreach (EnumInfo en in types.Enums) {
                            enumTypes[en.FullName] = en;
                        }
                        structTypes.Clear();
                        foreach (StructInfo sn in types.Structs) {
                            structTypes[sn.FullName] = sn;
                        }

                        JObject typMap = new JObject();
                        foreach (ClassInfo ci in types.Classes) {

                            var members = ci.ObjectMember
                                .Where(m => m.Dimension == Dimension.Array)
                                .Select(m => new {
                                    Array = m.Name,
                                    Type = m.ClassName
                                }).ToArray();

                            var entry = new {
                                ObjectMembers = members,
                                ci.IsExportable,
                                ci.IsImportable
                            };

                            typMap[ci.FullName] = new JRaw(StdJson.ObjectToString(entry));
                        }

                        var locations = await Connection.GetLocations();
                        hasLocations = locations.Count > 0;
                        rootLocation = hasLocations ? LocationRef.FromLocationID(locations[0].ID) : (LocationRef?)null;

                        return ReqResult.OK(new {
                            ObjectTree = node,
                            TypeInfo = typMap,
                            Locations = locations,
                        });
                    }

                case "GetObject": {

                        GetObjectParams pars = parameters.Object<GetObjectParams>() ?? throw new Exception("GetObjectParams is null");
                        var values = await GetObjectMembers(pars.ID, pars.Type);

                        ClassInfo info = objTypes[pars.Type];
                        var childTypes = info.ObjectMember
                            .GroupBy(om => om.ClassName)
                            .Select(g => new ChildType() {
                                TypeName = g.Key,
                                Members = g.Select(x => x.Name).ToArray()
                            }).ToList();

                        var res = new {
                            ObjectValues = values,
                            ChildTypes = childTypes
                        };
                        return ReqResult.OK(res);
                    }

                case "Save": {

                        SaveParams saveParams = parameters.Object<SaveParams>() ?? throw new Exception("SaveParams is null");

                        foreach (var m in saveParams.Members) {
                            Console.WriteLine(m.Name + " => " + m.Value);
                        }
                        ObjectRef obj = ObjectRef.FromEncodedString(saveParams.ID);
                        MemberValue[] mw = saveParams.Members.Select(m => MemberValue.Make(obj, m.Name, DataValue.FromJSON(m.Value))).ToArray();

                        await Connection.UpdateConfig(mw);

                        objects = await Connection.GetAllObjects(moduleID);
                        TreeNode node = TransformModel(objects);

                        var values = await GetObjectMembers(saveParams.ID, saveParams.Type);
                        return ReqResult.OK(new {
                            ObjectValues = values,
                            ObjectTree = node
                        });
                    }

                case "Delete": {

                        ObjectRef obj = ObjectRef.FromEncodedString(parameters.GetString() ?? "");
                        await Connection.UpdateConfig(ObjectValue.Make(obj, DataValue.Empty));

                        objects = await Connection.GetAllObjects(moduleID);
                        TreeNode node = TransformModel(objects);
                        return ReqResult.OK(node);
                    }

                case "AddObject": {

                        AddObjectParams addParams = parameters.Object<AddObjectParams>() ?? throw new Exception("AddObjectParams is null");
                        ObjectRef objParent = ObjectRef.FromEncodedString(addParams.ParentObjID);
                        DataValue dataValue = DataValue.FromObject(new {
                            ID = addParams.NewObjID,
                            Name = addParams.NewObjName
                        });
                        var element = AddArrayElement.Make(objParent, addParams.ParentMember, dataValue);
                        await Connection.UpdateConfig(element);

                        objects = await Connection.GetAllObjects(moduleID);

                        List<VariableValue> newVarVals = await Connection.ReadAllVariablesOfObjectTree(ObjectRef.Make(objParent.ModuleID, addParams.NewObjID));
                        foreach (VariableValue vv in newVarVals) {
                            mapVariables[vv.Variable] = vv.Value;
                        }

                        TreeNode node = TransformModel(objects);
                        return ReqResult.OK(new {
                            ObjectID = ObjectRef.Make(moduleID, addParams.NewObjID),
                            Tree = node
                        });
                    }

                case "DragDrop": {

                        DragDropParams dropParams = parameters.Object<DragDropParams>() ?? throw new Exception("DragDropParams is null");

                        ObjectRef obj = ObjectRef.FromEncodedString(dropParams.FromID);
                        ObjectValue objValue = await Connection.GetObjectValueByID(obj);

                        var deleteObj = ObjectValue.Make(obj, DataValue.Empty);

                        ObjectRef objParent = ObjectRef.FromEncodedString(dropParams.ToID);

                        var addElement = AddArrayElement.Make(objParent, dropParams.ToArray, objValue.Value);

                        await Connection.UpdateConfig(new ObjectValue[] { deleteObj }, new MemberValue[0], new AddArrayElement[] { addElement } );

                        objects = await Connection.GetAllObjects(moduleID);
                        TreeNode node = TransformModel(objects);
                        return ReqResult.OK(node);
                    }

                case "WriteVariable": {

                        var write = parameters.Object<WriteVariable_Params>() ?? throw new Exception("WriteVariable_Params is null");
                        VTQ vtq = new VTQ(Timestamp.Now, Quality.Good, DataValue.FromJSON(write.V));
                        await Connection.WriteVariable(ObjectRef.FromEncodedString(write.ObjID), write.Var, vtq);
                        return ReqResult.OK();
                    }

                case "MoveObject": {

                        var move = parameters.Object<MoveObject_Params>() ?? throw new Exception("MoveObject_Params is null");
                        bool up = move.Up;

                        ObjectRef obj = ObjectRef.FromEncodedString(move.ObjID);
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

                        objects = await Connection.GetAllObjects(moduleID);
                        TreeNode node = TransformModel(objects);
                        return ReqResult.OK(node);
                    }

                case "Browse": {

                        var browse = parameters.Object<Browse_Params>() ?? throw new Exception("Browse_Params is null");

                        var m = MemberRef.Make(ObjectRef.FromEncodedString(browse.ObjID), browse.Member);
                        BrowseResult res = await Connection.BrowseObjectMemberValues(m);
                        return ReqResult.OK(res.Values.Select(d => d.GetString()));

                    }

                case "Export": {
                        var exp = parameters.Object<Exp_Params>() ?? throw new Exception("Exp_Params is null");
                        DataValue exported = await Connection.CallMethod(moduleID, "ExportObjectAsFile", new NamedValue("objID", exp.ObjID));
                        ExportResult obj = exported.Object<ExportResult>() ?? throw new Exception("ExportResult is null");
                        var res = MemoryManager.GetMemoryStream("DownloadFile");
                        try {
                            res.Write(obj.Data, 0, obj.Data.Length);
                            res.Seek(0, SeekOrigin.Begin);
                            return new ReqResult(200, res, obj.ContentType);
                        }
                        catch (Exception) {
                            res.Dispose();
                            throw;
                        }
                    }

                case "GetNewID": {
                        string type = parameters.GetString() ?? throw new Exception("Type parameter is null");
                        string id = GetObjectID(type);
                        return ReqResult.OK(id);
                    }

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        public override async Task OnVariableValueChanged(List<VariableValue> variables) {

            var changes = new List<VarChange>(variables.Count);

            for (int n = 0; n < variables.Count; ++n) {
                VariableValue vv = variables[n];
                mapVariables[vv.Variable] = vv.Value;
                changes.Add(new VarChange() {
                    ObjectID = vv.Variable.Object.ToString(),
                    VarName = vv.Variable.Name,
                    V = vv.Value.V,
                    T = vv.Value.T,
                    Q = vv.Value.Q
                });
            }
            await Context.SendEventToUI("VarChange", changes);
        }

        private async Task<List<ObjectMember>> GetObjectMembers(string id, string type) {

            ObjectRef obj = ObjectRef.FromEncodedString(id);
            ClassInfo info = objTypes[type];

            MemberRef[] members = info.SimpleMember.Select(m => MemberRef.Make(obj, m.Name)).ToArray();
            List<MemberValue> memValues = await Connection.GetMemberValues(members);

            var values = new List<ObjectMember>();

            for (int i = 0; i < info.SimpleMember.Count; ++i) {
                SimpleMember m = info.SimpleMember[i];
                if (m.Type == DataType.LocationRef && !hasLocations) continue;

                MemberValue v = memValues[i];
                string defaultValue = "";
                if (m.DefaultValue.HasValue && m.Dimension != Dimension.Array) {
                    defaultValue = m.DefaultValue.Value.JSON;
                }
                else if (m.Type == DataType.Struct) {
                    defaultValue = StdJson.ObjectToString(GetStructDefaultValue(m)!, indented: true, ignoreShouldSerializeMembers: false);
                    //Console.WriteLine("=> " + m.Name + ": " + defaultValue);
                }
                else if (m.Type == DataType.LocationRef && rootLocation.HasValue) {
                    defaultValue = DataValue.FromLocationRef(rootLocation.Value).JSON;
                }
                else {
                    defaultValue = DataValue.FromDataType(m.Type, 1).JSON;
                }
                var member = new ObjectMember() {
                    Key = obj.ToEncodedString() + "__" + m.Name,
                    Name = m.Name,
                    Type = m.Type.ToString(),
                    IsScalar = m.Dimension == Dimension.Scalar,
                    IsOption = m.Dimension == Dimension.Optional,
                    IsArray = m.Dimension == Dimension.Array,
                    Category = m.Category,
                    Browseable = m.Browseable,
                    Value = new JRaw(v.Value.JSON),
                    ValueOriginal = new JRaw(v.Value.JSON),
                    EnumValues = ResolveEnum(m),
                    StructMembers = ResolveStruct(m),
                    DefaultValue = defaultValue
                };
                values.Add(member);
            }
            return values;
        }

        private StructMember[] ResolveStruct(SimpleMember m) {
            if (m.Type != DataType.Struct) return new StructMember[0];
            string structName = m.TypeConstraints;
            StructInfo structInfo = structTypes[structName];
            return structInfo.Member.Select(sm => new StructMember() {
                Name = sm.Name,
                Type = sm.Type.ToString(),
                IsScalar = sm.Dimension == Dimension.Scalar,
                IsOption = sm.Dimension == Dimension.Optional,
                IsArray = sm.Dimension == Dimension.Array,
                EnumValues = ResolveEnum(sm),
                StructMembers = ResolveStruct(sm)
            }).ToArray();
        }

        private JObject? GetStructDefaultValue(SimpleMember m) {
            if (m.Type != DataType.Struct) return null;
            string structName = m.TypeConstraints;
            StructInfo structInfo = structTypes[structName];
            JObject obj = new JObject();
            foreach (var sm in structInfo.Member) {
                string dv;
                if (sm.DefaultValue.HasValue) {
                    dv = sm.DefaultValue.Value.JSON;
                }
                else if (sm.Dimension == Dimension.Optional) {
                    dv = "null";
                }
                else {
                    dv = DataValue.FromDataType(sm.Type, 1).JSON;
                }
                obj[sm.Name] = new JRaw(dv);
            }
            return obj;
        }

        private string[] ResolveEnum(SimpleMember m) {
            if (m.Type != DataType.Enum) return new string[0];
            string enumName = m.TypeConstraints;
            EnumInfo enumInfo = enumTypes[enumName];
            return enumInfo.Values.Select(ev => ev.Description).ToArray();
        }

        private string GetObjectID(string type) {

            ClassInfo info = objTypes[type];

            string prefix = info.IdPrefix + "_";
            int prefixLen = prefix.Length;

            int maxN = 0;

            foreach (ObjectInfo obj in objects) {
                if (obj.ClassNameFull == type) {
                    string localID = obj.ID.LocalObjectID;
                    if (localID.StartsWith(prefix)) {
                        string num = localID.Substring(prefixLen);
                        int n;
                        if (int.TryParse(num, out n)) {
                            if (n > maxN) {
                                maxN = n;
                            }
                        }
                    }
                }
            }
            return prefix + (maxN + 1).ToString("000");
        }

        //////////////////////////////////////////

        public class ObjectMember
        {
            public string Key { get; set; } = "";
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public bool IsScalar { get; set; }
            public bool IsOption { get; set; }
            public bool IsArray { get; set; }
            public string Category { get; set; } = "";
            public bool Browseable { get; set; }
            public string[] BrowseValues { get; set; } = new string[0];
            public bool BrowseValuesLoading { get; set; } = false;
            public JToken Value { get; set; } = new JObject();
            public JToken ValueOriginal { get; set; } = new JObject();
            public string[] EnumValues { get; set; } = new string[0];
            public StructMember[] StructMembers { get; set; } = new StructMember[0];
            public string DefaultValue { get; set; } = "";
        }

        public class StructMember
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public bool IsScalar { get; set; }
            public bool IsOption { get; set; }
            public bool IsArray { get; set; }
            public string[] EnumValues { get; set; } = new string[0];
            public StructMember[] StructMembers { get; set; } = new StructMember[0];
        }

        //////////////////////////////////////////

        public class GetObjectParams
        {
            public string ID { get; set; } = "";
            public string Type { get; set; } = "";
        }

        public class SaveParams
        {
            public string ID { get; set; } = "";
            public string Type { get; set; } = "";
            public SaveMember[] Members { get; set; } = new SaveMember[0];
        }

        public class SaveMember
        {
            public string Name { get; set; } = "";
            public string Value { get; set; } = "";
        }

        public class AddObjectParams
        {
            public string ParentObjID { get; set; } = "";
            public string ParentMember { get; set; } = "";
            public string NewObjID { get; set; } = "";
            public string NewObjType { get; set; } = "";
            public string NewObjName { get; set; } = "";
        }

        public class DragDropParams
        {
            public string FromID { get; set; } = "";
            public string ToID { get; set; } = "";
            public string ToArray { get; set; } = "";
        }

        public class WriteVariable_Params
        {
            public string ObjID { get; set; } = "";
            public string Var { get; set; } = "";
            public string V { get; set; } = "";
        }

        public class MoveObject_Params
        {
            public string ObjID { get; set; } = "";
            public bool Up { get; set; }
        }

        public class Exp_Params {
            public string ObjID { get; set; } = "";
        }

        public class ExportResult {
            public string ContentType { get; set; } = "";
            public byte[] Data { get; set; } = Array.Empty<byte>();
        }

        public class Browse_Params
        {
            public string ObjID { get; set; } = "";
            public string Member { get; set; } = "";
        }

        public class ChildType
        {
            public string TypeName { get; set; } = "";
            public string[] Members { get; set; } = new string[0];
        }

        private TreeNode TransformModel(ObjectInfos objects) {

            ObjectInfo? rootObjInfo = null;
            var objectsChildren = new Dictionary<ObjectRef, ObjectInfos>();

            foreach (ObjectInfo obj in objects) {
                MemberRefIdx? parent = obj.Parent;
                if (parent.HasValue) {
                    var key = parent.Value.Object;
                    if (!objectsChildren.ContainsKey(key)) {
                        objectsChildren[key] = new ObjectInfos();
                    }
                    objectsChildren[key].Add(obj);
                }
                else {
                    rootObjInfo = obj;
                }
            }

            if (rootObjInfo == null) throw new Exception("No root object found");

            return MapObjectInfo2TreeNode(rootObjInfo, null, objectsChildren, new List<TreeNode>(0));
        }

        private TreeNode MapObjectInfo2TreeNode(ObjectInfo obj, ObjectInfos? siblings, Dictionary<ObjectRef, ObjectInfos> map, List<TreeNode> emptyChildren) {
            List<TreeNode> children;
            if (map.ContainsKey(obj.ID)) {
                var ch = map[obj.ID];
                children = new List<TreeNode>(ch.Count);
                for (int i = 0; i < ch.Count; ++i) {
                    ObjectInfo child = ch[i];
                    children.Add(MapObjectInfo2TreeNode(child, ch, map, emptyChildren));
                }
            }
            else {
                children = emptyChildren;
            }

            var listVariables = new List<VariableVal>();
            foreach (Variable v in obj.Variables) {
                var key = VariableRef.Make(obj.ID, v.Name);
                VTQ vtq;
                if (mapVariables.TryGetValue(key, out vtq)) {
                    listVariables.Add(new VariableVal() {
                        Name = v.Name,
                        Struct = v.Type == DataType.Struct,
                        Dim = v.Dimension,
                        V = vtq.V,
                        T = vtq.T,
                        Q = vtq.Q
                    });
                }
            }

            int count = 1;
            int idx = 0;

            if (obj.Parent.HasValue) {
                var p = obj.Parent.Value;
                idx = p.Index;
                // string mem = p.Name;
                count = siblings!.Count; // Assume all siblings are below the same parent member otherwise slow // (sib => sib.Parent!.Value.Name == mem);
            }

            return new TreeNode(
                id: obj.ID.ToString(),
                parentID: obj.Parent.HasValue ? obj.Parent.Value.Object.ToString() : "",
                first: idx == 0,
                last: idx + 1 == count,
                name: obj.Name,
                type: obj.ClassNameFull,
                children: children,
                variables: listVariables
            );
        }

        public class ViewConfig
        {
            public string ModuleID { get; set; } = "";
        }
    }

    public class TreeNode
    {
        public string ID { get; set; }
        public string ParentID { get; set; }
        public bool First { get; set; }
        public bool Last { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public List<VariableVal> Variables { get; set; }
        public List<TreeNode> Children { get; set; }

        public TreeNode() {
            ID = "";
            ParentID = "";
            Name = "";
            Type = "";
            Variables = new List<VariableVal>();
            Children = new List<TreeNode>();
        }

        public TreeNode(string id, string parentID, bool first, bool last, string name, string type, List<VariableVal> variables, List<TreeNode> children) {
            ID = id;
            ParentID = parentID;
            First = first;
            Last = last;
            Name = name;
            Type = type;
            Variables = variables;
            Children = children;
        }
    }

    public class VariableVal
    {
        public string Name { get; set; } = "";
        public bool Struct { get; set; }
        public int Dim { get; set; }
        public DataValue V { get; set; }
        public Timestamp T { get; set; }
        public Quality   Q { get; set; }
    }

    public class VarChange
    {
        public string ObjectID { get; set; } = "";
        public string VarName { get; set; } = "";
        public DataValue V { get; set; }
        public Timestamp T { get; set; }
        public Quality Q { get; set; }
    }
}
