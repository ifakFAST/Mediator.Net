// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ObjectInfos = System.Collections.Generic.List<Ifak.Fast.Mediator.ObjectInfo>;

namespace Ifak.Fast.Mediator.Dashboard.Pages.Widgets
{
    internal class Common
    {
        public class ModuleInfo
        {
            public string ID { get; set; } = "";
            public string Name { get; set; } = "";
        }

        public class ObjInfo
        {
            public string Name { get; set; } = "";
            public string[] Variables { get; set; } = new string[0];
        }

        public static async Task<ReqResult> GetItemsData(Connection connection, ObjectRef[] usedObjects) {

            var modules = (await connection.GetModules())
                .Where(m => m.HasNumericVariables)
                .Select(m => new ModuleInfo() {
                    ID = m.ID,
                    Name = m.Name
                }).ToArray();

            var objectMap = new Dictionary<string, ObjInfo>();

            // ObjectRef[] objects = configuration.Items.Select(it => it.Variable.Object).Distinct().ToArray();

            ObjectInfos infos;
            try {
                infos = await connection.GetObjectsByID(usedObjects);
            }
            catch (Exception) {
                infos = new ObjectInfos(usedObjects.Length);
                for (int i = 0; i < usedObjects.Length; ++i) {
                    ObjectRef obj = usedObjects[i];
                    try {
                        infos.Add(await connection.GetObjectByID(obj));
                    }
                    catch (Exception) {
                        infos.Add(new ObjectInfo(obj, "???", "???"));
                    }
                }
            }

            foreach (ObjectInfo info in infos) {
                var numericVariables = info.Variables.Where(IsNumericOrBool).Select(v => v.Name).ToArray();
                objectMap[info.ID.ToEncodedString()] = new ObjInfo() {
                    Name = info.Name,
                    Variables = numericVariables
                };
            }

            return ReqResult.OK(new {
                ObjectMap = objectMap,
                Modules = modules,
            });
        }

        public static bool IsNumericOrBool(Variable v) => v.IsNumeric || v.Type == DataType.Bool;

    }
}
