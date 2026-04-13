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
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;

namespace Ifak.Fast.Mediator.Calc;

[Dashboard.Identify(id: "Calc", configType: typeof(ViewConfig), icon: "mdi-powershell")]
public class View_Calc : ViewBase
{
    private ViewConfig configuration = new ViewConfig();
    private ObjectRef RootID;
    private string moduleID = "";

    private uint calclog_SinceCounter = 0;
    private int calclog_Generation = 0;
    private bool calclog_Polling = false;
    private string calcRuntimeStates_Snapshot = "";
    private int calcRuntimeStates_Generation = 0;
    private bool calcRuntimeStates_Polling = false;

    public override Task OnActivate() {
        if (Config.NonEmpty) {
            configuration = Config.Object<ViewConfig>() ?? new ViewConfig();
        }
        return Task.FromResult(true);
    }

    public override Task OnDeactivate() {
        calclog_Generation++;
        calclog_Polling = false;
        calcRuntimeStates_Generation++;
        calcRuntimeStates_Polling = false;
        return Task.FromResult(true);
    }

    public override async Task<ReqResult> OnUiRequestAsync(string command, DataValue parameters) {

        bool hasModuleID = !string.IsNullOrEmpty(configuration.ModuleID);
        moduleID = hasModuleID ? configuration.ModuleID : "CALC";

        switch (command) {
            case "GetModel": {

                    ObjectInfo root = await Connection.GetRootObject(moduleID);
                    RootID = root.ID;

                    await Connection.EnableVariableValueChangedEvents(SubOptions.AllUpdates(sendValueWithEvent: true), root.ID);
                    StartCalcRuntimeStatesPolling();

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
                    Context.NotifyRefreshConcurrentViews();
                    return await GetModelResult();
                }

            case "Delete": {

                    ObjectRef obj = ObjectRef.Make(moduleID, parameters.GetString() ?? "");
                    await Connection.UpdateConfig(ObjectValue.Make(obj, DataValue.Empty));
                    Context.NotifyRefreshConcurrentViews();
                    return await GetModelResult();
                }

            case "ResetVariables": {

                    ObjectRef obj = ObjectRef.Make(moduleID, parameters.GetString() ?? "");
                    await Connection.ResetAllVariablesOfObjectTree(obj);
                    return ReqResult.OK();
                }

            case "AddObject": {

                    AddObjectParams addParams = parameters.Object<AddObjectParams>() ?? throw new Exception("AddObjectParams is null");
                    ObjectRef objParent = ObjectRef.Make(moduleID, addParams.ParentObjID);
                    DataValue dataValue = DataValue.FromObject(new {
                        ID = addParams.NewObjID,
                        Name = addParams.NewObjName
                    });
                    var element = AddArrayElement.Make(objParent, addParams.ParentMember, dataValue);
                    await Connection.UpdateConfig(element);
                    Context.NotifyRefreshConcurrentViews();
                    return await GetModelResult();
                }

            case "DragDrop": {

                    DragDropParams dropParams = parameters.Object<DragDropParams>() ?? throw new Exception("DragDropParams is null");

                    ObjectRef obj = ObjectRef.Make(moduleID, dropParams.FromID);
                    ObjectValue objValue = await Connection.GetObjectValueByID(obj);

                    var deleteObj = ObjectValue.Make(obj, DataValue.Empty);

                    ObjectRef objParent = ObjectRef.Make(moduleID, dropParams.ToID);
                    var addElement = AddArrayElement.Make(objParent, dropParams.ToArray, objValue.Value);

                    await Connection.UpdateConfig([deleteObj], [], [addElement]);
                    Context.NotifyRefreshConcurrentViews();
                    return await GetModelResult();
                }

            case "MoveObject": {

                    var move = parameters.Object<MoveObject_Params>() ?? throw new Exception("MoveObject_Params is null");
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
                    Context.NotifyRefreshConcurrentViews();
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

                    Func<Variable, bool> isMatch = GetMatchPredicate(pars.ForType);

                    return ReqResult.OK(new {
                        Items = objects.Where(o => o.Variables.Any(isMatch)).Select(o => new {
                            Type = o.ClassNameFull,
                            ID = o.ID.ToEncodedString(),
                            Name = o.Name,
                            Variables = o.Variables.Where(isMatch).Select(v => v.Name).ToArray()
                        }).ToArray()
                    });
                }

            case "ReadVariableHistory": {

                    var pars = parameters.Object<ReadVariableHistoryParams>() ?? throw new Exception("ReadVariableHistoryParams is null");
                    ObjectRef obj = ObjectRef.Make(moduleID, pars.ObjectID);
                    VariableRef varRef = VariableRef.Make(obj, pars.VariableName);
                    Timestamp tStart = Timestamp.FromJavaTicks(pars.StartJavaTicks);
                    Timestamp tEnd = pars.EndJavaTicks <= 0 ? Timestamp.Max : Timestamp.FromJavaTicks(pars.EndJavaTicks);
                    BoundingMethod bounding = pars.Mode == "Last" ? BoundingMethod.TakeLastN : BoundingMethod.TakeFirstN;
                    var data = await Connection.HistorianReadRaw(varRef, tStart, tEnd, pars.Count, bounding);
                    var rows = data.Select(d => new {
                        T = FormatTimestampFull(d.T),
                        TJ = d.T.JavaTicks,
                        Q = d.Q.ToString(),
                        V = d.V.JSON
                    }).ToArray();
                    return ReqResult.OK(rows);
                }

            case "CountVariableHistory": {

                    var pars = parameters.Object<CountVariableHistoryParams>() ?? throw new Exception("CountVariableHistoryParams is null");
                    ObjectRef obj = ObjectRef.Make(moduleID, pars.ObjectID);
                    VariableRef varRef = VariableRef.Make(obj, pars.VariableName);
                    long count = await Connection.HistorianCount(varRef, Timestamp.Empty, Timestamp.Max);
                    return ReqResult.OK(new { Count = count });
                }

            case "StartCalcLogWatch": {
                    var pars = parameters.Object<StartCalcLogWatchParams>() ?? throw new Exception("StartCalcLogWatchParams is null");
                    calclog_Polling = false;
                    calclog_Generation++;
                    int currentGeneration = calclog_Generation;
                    DataValue result = await Connection.CallMethod(moduleID, "GetCalcLog", new NamedValue("CalcID", pars.CalcID));
                    if (currentGeneration != calclog_Generation) {
                        return ReqResult.OK(Array.Empty<LogEntry>()); // A new watch was started in the meantime, so we ignore this result
                    }
                    LogEntry[] entries = result.Object<LogEntry[]>() ?? [];
                    calclog_SinceCounter = entries.Length > 0 ? entries[^1].ID : 0;
                    calclog_Polling = true;
                    _ = PollCalcLog(pars.CalcID, currentGeneration);
                    return ReqResult.OK(entries);
                }

            case "StopCalcLogWatch": {
                    calclog_Generation++;
                    calclog_Polling = false;
                    return ReqResult.OK();
                }

            case "GetCalcLog": {
                    var pars = parameters.Object<GetCalcLogParams>() ?? throw new Exception("GetCalcLogParams is null");
                    DataValue result = await Connection.CallMethod(moduleID, "GetCalcLog", new NamedValue("CalcID", pars.CalcID));
                    return ReqResult.OK(result);
                }

            case "ClearCalcLog": {
                    var pars = parameters.Object<ClearCalcLogParams>() ?? throw new Exception("ClearCalcLogParams is null");
                    await Connection.CallMethod(moduleID, "ClearCalcLog", new NamedValue("CalcID", pars.CalcID));
                    return ReqResult.OK();
                }

            default:
                return ReqResult.Bad("Unknown command: " + command);
        }
    }

    private async Task<ReqResult> GetModelResult() {

        ObjectValue v = await Connection.GetObjectValueByID(RootID);
        Calc_Model model = v.ToObject<Calc_Model>() ?? new Calc_Model();

        ObjectRef[] objects = model.GetAllCalculations().SelectMany(GetObjects).Distinct().ToArray();
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
            var numericVariables = info.Variables.Select(v => v.Name).ToArray();
            objectInfos.Add(new ObjInfo() {
                ID = info.ID.ToEncodedString(),
                Name = info.Name,
                Variables = numericVariables
            });
        }

        var mods = await Connection.GetModules();

        List<VariableValue> variables = await Connection.ReadAllVariablesOfObjectTree(RootID);
        var changes = VarValsToEventEntries(variables);

        DataValue resGetAdapterInfo = await Connection.CallMethod(moduleID, "GetAdapterInfo");
        AdapterInfo[] adapterTypesInfo = resGetAdapterInfo.Object<AdapterInfo[]>() ?? new AdapterInfo[0];
        CalcRuntimeStateInfo[] calcRuntimeStates;
        try {
            calcRuntimeStates = await ReadCalcRuntimeStates();
        }
        catch (Exception) {
            calcRuntimeStates = [];
        }
        calcRuntimeStates_Snapshot = MakeCalcRuntimeStatesSnapshot(calcRuntimeStates);

        model.Normalize(adapterTypesInfo);

        var res = new {
            model = model,
            objectInfos = objectInfos,
            moduleInfos = mods.Select(m => new {
                ID = m.ID,
                Name = m.Name
            }).ToArray(),
            variableValues = changes,
            calcRuntimeStates = calcRuntimeStates,
            adapterTypesInfo = adapterTypesInfo,
        };

        return ReqResult.OK(res, ignoreShouldSerializeMembers: true);
    }

    private void StartCalcRuntimeStatesPolling() {
        calcRuntimeStates_Generation++;
        calcRuntimeStates_Polling = true;
        int currentGeneration = calcRuntimeStates_Generation;
        _ = PollCalcRuntimeStates(currentGeneration);
    }

    private async Task PollCalcRuntimeStates(int generation) {
        while (calcRuntimeStates_Polling && calcRuntimeStates_Generation == generation) {
            try { await Task.Delay(1000); } catch (Exception) { }
            if (!calcRuntimeStates_Polling || calcRuntimeStates_Generation != generation) break;
            try {
                CalcRuntimeStateInfo[] calcRuntimeStates = await ReadCalcRuntimeStates();
                string snapshot = MakeCalcRuntimeStatesSnapshot(calcRuntimeStates);
                if (snapshot != calcRuntimeStates_Snapshot) {
                    calcRuntimeStates_Snapshot = snapshot;
                    await Context.SendEventToUI("CalcRuntimeStatesUpdate", calcRuntimeStates);
                }
            }
            catch (Exception) { }
        }
    }

    private async Task<CalcRuntimeStateInfo[]> ReadCalcRuntimeStates() {
        DataValue result = await Connection.CallMethod(moduleID, "GetCalcRuntimeStates");
        return result.Object<CalcRuntimeStateInfo[]>() ?? [];
    }

    private static string MakeCalcRuntimeStatesSnapshot(CalcRuntimeStateInfo[] calcRuntimeStates) {
        return DataValue.FromObject(calcRuntimeStates).JSON;
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

    // private static bool IsNumericBoolOrStruct(Variable v) => v.IsNumeric || v.Type == DataType.Bool || v.Type == DataType.Struct;

    private MemberValue MakeMemberValue(string id, KeyValuePair<string, JToken?> entry) {
        JToken? value = entry.Value;
        DataValue dataValue = value == null ? DataValue.Empty : DataValue.FromObject(value);
        // Console.WriteLine($"{id}.{entry.Key}: {dataValue.ToString()}");
        return MemberValue.Make(moduleID, id, entry.Key, dataValue);
    }

    public override async Task OnVariableValueChanged(List<VariableValue> variables) {
        var changes = VarValsToEventEntries(variables);
        await Context.SendEventToUI("VarChange", changes);
    }

    private async Task PollCalcLog(string calcID, int generation) {
        while (calclog_Polling && calclog_Generation == generation) {
            try { await Task.Delay(2000); } catch (Exception) { }
            if (!calclog_Polling || calclog_Generation != generation) break;
            try {
                DataValue result = await Connection.CallMethod(moduleID, "GetCalcLog",
                    new NamedValue("CalcID", calcID),
                    new NamedValue("SinceCounter", calclog_SinceCounter.ToString()));
                if (!calclog_Polling || calclog_Generation != generation) break;
                LogEntry[] entries = result.Object<LogEntry[]>() ?? [];
                if (entries.Length > 0) {
                    calclog_SinceCounter = entries[^1].ID;
                    var eventPayload = new {
                        CalcID = calcID,
                        Entries = entries
                    };
                    await Context.SendEventToUI("CalcLogUpdate", eventPayload);
                }
            }
            catch (Exception) { }
        }
    }

    private static List<EventEntry> VarValsToEventEntries(List<VariableValue> variables) {
        var changes = new List<EventEntry>(variables.Count);
        for (int n = 0; n < variables.Count; ++n) {
            VariableValue vv = variables[n];
            changes.Add(new EventEntry(
                key: vv.Variable.Object.LocalObjectID,
                v: vv.Value.V,
                t: FormatTime(vv.Value.T),
                q: vv.Value.Q));
        }
        return changes;
    }

    private static string FormatTime(Timestamp t) {
        DateTime tLocal = AppTimeZone.ConvertToLocalTime(t);
        int millis = tLocal.Millisecond;
        if (millis != 0)
            return tLocal.ToString("HH':'mm':'ss'.'fff", CultureInfo.InvariantCulture);
        else
            return tLocal.ToString("HH':'mm':'ss", CultureInfo.InvariantCulture);
    }

    private static string FormatTimestampFull(Timestamp t) {
        DateTime tLocal = AppTimeZone.ConvertToLocalTime(t);
        if (tLocal.Millisecond != 0)
            return tLocal.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss'.'fff", CultureInfo.InvariantCulture);
        else
            return tLocal.ToString("yyyy'-'MM'-'dd' 'HH':'mm':'ss", CultureInfo.InvariantCulture);
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

    public class MoveObject_Params
    {
        public string ObjID { get; set; } = "";
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

    public class ReadVariableHistoryParams
    {
        public string ObjectID { get; set; } = "";
        public string VariableName { get; set; } = "Value";
        public int Count { get; set; } = 20;
        public string Mode { get; set; } = "First";
        public long StartJavaTicks { get; set; } = 0;
        public long EndJavaTicks { get; set; } = 0;
    }

    public class CountVariableHistoryParams
    {
        public string ObjectID { get; set; } = "";
        public string VariableName { get; set; } = "Value";
    }

    public class GetCalcLogParams
    {
        public string CalcID { get; set; } = "";
    }

    public class ClearCalcLogParams
    {
        public string CalcID { get; set; } = "";
    }

    public class StartCalcLogWatchParams
    {
        public string CalcID { get; set; } = "";
    }

    public class CalcRuntimeStateInfo
    {
        public string CalcID { get; set; } = "";
        public string State { get; set; } = "";
        public bool IsRunning { get; set; }
        public int RunningForSeconds { get; set; }
        public bool LastRunFailed { get; set; }
    }

    public class EventEntry
    {
        public string Key { get; set; }
        public DataValue V { get; set; }
        public string T { get; set; }
        public Quality Q { get; set; }

        public EventEntry(string key, DataValue v, string t, Quality q) {
            Key = key;
            V = v;
            T = t;
            Q = q;
        }
    }
}
