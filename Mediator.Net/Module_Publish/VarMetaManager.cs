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
    public Dictionary<VariableRef, VarInfo> variables2Info = new Dictionary<VariableRef, VarInfo>();

    public async Task Check(VariableValues values, Connection clientFAST) {

        bool newVars = values.Any(vv => !variables.Contains(vv.Variable));
        if (!newVars) return;

        foreach (var vv in values) {
            variables.Add(vv.Variable);
        }

        await UpdateVarInfo(clientFAST);
    }

    public Task OnConfigChanged(Connection clientFAST) {
        return UpdateVarInfo(clientFAST);
    }

    private async Task UpdateVarInfo(Connection clientFAST) {
        VarInfo[]? infos = await GetVarInfoFromVars(clientFAST, variables.ToArray());
        if (infos != null) {
            variables2Info = infos.ToDictionary(i => i.VarRef);
        }
        else {
            this.variables.Clear();
        }
    }

    private static async Task<VarInfo[]?> GetVarInfoFromVars(Connection client, IEnumerable<VariableRef> vars) {

        var objects = new HashSet<ObjectRef>();
        foreach (var v in vars) {
            objects.Add(v.Object);
        }

        ObjectRef[] objectsArr = objects.ToArray();

        List<ObjectInfo> infos;
        try {
            infos = await client.GetObjectsByID(objectsArr);
        }
        catch (Exception) {
            return null; // old object refs not found
        }

        List<ObjectValue> objValues = await client.GetObjectValuesByID(objectsArr);

        Dictionary<ObjectRef, ObjectInfo> mapObj2Info = infos.ToDictionary(i => i.ID);
        Dictionary<ObjectRef, ObjectValue> mapObj2Value = objValues.ToDictionary(i => i.Object);

        var result = new List<VarInfo>();
        foreach (var v in vars) {
            ObjectInfo objInfo = mapObj2Info[v.Object];
            ObjectValue objValue = mapObj2Value[v.Object];
            foreach (var variable in objInfo.Variables) {
                if (variable.Name == v.Name) {
                    result.Add(new VarInfo(v, variable, objInfo, objValue));
                }
            }
        }
        return result.ToArray();
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

            Console.WriteLine($"Loop: {w.What}  TID: {Environment.CurrentManagedThreadId}");

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

    private Task DoCheck(VariableValues values, Connection clientFAST) {
        try {
            return intern.Check(values, clientFAST);
        }
        catch (Exception) {
            return Task.FromResult(true);
        }
    }

    private Task DoOnConfigChanged(Connection clientFAST) {
        try {
            return intern.OnConfigChanged(clientFAST);
        }
        catch (Exception) {
            return Task.FromResult(true);
        }
    }
}