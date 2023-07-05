using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VariableValues = System.Collections.Generic.List<Ifak.Fast.Mediator.VariableValue>;

namespace Ifak.Fast.Mediator.Publish;

internal sealed class VarMetaManager {

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

