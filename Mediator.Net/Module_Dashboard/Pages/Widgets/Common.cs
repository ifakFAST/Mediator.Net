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

        public static async Task<Y[]> TransformAsync<X, Y>(IReadOnlyList<X> x, Func<X, Task<Y>> transform) {
            const int BatchSize = 4;
            int n = x.Count;
            if (n <= BatchSize) {
                return await Task.WhenAll(x.Select(transform));
            }
            else {
                Y[] firstResults = await Task.WhenAll(x.Take(BatchSize).Select(transform));
                Y[] restResults = await Task.WhenAll(x.Skip(BatchSize).Select(transform));
                return firstResults.Concat(restResults).ToArray();
            }
        }

        public static Task<ReqResult> GetNumericVarItemsData(Connection connection, ObjectRef[] usedObjects) {
            return GetVarItemsData(connection, usedObjects, IsNumericOrBoolOrTimeseries);
        }

        public static Task<ReqResult> GetNumericAndStringVarItemsData(Connection connection, ObjectRef[] usedObjects) {
            return GetVarItemsData(connection, usedObjects, IsNumericOrBoolOrString);
        }

        public static async Task<ReqResult> GetVarItemsData(Connection connection, ObjectRef[] usedObjects, Func<DataType, bool> f) {

            var modules = (await connection.GetModules())
                .Where(m => m.VariableDataTypes.Any(f))
                .Select(m => new ModuleInfo() {
                    ID = m.ID,
                    Name = m.Name
                }).ToArray();

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
                        infos.Add(new ObjectInfo(obj, obj.ToEncodedString(), "???", "???"));
                    }
                }
            }

            var objectMap = new Dictionary<string, ObjInfo>();

            foreach (ObjectInfo info in infos) {
                var relevantVariables = info.Variables.Where(v => f(v.Type)).Select(v => v.Name).ToArray();
                objectMap[info.ID.ToEncodedString()] = new ObjInfo() {
                    Name = info.Name,
                    Variables = relevantVariables
                };
            }

            return ReqResult.OK(new {
                ObjectMap = objectMap,
                Modules = modules,
            });
        }

        public static bool IsNumericOrBoolOrTimeseries(DataType t) => t.IsNumeric() || t == DataType.Bool || t == DataType.Timeseries;

        private static bool IsNumericOrBoolOrString(DataType t) => t.IsNumeric() || t == DataType.Bool || t == DataType.String;

    }
}
