// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Ifak.Fast.Mediator.Util
{
    public class Reflect
    {
        public static T CreateInstanceOrThrow<T>(string typeName, string? assemblyName = null) {

            if (string.IsNullOrWhiteSpace(assemblyName)) {
                Type t = Type.GetType(typeName, throwOnError: true);
                return (T)Activator.CreateInstance(t);
            }
            else {
                Assembly? assembly;

                string fullFileName = Path.GetFullPath(Path.IsPathRooted(assemblyName) ? assemblyName : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName));
                Assembly assemblyOfT = typeof(T).Assembly;

                if (assemblyOfT.Location.Equals(fullFileName, StringComparison.InvariantCultureIgnoreCase)) {
                    assembly = assemblyOfT;
                }
                else {

                    try {
                        assembly = Assembly.LoadFile(fullFileName);
                    }
                    catch (Exception exp) {
                        throw new Exception($"Failed to load assembly: {fullFileName} ({exp.Message})");
                    }
                    if (assembly == null) throw new Exception($"Failed to load assembly: {fullFileName}");
                }

                var type = assembly.GetType(typeName);
                if (type == null) throw new Exception($"Type {typeName} not found in {fullFileName}");
                var newObject = Activator.CreateInstance(type);
                return (T)newObject;
            }
        }

        public static IList<Type> GetAllNonAbstractSubclasses(Type baseClass, string[]? externalAssemblyFiles = null) {

            var result = new List<Type>();
            var processedAssemblies = new HashSet<string>();
            var processedTypes = new HashSet<string>();

            void loadTypes(Assembly assembly) {
                Type[] types = assembly.GetExportedTypes();
                foreach (Type t in types) {
                    if (t.IsSubclassOf(baseClass) && !t.IsAbstract && !processedTypes.Contains(t.FullName)) {
                        result.Add(t);
                        processedTypes.Add(t.FullName);
                    }
                }
            }

            string BaseDir = EnsurePathSep(AppDomain.CurrentDomain.BaseDirectory);
            string CallingDir = EnsurePathSep(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location));

            // Console.WriteLine($"CallDir: {CallingDir}");
            // Console.WriteLine($"BaseDir: {BaseDir}");

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (!assembly.IsDynamic) {
                    string location = assembly.Location;
                    if (location.StartsWith(BaseDir) || location.StartsWith(CallingDir)) {
                        loadTypes(assembly);
                        processedAssemblies.Add(location);
                    }
                }
            }

            if (externalAssemblyFiles != null) {

                foreach (string assemblyFile in externalAssemblyFiles) {

                    if (processedAssemblies.Contains(assemblyFile))
                        continue;

                    var assembly = Assembly.LoadFrom(assemblyFile);
                    loadTypes(assembly);
                }
            }

            return result;
        }

        private static string EnsurePathSep(string path) {
            int N = path.Length;
            if (N > 0 && path[N - 1] == Path.DirectorySeparatorChar) return path;
            return path + Path.DirectorySeparatorChar;
        }

        public static Type? GetNonAbstractSubclassInDomainBaseDirectory(Type baseClass, string typeName) {

            string BaseDir = EnsurePathSep(AppDomain.CurrentDomain.BaseDirectory);

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                if (!assembly.IsDynamic) {
                    string location = assembly.Location;
                    if (location.StartsWith(BaseDir)) {
                        string file = Path.GetFileName(location);
                        bool sysDll = (file.StartsWith("Microsoft.") || file.StartsWith("System."));
                        if (!sysDll) {
                            Type[] types = assembly.GetExportedTypes();
                            foreach (Type t in types) {
                                if (t.IsSubclassOf(baseClass) && !t.IsAbstract && t.FullName.Equals(typeName, StringComparison.InvariantCultureIgnoreCase)) {
                                    return t;
                                }
                            }
                        }
                    }
                }
            }

            return null;
        }
    }
}
