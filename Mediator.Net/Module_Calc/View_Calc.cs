// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Dashboard;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Ifak.Fast.Json.Linq;
using System.Collections.Generic;
using Ifak.Fast.Mediator.Calc.Config;

namespace Ifak.Fast.Mediator.Calc
{

    [Dashboard.Identify(id: "Calc", bundle: "Generic", path: "calc.html", icon: "mdi-powershell")]
    public class View_Calc : ViewBase
    {
        private ViewConfig configuration = new ViewConfig();
        private ObjectRef RootID;
        private string moduleID = "";

        public override Task OnActivate() {
            if (Config.NonEmpty) {
                configuration = Config.Object<ViewConfig>();
            }
            return Task.FromResult(true);
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            bool hasModuleID = !(configuration == null || string.IsNullOrEmpty(configuration.ModuleID));
            moduleID = hasModuleID ? configuration.ModuleID : "CALC";

            switch (command) {
                case "GetModel": {

                        ObjectInfo root = await Connection.GetRootObject(moduleID);
                        RootID = root.ID;

                        await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), root.ID);

                        return await GetModelResult();
                    }

                case "Save": {

                        SaveParams saveParams = parameters.Object<SaveParams>();
                        string objID = saveParams.ID;
                        IDictionary<string, JToken> dict = saveParams.Obj;
                        MemberValue[] members = dict
                            .Where(kv => kv.Key != "ID")
                            .Select(entry => MakeMemberValue(objID, entry))
                            .ToArray();
                        await Connection.UpdateConfig(members);

                        return await GetModelResult();
                    }

                case "Delete": {

                        ObjectRef obj = ObjectRef.Make(moduleID, parameters.GetString());
                        await Connection.UpdateConfig(ObjectValue.Make(obj, DataValue.Empty));

                        return await GetModelResult();
                    }

                case "AddObject": {

                        AddObjectParams addParams = parameters.Object<AddObjectParams>();
                        ObjectRef objParent = ObjectRef.Make(moduleID, addParams.ParentObjID);
                        DataValue dataValue = DataValue.FromObject(new {
                            ID = addParams.NewObjID,
                            Name = addParams.NewObjName
                        });
                        var element = AddArrayElement.Make(objParent, addParams.ParentMember, dataValue);
                        await Connection.UpdateConfig(element);

                        return await GetModelResult();
                    }

                case "MoveObject": {

                        var move = parameters.Object<MoveObject_Params>();
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

                        var pars = parameters.Object<ReadModuleObjectsParams>();

                        ObjectInfo[] objects;

                        try {
                            objects = await Connection.GetAllObjects(pars.ModuleID);
                        }
                        catch (Exception) {
                            objects = new ObjectInfo[0];
                        }

                        Func<Variable, bool> isMatch = GetMatchPredicate(pars.ForType);

                        return ReqResult.OK(new {
                            Items = objects.Where(o => o.Variables.Any(isMatch)).Select(o => new {
                                Type = o.ClassName,
                                ID = o.ID.ToEncodedString(),
                                Name = o.Name,
                                Variables = o.Variables.Where(isMatch).Select(v => v.Name).ToArray()
                            })
                        });
                    }

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        private async Task<ReqResult> GetModelResult() {

            ObjectValue v = await Connection.GetObjectValueByID(RootID);
            Calc_Model model = v.ToObject<Calc_Model>();

            ObjectRef[] objects = model.GetAllCalculations().SelectMany(GetObjects).Distinct().ToArray();
            ObjectInfo[] infos;
            try {
                infos = await Connection.GetObjectsByID(objects);
            }
            catch (Exception) {
                infos = new ObjectInfo[objects.Length];
                for (int i = 0; i < objects.Length; ++i) {
                    ObjectRef obj = objects[i];
                    try {
                        infos[i] = await Connection.GetObjectByID(obj);
                    }
                    catch (Exception) {
                        infos[i] = new ObjectInfo(obj, "???", "???");
                    }
                }
            }

            var objectInfos = new List<ObjInfo>();
            foreach (ObjectInfo info in infos) {
                var numericVariables = info.Variables.Where(IsNumericBoolOrStruct).Select(v => v.Name).ToArray();
                objectInfos.Add(new ObjInfo() {
                    ID = info.ID.ToEncodedString(),
                    Name = info.Name,
                    Variables = numericVariables
                });
            }

            var mods = await Connection.GetModules();

            VariableValue[] variables = await Connection.ReadAllVariablesOfObjectTree(RootID);
            var changes = VarValsToEventEntries(variables);

            DataValue resGetAdapterInfo = await Connection.CallMethod(moduleID, "GetAdapterInfo");
            AdapterInfo[] adapterTypesInfo = resGetAdapterInfo.Object<AdapterInfo[]>();

            var res = new {
                model = model,
                objectInfos = objectInfos,
                moduleInfos = mods.Select(m => new {
                    ID = m.ID,
                    Name = m.Name
                }).ToArray(),
                variableValues = changes,
                adapterTypesInfo = adapterTypesInfo,
            };

            return ReqResult.OK(res, ignoreShouldSerializeMembers: true);
        }

        private ObjectRef[] GetObjects(Config.Calculation c) {
            var res = new List<ObjectRef>();
            foreach (var input in c.Inputs) {
                if (input.Variable.HasValue) {
                    res.Add(input.Variable.Value.Object);
                }
            }
            foreach (var output in c.Outputs) {
                if (output.Variable.HasValue) {
                    res.Add(output.Variable.Value.Object);
                }
            }
            return res.ToArray();
        }

        private static Func<Variable, bool> GetMatchPredicate(DataType type) {
            if (type.IsNumeric() || type == DataType.Bool) {
                return (v) => v.IsNumeric || v.Type == DataType.Bool;
            }
            return (v) => v.Type == type;
        }

        private static bool IsNumericBoolOrStruct(Variable v) => v.IsNumeric || v.Type == DataType.Bool || v.Type == DataType.Struct;

        private MemberValue MakeMemberValue(string id, KeyValuePair<string, JToken> entry) {
            JToken value = entry.Value;
            DataValue dataValue = DataValue.FromObject(value);
            // Console.WriteLine($"{id}.{entry.Key}: {dataValue.ToString()}");
            return MemberValue.Make(moduleID, id, entry.Key, dataValue);
        }

        public override async Task OnVariableValueChanged(VariableValue[] variables) {
            var changes = VarValsToEventEntries(variables);
            await Context.SendEventToUI("VarChange", changes);
        }

        private static IList<EventEntry> VarValsToEventEntries(VariableValue[] variables) {
            var changes = new List<EventEntry>(variables.Length);
            for (int n = 0; n < variables.Length; ++n) {
                VariableValue vv = variables[n];
                changes.Add(new EventEntry() {
                    Key = vv.Variable.Object.LocalObjectID,
                    V = vv.Value.V,
                    T = FormatTime(vv.Value.T),
                    Q = vv.Value.Q
                });
            }
            return changes;
        }

        private static string FormatTime(Timestamp t) {
            DateTime tLocal = t.ToDateTime().ToLocalTime();
            int millis = tLocal.Millisecond;
            if (millis != 0)
                return tLocal.ToString("HH':'mm':'ss'.'fff", CultureInfo.InvariantCulture);
            else
                return tLocal.ToString("HH':'mm':'ss", CultureInfo.InvariantCulture);
        }

        public class ViewConfig
        {
            public string ModuleID { get; set; }
        }

        public class SaveParams
        {
            public string ID { get; set; }

            public JObject Obj { get; set; }
        }

        public class AddObjectParams
        {
            public string ParentObjID { get; set; }
            public string ParentMember { get; set; }
            public string NewObjID { get; set; }
            public string NewObjType { get; set; }
            public string NewObjName { get; set; }
        }

        public class MoveObject_Params
        {
            public string ObjID { get; set; }
            public bool Up { get; set; }
        }

        public class ObjInfo
        {
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
            public string[] Variables { get; set; } = new string[0];
        }

        public class ReadModuleObjectsParams
        {
            public string ModuleID { get; set; } = "";
            public DataType ForType { get; set; } = DataType.Float64;
        }

        public class EventEntry
        {
            public string Key { get; set; }
            public DataValue V { get; set; }
            public string T { get; set; }
            public Quality Q { get; set; }
        }

        public class AdapterInfo
        {
            public string Type { get; set; }
            public bool Show_WindowVisible { get; set; }
            public bool Show_Definition { get; set; }
            public string DefinitionLabel { get; set; }
            public bool DefinitionIsCode { get; set; }
        }
    }
}
