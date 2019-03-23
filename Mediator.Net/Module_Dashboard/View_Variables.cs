// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    [Identify(id: "ModuleVariables", bundle: "Generic", path: "variables.html")]
    public class View_ModuleVariables : ViewBase
    {
        private string activeModuleID = "";
        private ModuleInfo[] modules = new ModuleInfo[0];
        private readonly Dictionary<ObjectRef, string> mapObjectToName = new Dictionary<ObjectRef, string>();
        private readonly Dictionary<VariableRef, int> mapIdx = new Dictionary<VariableRef, int>();

        public override async Task OnActivate() {
            string[] exclude = new string[0];
            if (Config.NonEmpty) {
                var vc = Config.Object<ViewConfig>();
                exclude = vc.ExcludeModules;
            }
            var mods = await Connection.GetModules();
            modules = mods
                .Where(m => !exclude.Contains(m.ID))
                .Select(m => new ModuleInfo() {
                    ID = m.ID,
                    Name = m.Name
                }).ToArray();
        }

        public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

            switch (command) {

                case "ReadModuleVariables":

                    if (activeModuleID != "") {
                        await Connection.DisableChangeEvents();
                    }

                    var pars = parameters.Object<ReadModuleVariables_Params>();
                    string moduleID = string.IsNullOrEmpty(pars.ModuleID) ? modules[0].ID : pars.ModuleID;

                    ObjectInfo rootObjInfo = await Connection.GetRootObject(moduleID);
                    ObjectRef rootObj = rootObjInfo.ID;

                    ObjectInfo[] objects = await Connection.GetAllObjects(moduleID);
                    SetObjectNameMap(objects);

                    VariableValue[] values = await Connection.ReadAllVariablesOfObjectTree(rootObj);

                    await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), rootObj);

                    VarEntry[] entries = values.Select(MapVarValue).ToArray();

                    activeModuleID = moduleID;

                    var result = new ReadModuleVariables_Result() {
                        Modules = modules,
                        ModuleID = moduleID,
                        ModuleName = modules.FirstOrDefault(m => m.ID == moduleID).Name,
                        Variables = entries
                    };

                    mapIdx.Clear();
                    for (int n = 0; n < values.Length; ++n) {
                        mapIdx[values[n].Variable] = n;
                    }

                    return ReqResult.OK(result);

                case "WriteVariable":

                    var write = parameters.Object<WriteVariable_Params>();
                    VTQ vtq = new VTQ(Timestamp.Now, Quality.Good, DataValue.FromJSON(write.V));
                    await Connection.WriteVariable(ObjectRef.FromEncodedString(write.ObjID), write.Var, vtq);
                    return ReqResult.OK();

                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        public override void OnVariableValueChanged(VariableValue[] variables) {

            var changes = new List<ChangeEntry>(variables.Length);

            for (int n = 0; n < variables.Length; ++n) {
                VariableValue vv = variables[n];
                try {
                    int idx = mapIdx[vv.Variable];
                    changes.Add(new ChangeEntry() {
                        N = idx,
                        V = vv.Value.V,
                        T = vv.Value.T,
                        Q = vv.Value.Q
                    });
                }
                catch (Exception) { }
            }

            Context.SendEventToUI("Change", changes);
        }

        private void SetObjectNameMap(ObjectInfo[] objects) {
            mapObjectToName.Clear();
            foreach (var obj in objects) {
                mapObjectToName[obj.ID] = obj.Name;
            }
        }

        private VarEntry MapVarValue(VariableValue vv) {
            ObjectRef obj = vv.Variable.Object;
            string name = mapObjectToName.ContainsKey(obj) ? mapObjectToName[obj] : "???";
            return new VarEntry() {
                ObjID = obj.ToString(),
                Obj = name,
                Var = vv.Variable.Name,
                V = vv.Value.V,
                T = vv.Value.T,
                Q = vv.Value.Q
            };
        }

        public class ReadModuleVariables_Params
        {
            public string ModuleID { get; set; } = "";
        }

        public class WriteVariable_Params
        {
            public string ObjID { get; set; }
            public string Var { get; set; }
            public string V { get; set; }
        }

        public class ReadModuleVariables_Result {
            public ModuleInfo[] Modules = new ModuleInfo[0];
            public string ModuleID { get; set; } = "";
            public string ModuleName { get; set; } = "";
            public VarEntry[] Variables { get; set; } = new VarEntry[0];
        }

        public class ModuleInfo
        {
            public string ID { get; set; }
            public string Name { get; set; }
        }

        public class VarEntry
        {
            public string ObjID { get; set; }
            public string Obj { get; set; }
            public string Var { get; set; }
            public DataValue V { get; set; }
            public Timestamp T { get; set; }
            public Quality Q { get; set; }
        }

        public class ChangeEntry
        {
            public int N { get; set; }
            public DataValue V { get; set; }
            public Timestamp T { get; set; }
            public Quality Q { get; set; }
        }

        public class ViewConfig
        {
            public string[] ExcludeModules { get; set; } = new string[0];
        }
    }
}
