// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Dashboard
{
    [Identify(id: "ModuleVariables", bundle: "Generic", path: "variables.html", icon: "mdi-table-large")]
    public class View_ModuleVariables : ViewBase
    {
        private string activeModuleID = "";
        private ModuleInfo[] modules = new ModuleInfo[0];
        private readonly Dictionary<ObjectRef, ObjectInfo> mapObjectToObjectInfo = new Dictionary<ObjectRef, ObjectInfo>();
        private readonly Dictionary<VariableRef, int> mapIdx = new Dictionary<VariableRef, int>();

        public override async Task OnActivate() {
            string[] exclude = new string[0];
            if (Config.NonEmpty) {
                var vc = Config.Object<ViewConfig>() ?? new ViewConfig();
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

                    var pars = parameters.Object<ReadModuleVariables_Params>() ?? throw new Exception("ReadModuleVariables_Params is null");
                    string moduleID = string.IsNullOrEmpty(pars.ModuleID) ? modules[0].ID : pars.ModuleID;

                    ObjectInfo? rootObjInfo;
                    while (true) {
                        rootObjInfo = await Connection.GetRootObject(moduleID);
                        if (rootObjInfo != null) break;
                        await Task.Delay(500);
                    }

                    ObjectRef rootObj = rootObjInfo.ID;

                    var objects = await Connection.GetAllObjects(moduleID);
                    SetObjectNameMap(objects);

                    List<VariableValue> values = await Connection.ReadAllVariablesOfObjectTree(rootObj);

                    await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), rootObj);

                    VarEntry[] entries = values.Select(MapVarValue).ToArray();

                    activeModuleID = moduleID;

                    var locations = await Connection.GetLocations();

                    var result = new ReadModuleVariables_Result() {
                        Modules = modules,
                        ModuleID = moduleID,
                        ModuleName = modules.FirstOrDefault(m => m.ID == moduleID).Name,
                        Variables = entries,
                        Locations = locations.ToArray(),
                    };

                    mapIdx.Clear();
                    for (int n = 0; n < values.Count; ++n) {
                        mapIdx[values[n].Variable] = n;
                    }

                    return ReqResult.OK(result);

                case "WriteVariable": {

                        var write = parameters.Object<WriteVariable_Params>() ?? throw new Exception("WriteVariable_Params is null");
                        VTQ vtq = new VTQ(Timestamp.Now, Quality.Good, DataValue.FromJSON(write.V));
                        await Connection.WriteVariable(ObjectRef.FromEncodedString(write.ObjID), write.Var, vtq);
                        return ReqResult.OK();
                    }

                case "SyncRead": {

                        var sread = parameters.Object<ReadSync_Params>() ?? throw new Exception("ReadSync_Params is null");
                        ObjectRef obj = ObjectRef.FromEncodedString(sread.ObjID);
                        VariableRef varRef = VariableRef.Make(obj, sread.Var);
                        VTQ vtq = await Connection.ReadVariableSync(varRef);
                        int idx = mapIdx[varRef];
                        var entry = new ChangeEntry() {
                            N = idx,
                            V = vtq.V,
                            T = Timestamp2Str(vtq.T),
                            Q = vtq.Q
                        };
                        return ReqResult.OK(entry);
                    }
                default:
                    return ReqResult.Bad("Unknown command: " + command);
            }
        }

        public override async Task OnVariableValueChanged(List<VariableValue> variables) {

            var changes = new List<ChangeEntry>(variables.Count);

            for (int n = 0; n < variables.Count; ++n) {
                VariableValue vv = variables[n];
                try {
                    int idx = mapIdx[vv.Variable];
                    changes.Add(new ChangeEntry() {
                        N = idx,
                        V = vv.Value.V,
                        T = Timestamp2Str(vv.Value.T),
                        Q = vv.Value.Q
                    });
                }
                catch (Exception) { }
            }

            await Context.SendEventToUI("Change", changes);
        }

        private void SetObjectNameMap(IEnumerable<ObjectInfo> objects) {
            mapObjectToObjectInfo.Clear();
            foreach (var obj in objects) {
                mapObjectToObjectInfo[obj.ID] = obj;
            }
        }

        private VarEntry MapVarValue(VariableValue vv) {
            ObjectRef obj = vv.Variable.Object;
            ObjectInfo? info = mapObjectToObjectInfo.ContainsKey(obj) ? mapObjectToObjectInfo[obj] : null;
            string varName = vv.Variable.Name;
            Variable? variable = info?.Variables.FirstOrDefault(v => v.Name == varName);
            LocationRef? loc = info?.Location;

            return new VarEntry() {
                ID = obj.ToString() + "___" + vv.Variable.Name,
                ObjID = obj.ToString(),
                Obj = info?.Name ?? "???",
                Loc = loc.HasValue ? loc.Value.LocationID : "",
                Var = vv.Variable.Name,
                V = vv.Value.V,
                T = Timestamp2Str(vv.Value.T),
                Q = vv.Value.Q,
                Type = variable?.Type ?? DataType.JSON,
                Dimension = variable?.Dimension ?? 1,
                SyncReadable = variable?.SyncReadable ?? false,
                Writable = variable?.Writable ?? false
            };
        }

        private string Timestamp2Str(Timestamp t) {
            return t.ToString().Replace('T', '\u00A0');
        }

        public class ReadModuleVariables_Params
        {
            public string ModuleID { get; set; } = "";
        }

        public class WriteVariable_Params
        {
            public string ObjID { get; set; } = "";
            public string Var { get; set; } = "";
            public string V { get; set; } = "";
        }

        public class ReadSync_Params
        {
            public string ObjID { get; set; } = "";
            public string Var { get; set; } = "";
        }

        public class ReadModuleVariables_Result {
            public ModuleInfo[] Modules = new ModuleInfo[0];
            public string ModuleID { get; set; } = "";
            public string ModuleName { get; set; } = "";
            public VarEntry[] Variables { get; set; } = new VarEntry[0];
            public LocationInfo[] Locations { get; set; } = new LocationInfo[0];
        }

        public class ModuleInfo
        {
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public class VarEntry
        {
            public string ID { get; set; } = "";
            public string ObjID { get; set; } = "";
            public string Obj { get; set; } = "";
            public string Loc { get; set; } = "";
            public string Var { get; set; } = "";
            public DataType Type { get; set; }
            public int Dimension { get; set; }
            public bool Writable { get; set; }
            public bool SyncReadable { get; set; }
            public DataValue V { get; set; }
            public string T { get; set; } = "";
            public Quality Q { get; set; }
        }

        public class ChangeEntry
        {
            public int N { get; set; }
            public DataValue V { get; set; }
            public string T { get; set; } = "";
            public Quality Q { get; set; }
        }

        public class ViewConfig
        {
            public string[] ExcludeModules { get; set; } = new string[0];
        }
    }
}
