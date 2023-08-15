﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Util
{
    public abstract class ModelObjectModule<T> : ModuleBase, Unnestable where T : IModelObject, new()
    {
        protected T model = new T();

        protected string modelFileName = "model.xml";
        protected string modelAsString = "";
        protected ModelFormat modelFormat = ModelFormat.XML;

        protected ObjectInfo[] allObjectInfos = new ObjectInfo[0];
        protected Dictionary<ObjectRef, ObjectInfo> mapObjectInfos = new Dictionary<ObjectRef, ObjectInfo>();
        protected Dictionary<ObjectRef, IModelObject> mapObjects = new Dictionary<ObjectRef, IModelObject>();

        protected string moduleID = "";
        protected string moduleName = "";
        protected Notifier? notifier = null;

        public virtual IModelObject? UnnestConfig(IModelObject parent, object? obj) => null;

        public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {
            this.moduleID = info.ModuleID;
            this.moduleName = info.ModuleName;
            this.notifier = notifier;
            var config = info.GetConfigReader();
            modelFileName = Path.GetFullPath(config.GetString("model-file"));
            modelAsString = ReadConfigFileToString(modelFileName);
            model = DeserializeModelFromString(modelAsString);
            ModifyModelAfterInit();
            await OnConfigModelChanged(init: true);
        }

        protected Origin GetModuleOrigin() {
            var origin = new Origin();
            origin.Type = OriginType.Module;
            origin.ID = moduleID;
            origin.Name = moduleName;
            return origin;
        }

        protected virtual void ModifyModelAfterInit() { }

        protected virtual string ReadConfigFileToString(string modelFileName) {
            return File.ReadAllText(modelFileName, Encoding.UTF8);
        }

        protected virtual void WriteConfigFile(string modelFileName, string modelAsString) {
            FileWrite.WriteFileOrThrow(modelFileName, modelAsString, Encoding.UTF8, maxRetry: 5, retryDelayFactor: 50);
        }

        protected virtual T DeserializeModelFromString(string model) {

            string m = model.TrimStart();

            if (m.StartsWith("<")) {
                modelFormat = ModelFormat.XML;
                return Xml.FromXmlString<T>(model);
            }
            else if (m.StartsWith("{")) {
                modelFormat = ModelFormat.JSON;
                return StdJson.ObjectFromString<T>(model) ?? new T();
            }
            else {
                throw new Exception($"Unknown file format for {modelFileName}: expected XML or JSON");
            }
        }

        protected virtual string SerializeModelToString(T model) {

            switch (modelFormat) {
                case ModelFormat.XML:
                    return Xml.ToXml(model);

                case ModelFormat.JSON:
                    return StdJson.ObjectToString(model, indented: true);

                default:
                    throw new Exception($"Model format {modelFormat} not implemented in SerializeModelToString");
            }
        }

        protected X? GetParentModelObjectFromIdOrNull<X>(ObjectRef id) where X : class, IModelObject {
            if (mapObjectInfos.TryGetValue(id, out ObjectInfo m)) {
                MemberRefIdx? parent = m.Parent;
                if (!parent.HasValue) return null;
                ObjectRef objParent = parent.Value.Object;
                return GetModelObjectFromIdOrNull<X>(objParent);
            }
            return null;
        }

        protected X GetParentModelObjectFromIdOrThrow<X>(ObjectRef id) where X : class, IModelObject {
            if (mapObjectInfos.TryGetValue(id, out ObjectInfo m)) {
                var parent = m.Parent;
                if (!parent.HasValue) throw new Exception($"Object {id} has no parent");
                ObjectRef objParent = parent.Value.Object;
                return GetModelObjectFromIdOrThrow<X>(objParent);
            }
            throw new Exception($"Unable to find object for id: {id}");
        }

        protected X? GetModelObjectFromIdOrNull<X>(ObjectRef id) where X : class, IModelObject {
            if (mapObjects.TryGetValue(id, out IModelObject m)) {
                if (m is X t) {
                    return t;
                }
            }
            return null;
        }

        protected X GetModelObjectFromIdOrThrow<X>(ObjectRef id) where X : class, IModelObject {
            X? res = GetModelObjectFromIdOrNull<X>(id);
            if (res != null) return res;
            throw new Exception($"Unable to find object for id: {id}");
        }

        protected ObjectRef? GetIdFromModelObjectOrNull<X>(X obj) where X : class, IModelObject {
            if (obj == null) return null;
            foreach (KeyValuePair<ObjectRef, IModelObject> entry in mapObjects) {
                IModelObject value = entry.Value;
                if (ReferenceEquals(value, obj)) {
                    return entry.Key;
                }
            }
            return null;
        }

        protected ObjectRef GetIdFromModelObjectOrThrow<X>(X obj) where X : class, IModelObject {
            if (obj == null) throw new ArgumentException("GetIdFromModelObjectOrThrow: obj argument is null");
            ObjectRef? res = GetIdFromModelObjectOrNull(obj);
            if (res.HasValue) return res.Value;
            throw new Exception("Unable to find ObjectRef for object");
        }

        protected virtual Task OnConfigModelChanged(bool init) {

            var res = new List<ObjectInfo>();
            var mapObjects = new Dictionary<ObjectRef, IModelObject>();

            var parents = new Stack<IModelObject>();
            InitFromModelImpl(moduleID, model, parents, null, res, mapObjects);

            var mapObjectInfos = new Dictionary<ObjectRef, ObjectInfo>(2 * res.Count);
            foreach (ObjectInfo obj in res) {
                mapObjectInfos[obj.ID] = obj;
            }

            this.allObjectInfos = res.ToArray();
            this.mapObjectInfos = mapObjectInfos;
            this.mapObjects = mapObjects;
            return Task.FromResult(true);
        }

        protected void InitFromModelImpl(string moduleID, IModelObject root, Stack<IModelObject> parents, MemberRefIdx? parent, List<ObjectInfo> res, Dictionary<ObjectRef, IModelObject> mapObjests) {
            ObjInfo info = root.GetObjectInfo(moduleID, parents, this);
            res.Add(new ObjectInfo(info.ID, info.DisplayName, info.ClassNameFull, info.ClassNameShort, parent, info.Variables, info.Location));
            ObjectRef key = info.ID;
            if (mapObjests.ContainsKey(key)) throw new Exception("Object id is not unique: " + key);
            mapObjests[key] = root;
            if (info.ChildObjects == null) return;
            parents.Push(root);
            foreach (ChildObjectsInMember member in info.ChildObjects) {
                int i = 0;
                foreach (IModelObject obj in member.ChildObjects) {
                    MemberRefIdx par = MemberRefIdx.Make(info.ID, member.MemberName, i);
                    InitFromModelImpl(moduleID, obj, parents, par, res, mapObjests);
                    i += 1;
                }
            }
            parents.Pop();
        }

        public override Task<ObjectInfo[]> GetAllObjects() => Task.FromResult(allObjectInfos);

        public override Task<MemberValue[]> GetMemberValues(MemberRef[] member) =>
            Task.FromResult(member.Where(m => mapObjects.ContainsKey(m.Object)).SelectMany(m => {
                var obj = mapObjects[m.Object];
                DataValue dv;
                try {
                    dv = obj.GetMemberValueByName(m.Name);
                }
                catch (Exception) { return new MemberValue[0]; }
                return new MemberValue[] { new MemberValue(m, dv) };
            }).ToArray());

        public override Task<MetaInfos> GetMetaInfo() {

            var allClasses = new Dictionary<string, ClassInfo>();
            var allStructs = new Dictionary<string, StructInfo>();
            var allEnums = new Dictionary<string, EnumInfo>();

            Type rootType = typeof(T);
            string fullName = rootType.FullName;
            allClasses[fullName] = new ClassInfo();
            ClassInfo rootClass = GetClassInfo(rootType, allClasses, allStructs, allEnums);
            rootClass.IsRoot = true;
            allClasses[fullName] = rootClass;

            var res = new MetaInfos(allClasses.Values.ToArray(), allStructs.Values.ToArray(), allEnums.Values.ToArray());
            return Task.FromResult(res);
        }

        private static ClassInfo GetClassInfo(Type typeClass, Dictionary<string, ClassInfo> allClasses, Dictionary<string, StructInfo> allStructs, Dictionary<string, EnumInfo> allEnums) {

            var res = new ClassInfo();

            res.FullName = typeClass.FullName;
            res.Name = typeClass.Name;
            res.IsAbstract = typeClass.IsAbstract;
            Type super = typeClass.BaseType;
            if (super != null && super.FullName != "System.Object" && super.FullName != "Mediator.Util.ModelObject") {
                res.BaseClassName = super.FullName;
            }

            object defaultObject = Activator.CreateInstance(typeClass);

            Type tIModelObject = typeof(IModelObject);
            Type tIModelObjectEnum = typeof(IEnumerable<IModelObject>);
            PropertyInfo[] properties = typeClass.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            DefaultCategory defaultCategory = typeClass.GetCustomAttribute<DefaultCategory>();
            res.IsExportable = typeClass.GetCustomAttribute<Exportable>() != null;
            res.IsImportable = typeClass.GetCustomAttribute<Importable>() != null;

            IdPrefix idPrefix = typeClass.GetCustomAttribute<IdPrefix>();
            if (idPrefix != null) {
                res.IdPrefix = idPrefix.Value;
            }
            else {
                res.IdPrefix = typeClass.Name;
            }

            foreach (PropertyInfo p in properties) {
                Type t = p.PropertyType;
                bool ignore = p.GetCustomAttribute<Ignore>() != null;
                if (ignore) continue;

                bool browseable = p.GetCustomAttribute<Browseable>() != null;
                if (tIModelObject.IsAssignableFrom(t)) {
                    if (!allClasses.ContainsKey(t.FullName)) {
                        allClasses[t.FullName] = new ClassInfo();
                        allClasses[t.FullName] = GetClassInfo(t, allClasses, allStructs, allEnums);
                    }
                    res.ObjectMember.Add(new ObjectMember(p.Name, t.FullName, Dimension.Scalar, browseable));
                }
                else if (t.IsGenericType && tIModelObjectEnum.IsAssignableFrom(t)) {
                    t = t.GetGenericArguments()[0];
                    if (!allClasses.ContainsKey(t.FullName)) {
                        allClasses[t.FullName] = new ClassInfo();
                        allClasses[t.FullName] = GetClassInfo(t, allClasses, allStructs, allEnums);
                    }
                    res.ObjectMember.Add(new ObjectMember(p.Name, t.FullName, Dimension.Array, browseable));
                }
                else { // SimpleMember
                    Dimension dim = GetDimensionFromPropertyType(t);
                    if (t.IsGenericType) { // Nullable or List
                        t = t.GetGenericArguments()[0];
                    } else if (t.IsArray) {
                        t = t.GetElementType();
                    }
                    DataType type = DataValue.TypeToDataType(t);
                    string typeConstraints = "";
                    if (type == DataType.Enum || type == DataType.Struct) {
                        typeConstraints = t.FullName;
                    }

                    if (type == DataType.Struct) {
                        if (!allStructs.ContainsKey(t.FullName)) {
                            allStructs[t.FullName] = new StructInfo();
                            allStructs[t.FullName] = GetStructInfo(t, allStructs, allEnums);
                        }
                    }
                    else if (type == DataType.Enum) {
                        if (!allEnums.ContainsKey(t.FullName)) {
                            allEnums[t.FullName] = GetEnumInfo(t);
                        }
                    }

                    object value = p.GetValue(defaultObject, null);
                    DataValue? defaultValue = null;
                    if (value != null) {
                        defaultValue = DataValue.FromObject(value);
                    }
                    Category category = p.GetCustomAttribute<Category>();
                    string cat = category == null ? (defaultCategory == null ? "" : defaultCategory.Name) : category.Name;
                    res.SimpleMember.Add(new SimpleMember(p.Name, type, typeConstraints, dim, defaultValue, browseable, cat));
                }
            }
            return res;
        }

        private static StructInfo GetStructInfo(Type typeClass, Dictionary<string, StructInfo> allStructs, Dictionary<string, EnumInfo> allEnums) {

            var res = new StructInfo();

            res.FullName = typeClass.FullName;
            res.Name = typeClass.Name;

            object defaultObject = Activator.CreateInstance(typeClass);

            PropertyInfo[] properties = typeClass.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo p in properties) {
                Type t = p.PropertyType;
                bool ignore = p.GetCustomAttribute<Ignore>() != null;
                if (ignore) continue;
                Dimension dim = GetDimensionFromPropertyType(t);
                if (t.IsGenericType) { // Nullable or List
                    t = t.GetGenericArguments()[0];
                }
                else if (t.IsArray) {
                    t = t.GetElementType();
                }
                DataType type = DataValue.TypeToDataType(t);
                string typeConstraints = "";
                if (type == DataType.Enum || type == DataType.Struct) {
                    typeConstraints = t.FullName;
                }

                if (type == DataType.Struct) {
                    if (!allStructs.ContainsKey(t.FullName)) {
                        allStructs[t.FullName] = new StructInfo();
                        allStructs[t.FullName] = GetStructInfo(t, allStructs, allEnums);
                    }
                }
                else if (type == DataType.Enum) {
                    if (!allEnums.ContainsKey(t.FullName)) {
                        allEnums[t.FullName] = GetEnumInfo(t);
                    }
                }

                object value = p.GetValue(defaultObject, null);
                DataValue? defaultValue = null;
                if (value != null) {
                    defaultValue = DataValue.FromObject(value);
                }
                res.Member.Add(new SimpleMember(p.Name, type, typeConstraints, dim, defaultValue, false, ""));
            }
            return res;
        }

        private static EnumInfo GetEnumInfo(Type typeEnum) {
            var res = new EnumInfo();
            res.FullName = typeEnum.FullName;
            res.Name = typeEnum.Name;
            Array values = Enum.GetValues(typeEnum);
            foreach(var v in values) {
                string name = v.ToString();
                string desc = name;
                res.Values.Add(new EnumValue(name, desc));
            }
            return res;
        }

        private static Dimension GetDimensionFromPropertyType(Type t) {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                return Dimension.Optional;
            }
            else if (typeof(System.Collections.IList).IsAssignableFrom(t)) {
                return Dimension.Array;
            }
            return Dimension.Scalar;
        }

        public override Task<ObjectInfo[]> GetObjectsByID(ObjectRef[] ids) => Task.FromResult(ids.Where(id => mapObjectInfos.ContainsKey(id)).Select(id => mapObjectInfos[id]).ToArray());

        public override Task<ObjectValue[]> GetObjectValuesByID(ObjectRef[] objectIDs) => Task.FromResult(objectIDs.Where(id => mapObjects.ContainsKey(id)).Select(id => {
            var obj = mapObjects[id];
            var dv = DataValue.FromObject(obj);
            return new ObjectValue(id, dv);
        }).ToArray());

        public override async Task<Result> UpdateConfig(Origin origin, ObjectValue[]? updateOrDeleteObjects, MemberValue[]? updateOrDeleteMembers, AddArrayElement[]? addArrayElements) {

            try {
                var changedObjects = new HashSet<ObjectRef>();

                if (updateOrDeleteObjects != null) {
                    foreach (ObjectValue ov in updateOrDeleteObjects) {
                        if (!mapObjects.ContainsKey(ov.Object)) throw new Exception("Failed to update object " + ov.Object + " because no object found with this id.");
                        IModelObject obj = mapObjects[ov.Object];
                        if (ov.Value.IsEmpty) {
                            var objInfo = mapObjectInfos[ov.Object];
                            MemberRefIdx? parent = objInfo.Parent;
                            if (!parent.HasValue) throw new Exception("Failed to delete object " + ov.Object + " because there is no parent object.");
                            var objParent = mapObjects[parent.Value.Object];
                            objParent.RemoveChildObject(parent.Value.Name, obj);
                            changedObjects.Add(parent.Value.Object);
                        }
                        else {
                            ov.Value.PopulateObject(obj);
                            changedObjects.Add(ov.Object);
                        }
                    }
                }

                if (updateOrDeleteMembers != null) {
                    foreach (MemberValue m in updateOrDeleteMembers) {
                        if (!mapObjects.ContainsKey(m.Member.Object)) throw new Exception("Failed to update member " + m.Member.Name + " because no object found with id: " + m.Member.Object);
                        IModelObject obj = mapObjects[m.Member.Object];
                        obj.SetMemberValue(m.Member.Name, m.Value);
                        changedObjects.Add(m.Member.Object);
                    }
                }

                if (addArrayElements != null) {
                    foreach (AddArrayElement element in addArrayElements) {
                        if (!mapObjects.ContainsKey(element.ArrayMember.Object)) throw new Exception("Failed to add item to member " + element.ArrayMember.Name + " because no object found with id: " + element.ArrayMember.Object);
                        IModelObject obj = mapObjects[element.ArrayMember.Object];
                        obj.AddChildObject(element.ArrayMember.Name, element.ValueToAdd);
                        changedObjects.Add(element.ArrayMember.Object);
                    }
                }

                await OnConfigModelChanged(init: false);
                modelAsString = SerializeModelToString(model);
                WriteConfigFile(modelFileName, modelAsString);
                if (notifier != null) {
                    notifier.Notify_ConfigChanged(changedObjects.ToList());
                }
                return Result.OK;
            }
            catch (Exception exp) {
                model = DeserializeModelFromString(modelAsString); // restore model which might have been partially modified before the exception
                ModifyModelAfterInit();
                await OnConfigModelChanged(init: false);
                return Result.Failure("UpdateConfig failed: " + exp.Message);
            }
        }

        protected async Task<Result> UpdateConfigByStr(string str) {

            try {
                var changedObjects = new List<ObjectRef>();

                model = DeserializeModelFromString(str);
                ModifyModelAfterInit();
                await OnConfigModelChanged(init: false);

                ObjectInfo rootObj = allObjectInfos.FirstOrDefault(obj => obj.Parent == null);
                changedObjects.Add(rootObj.ID);

                modelAsString = str;
                WriteConfigFile(modelFileName, str);
                if (notifier != null) {
                    notifier.Notify_ConfigChanged(changedObjects);
                }
                return Result.OK;
            }
            catch (Exception exp) {
                model = DeserializeModelFromString(modelAsString); // restore model which might have been partially modified before the exception
                ModifyModelAfterInit();
                await OnConfigModelChanged(init: false);
                return Result.Failure("UpdateConfigByStr failed: " + exp.Message);
            }
        }

        public override async Task<Result<DataValue>> OnMethodCall(Origin origin, string methodName, NamedValue[] parameters) {

            switch (methodName) {
                case "GetConfigString": {
                        var res = Result<DataValue>.OK(DataValue.FromString(modelAsString));
                        return res;
                    }

                case "SetConfigString": {

                        if (parameters.Length != 1) {
                            return Result<DataValue>.Failure("Missing parameter: config");
                        }

                        NamedValue parameter = parameters[0];
                        if (parameter.Name != "config") {
                            return Result<DataValue>.Failure("Missing parameter: config");
                        }

                        string modelString = parameter.Value;

                        Result res = await UpdateConfigByStr(modelString);

                        if (res.IsOK) {
                            return Result<DataValue>.OK(DataValue.Empty);
                        }
                        else {
                            return Result<DataValue>.Failure(res.Error ?? "");
                        }
                    }

                default: {
                        return Result<DataValue>.Failure("Method not implemented: " + methodName);
                    }
            }
        }

        public override Task<VTQ[]> ReadVariables(Origin origin, VariableRef[] variables, Duration? timeout) {
            throw new NotImplementedException("ReadVariables not implemented");
        }

        public override Task<WriteResult> WriteVariables(Origin origin, VariableValue[] values, Duration? timeout, bool sync) {
            throw new NotImplementedException("WriteVariables not implemented");
        }
    }


    public enum ModelFormat
    {
        XML,
        JSON
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////


    public abstract class ModelObjectModuleExt<T> : ModelObjectModule<T> where T : IModelObject, new()
    {
        protected Dictionary<ObjectRef, VariableValue[]> varVals = new Dictionary<ObjectRef, VariableValue[]>();

        public override async Task Init(ModuleInitInfo info, VariableValue[] restoreVariableValues, Notifier notifier, ModuleThread moduleThread) {
            await base.Init(info, restoreVariableValues, notifier, moduleThread);
            foreach (VariableValue v in restoreVariableValues) {
                if (varVals.ContainsKey(v.Variable.Object)) {
                    VariableValue[] values = varVals[v.Variable.Object];
                    for (int i = 0; i < values.Length; ++i) {
                        if (values[i].Variable == v.Variable) {
                            values[i].Value = v.Value;
                            break;
                        }
                    }
                }
            }
        }

        protected override async Task OnConfigModelChanged(bool init) {

            await base.OnConfigModelChanged(init);

            var t = Timestamp.Now.TruncateMilliseconds();
            var newVarVals = new Dictionary<ObjectRef, VariableValue[]>();
            foreach (var entry in mapObjectInfos) {
                ObjectInfo obj = entry.Value;
                ObjectRef id = obj.ID;
                Variable[] variables = obj.Variables;

                VariableValue[]? existingValues = null;
                if (varVals.ContainsKey(id)) {
                    existingValues = varVals[id];
                }
                int VarLen = variables == null ? 0 : variables.Length;
                if (existingValues != null || VarLen > 0) {
                    VariableValue[] newValues = new VariableValue[VarLen];
                    for (int i = 0; i < VarLen; ++i) {
                        Variable variable = variables![i];
                        VTQ vtq = new VTQ(t, Quality.Bad, variable.DefaultValue);
                        if (existingValues != null) {
                            foreach (VariableValue vv in existingValues) {
                                if (vv.Variable.Name == variable.Name) {
                                    vtq = vv.Value;
                                    break;
                                }
                            }
                        }
                        newValues[i] = VariableValue.Make(id, variable.Name, vtq);
                    }
                    newVarVals[id] = newValues;
                }
            }
            this.varVals = newVarVals;
        }

        public override Task<VTQ[]> ReadVariables(Origin origin, VariableRef[] variables, Duration? timeout) {
            VTQ[] result = new VTQ[variables.Length];
            for (int i = 0; i < variables.Length; ++i) {
                result[i] = new VTQ(Timestamp.Empty, Quality.Bad, DataValue.Empty);
                VariableRef vr = variables[i];
                if (varVals.ContainsKey(vr.Object)) {
                    VariableValue[] values = varVals[vr.Object];
                    foreach (VariableValue val in values) {
                        if (val.Variable.Name == vr.Name) {
                            result[i] = val.Value;
                            break;
                        }
                    }
                }
            }
            return Task.FromResult(result);
        }

        public override Task<WriteResult> WriteVariables(Origin origin, VariableValue[] values, Duration? timeout, bool sync) {
            var failures = new List<VariableError>();
            foreach (VariableValue vv in values) {
                string? error = null;
                if (varVals.ContainsKey(vv.Variable.Object)) {
                    VariableValue[] currentValues = varVals[vv.Variable.Object];
                    bool variableFound = false;
                    for (int i = 0; i < currentValues.Length; ++i) {
                        if (currentValues[i].Variable == vv.Variable) {
                            currentValues[i] = vv;
                            varVals[vv.Variable.Object] = currentValues;
                            variableFound = true;
                            break;
                        }
                    }
                    if (!variableFound) {
                        error = "No variable found with name " + vv.Variable;
                    }
                }
                else {
                    error = "No object found with id " + vv.Variable.Object.ToString();
                }

                if (error != null) {
                    failures.Add(new VariableError(vv.Variable, error));
                }
            }
            return Task.FromResult(failures.Count == 0 ? WriteResult.OK : WriteResult.Failure(failures.ToArray()));
        }
    }
}
