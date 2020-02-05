using Ifak.Fast.Mediator.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Ifak.Fast.Mediator
{
    internal class ModuleLoader
    {
        internal static ModuleBase CreateModuleInstanceOrThrow(string typeName, string assemblyName) {

            Type t = Reflect.GetNonAbstractSubclassInDomainBaseDirectory(typeof(ModuleBase), typeName);

            if (t != null) {
                return (ModuleBase)Activator.CreateInstance(t);
            }

            assemblyName = assemblyName.Trim();

            if (string.IsNullOrEmpty(assemblyName)) {
                throw new Exception($"Module type '{typeName}' not found in domain base dir and no assembly file given.");
            }

            string fullAssemblyFile;
            if (Path.IsPathRooted(assemblyName)) {
                fullAssemblyFile = Path.GetFullPath(assemblyName);
            }
            else if (assemblyName[0] == '.' || assemblyName.Contains('/') || assemblyName.Contains(Path.DirectorySeparatorChar)) {
                fullAssemblyFile = Path.GetFullPath(assemblyName);
            }
            else {
                fullAssemblyFile = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assemblyName));
            }

            t = LoadTypeFromAssemblyFile(fullAssemblyFile, typeName);

            if (t != null) {
                return (ModuleBase)Activator.CreateInstance(t);
            }

            throw new Exception($"Module type '{typeName}' not found in assembly {fullAssemblyFile}.");
        }

        private static Type LoadTypeFromAssemblyFile(string fileName, string typeName) {
            try {
                Type baseClass = typeof(ModuleBase);

                var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
                        fileName,
                        sharedTypes: new Type[] { baseClass });

                return loader.LoadDefaultAssembly()
                    .GetExportedTypes()
                    .FirstOrDefault(t => t.IsSubclassOf(baseClass) && !t.IsAbstract && t.FullName.Equals(typeName, StringComparison.InvariantCultureIgnoreCase));
            }
            catch (Exception exp) {
                Console.Error.WriteLine($"Failed to load module types from assembly '{fileName}': {exp.Message}");
                Console.Error.Flush();
                return null;
            }
        }

    }
}
