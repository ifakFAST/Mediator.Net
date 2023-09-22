// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish;

internal sealed class VarMetaManagerIntern {

    private readonly HashSet<VariableRef> variables = new HashSet<VariableRef>();
    internal Dictionary<VariableRef, VarInfo> variables2Info = new Dictionary<VariableRef, VarInfo>();

    internal async Task Check(VariableValues values, Connection clientFAST) {

        bool newVars = values.Any(vv => !variables.Contains(vv.Variable));
        if (!newVars) return;

        foreach (var vv in values) {
            variables.Add(vv.Variable);
        }

        await UpdateVarInfo(clientFAST);
    }

    internal Task OnConfigChanged(Connection clientFAST) {
        return UpdateVarInfo(clientFAST);
    }

    private async Task UpdateVarInfo(Connection clientFAST) {
        VarInfoResult infoResult = await GetVarInfoFromVars(clientFAST, variables.ToArray());
        variables2Info = infoResult.Infos.ToDictionary(i => i.VarRef);
        foreach (VariableRef v in infoResult.InvalidVarRefs) {
            variables.Remove(v);
        }
    }

    private record VarInfoResult(IList<VarInfo> Infos, IList<VariableRef> InvalidVarRefs);

    private static async Task<VarInfoResult> GetVarInfoFromVars(Connection client, VariableRef[] vars) {

        var objects = new HashSet<ObjectRef>();
        foreach (var v in vars) {
            objects.Add(v.Object);
        }

        ObjectRef[] objectsArr = objects.ToArray();

        List<ObjectInfo>  objInfos  = await client.GetObjectsByID(objectsArr, ignoreMissing: true);
        List<ObjectValue> objValues = await client.GetObjectValuesByID(objectsArr, ignoreMissing: true);

        Dictionary<ObjectRef, ObjectInfo>  mapObj2Info  = objInfos.ToDictionary(i => i.ID);
        Dictionary<ObjectRef, ObjectValue> mapObj2Value = objValues.ToDictionary(i => i.Object);

        var result = new List<VarInfo>();
        var invalidVars = new List<VariableRef>();

        foreach (VariableRef v in vars) {

            if (!mapObj2Info.TryGetValue(v.Object, out ObjectInfo? objInfo)) {
                invalidVars.Add(v);
                continue;
            }

            if (!mapObj2Value.TryGetValue(v.Object, out ObjectValue objValue)) {
                invalidVars.Add(v);
                continue;
            }

            foreach (Variable variable in objInfo.Variables) {
                if (variable.Name == v.Name) {
                    result.Add(new VarInfo(v, variable, objInfo, objValue));
                }
            }
        }

        return new VarInfoResult(result, invalidVars);
    }
}

internal sealed class VarMetaManager {

    private enum TaskType {
        Check,
        OnConfigChanged,
        Stop
    }

    private record Work(
        TaskType What, 
        TaskCompletionSource<bool> Promise, 
        Connection? Client = null, 
        VariableValues? Values = null);

    private readonly AsyncQueue<Work> queue = new();

    public VarMetaManager() {
        _ = Loop();
    }

    public Dictionary<VariableRef, VarInfo> Variables2Info => intern.variables2Info;

    public Task Close() {
        var promise = new TaskCompletionSource<bool>();
        queue.Post(new Work(TaskType.Stop, promise));
        return promise.Task;
    }

    public Task Check(VariableValues values, Connection clientFAST) {
        var promise = new TaskCompletionSource<bool>();
        queue.Post(new Work(TaskType.Check, promise, clientFAST, values));
        return promise.Task;
    }

    public Task OnConfigChanged(Connection clientFAST) {
        var promise = new TaskCompletionSource<bool>();
        queue.Post(new Work(TaskType.OnConfigChanged, promise, clientFAST));
        return promise.Task;
    }

    private async Task Loop() {

        while (true) {

            Work w = await queue.ReceiveAsync();

            switch (w.What) {

                case TaskType.Check:
                    await DoCheck(w.Values!, w.Client!);
                    w.Promise.SetResult(true);
                    break;

                case TaskType.OnConfigChanged:
                    await DoOnConfigChanged(w.Client!);
                    w.Promise.SetResult(true);
                    break;

                case TaskType.Stop:
                    w.Promise.SetResult(true);
                    return;
            }
        }
    }

    private readonly VarMetaManagerIntern intern = new();

    private async Task DoCheck(VariableValues values, Connection clientFAST) {
        try {
            await intern.Check(values, clientFAST);
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            Console.Error.WriteLine($"VarMetaManager Check: {e.Message}");
        }
    }

    private async Task DoOnConfigChanged(Connection clientFAST) {
        try {
            await intern.OnConfigChanged(clientFAST);
        }
        catch (Exception exp) {
            Exception e = exp.GetBaseException() ?? exp;
            Console.Error.WriteLine($"VarMetaManager OnConfigChanged: {e.Message}");
        }
    }
}