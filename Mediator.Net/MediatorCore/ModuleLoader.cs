﻿// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Ifak.Fast.Mediator.Util;
using System;

namespace Ifak.Fast.Mediator
{
    internal class ModuleLoader
    {
        internal static ModuleBase CreateModuleInstanceOrThrow(string typeName, string assemblyName) {

            if (typeName == Module.ExternalModule) {
                return new ExternalModule();
            }

            // Load known module types, so that they can be found by GetNonAbstractSubclassInDomainBaseDirectory
            Type[] preload = [
                typeof(Ifak.Fast.Mediator.Calc.Module),
                typeof(Ifak.Fast.Mediator.IO.Module),
                typeof(Ifak.Fast.Mediator.Dashboard.Module),
                typeof(Ifak.Fast.Mediator.EventLog.Module),
                typeof(Ifak.Fast.Mediator.Publish.Module),
                typeof(Ifak.Fast.Mediator.TagMetaData.Module),
            ];
            
            Type? t = Reflect.GetNonAbstractSubclassInDomainBaseDirectory(typeof(ModuleBase), typeName);

            if (t != null) {
                return (ModuleBase)(Activator.CreateInstance(t) ?? throw new Exception($"CreateInstance of module type '{t}' returned null"));
            }

            // Loading third-party modules in-process currently not supported
            // To add support: https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support

            throw new Exception($"Failed to load module type '{typeName}'.");


            //assemblyName = assemblyName.Trim();

            //if (string.IsNullOrEmpty(assemblyName)) {
            //    throw new Exception($"Module type '{typeName}' not found in domain base dir and no assembly file given.");
            //}

            //string fullAssemblyFile;
            //if (Path.IsPathRooted(assemblyName)) {
            //    fullAssemblyFile = Path.GetFullPath(assemblyName);
            //}
            //else if (assemblyName[0] == '.' || assemblyName.Contains('/') || assemblyName.Contains(Path.DirectorySeparatorChar)) {
            //    fullAssemblyFile = Path.GetFullPath(assemblyName);
            //}
            //else {
            //    fullAssemblyFile = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? "", assemblyName));
            //}

            //t = LoadTypeFromAssemblyFile(fullAssemblyFile, typeName);

            //if (t != null) {
            //    return (ModuleBase)(Activator.CreateInstance(t) ?? throw new Exception($"CreateInstance of module type '{t}' returned null"));
            //}

            //throw new Exception($"Module type '{typeName}' not found in assembly {fullAssemblyFile}.");
        }

        //private static Type? LoadTypeFromAssemblyFile(string fileName, string typeName) {
        //    try {
        //        Type baseClass = typeof(ModuleBase);

        //        var loader = McMaster.NETCore.Plugins.PluginLoader.CreateFromAssemblyFile(
        //                fileName,
        //                sharedTypes: new Type[] { baseClass });

        //        return loader.LoadDefaultAssembly()
        //            .GetExportedTypes()
        //            .FirstOrDefault(t => t.IsSubclassOf(baseClass) && !t.IsAbstract && typeName.Equals(t.FullName, StringComparison.InvariantCultureIgnoreCase));
        //    }
        //    catch (Exception exp) {
        //        Console.Error.WriteLine($"Failed to load module types from assembly '{fileName}': {exp.Message}");
        //        Console.Error.Flush();
        //        return null;
        //    }
        //}

    }
}
