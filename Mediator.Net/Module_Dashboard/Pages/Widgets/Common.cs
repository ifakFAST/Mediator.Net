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

        /// <summary>
        /// Transforms each input element to an async result while preserving the original input order.
        /// </summary>
        /// <typeparam name="X">Input element type (must be non-null).</typeparam>
        /// <typeparam name="Y">Result element type.</typeparam>
        /// <param name="x">Input values to transform.</param>
        /// <param name="transform">Async transform function.</param>
        /// <returns>
        /// An array with the same length and order as <paramref name="x"/>, containing transformed values.
        /// </returns>
        /// <remarks>
        /// Inputs are deduplicated using <see cref="EqualityComparer{T}.Default"/>, so <paramref name="transform"/> is executed
        /// once per distinct input and shared across duplicates.
        /// The first four distinct transforms are started immediately; when a fifth distinct input is encountered,
        /// the method waits for the initial four tasks to complete before starting additional distinct transforms.
        /// </remarks>
        internal static async Task<Y[]> TransformAsync<X, Y>(IReadOnlyList<X> x, Func<X, Task<Y>> transform) where X: notnull {

            if (x.Count == 0) return [];

            var cache = new Dictionary<X, Task<Y>>();
            var resultTasks = new Task<Y>[x.Count];

            for (int i = 0; i < x.Count; i++) {
                X input = x[i];

                if (!cache.TryGetValue(input, out Task<Y>? task)) {
                    // Respect the batching requirement: 
                    // Wait for the first 4 distinct tasks before starting any others
                    if (cache.Count == 4) {
                        await Task.WhenAll(cache.Values);
                    }

                    task = transform(input);
                    cache[input] = task;
                }

                resultTasks[i] = task;
            }

            return await Task.WhenAll(resultTasks);
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
