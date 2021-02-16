using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp
{
    internal abstract class CodeToObjectBase
    {
        internal const string className = "Script";

        internal abstract Task<object> MakeObjectFromCode(string name, string code, IList<Assembly> referencedAssemblies);
    }

    internal class CodeToObjectCompile : CodeToObjectBase
    {
        internal override Task<object> MakeObjectFromCode(string name, string code, IList<Assembly> referencedAssemblies) {

            var sb = new StringBuilder();
            sb.Append("using System; ");
            sb.Append("using System.Collections.Generic; ");
            sb.Append("using System.Linq; ");
            sb.Append("using System.Globalization; ");
            sb.Append("using System.Text; ");
            sb.Append("using Ifak.Fast.Mediator.Calc.Adapter_CSharp; ");
            sb.Append("using Ifak.Fast.Mediator; ");
            sb.Append(code);

            CompileResult compileRes = CompileLib.CSharpCode2Assembly(sb.ToString(), referencedAssemblies); ;
            string assemblyFileName = compileRes.AssemblyFileName;

            Print(compileRes, name);

            Type typeScriptClass;
            try {
                Assembly assembly = Assembly.LoadFrom(assemblyFileName);
                typeScriptClass = assembly.GetType(className, throwOnError: true, ignoreCase: false);
            }
            catch (Exception e) {
                Exception exp = e.GetBaseException() ?? e;
                throw new Exception($"Script {name}: Failed to load {className} type from {assemblyFileName}: {exp.Message}");
            }

            object obj;
            try {
                obj = Activator.CreateInstance(typeScriptClass);
            }
            catch (Exception e) {
                Exception exp = e.GetBaseException() ?? e;
                throw new Exception($"Script {name}: Failed to create instance of {className} class: {exp.Message}");
            }

            return Task.FromResult(obj);
        }

        private void Print(CompileResult compileRes, string name) {
            var buffer = new StringBuilder();
            string assemblyFileName = Path.GetFileName(compileRes.AssemblyFileName);
            string assemblyDir = Path.GetDirectoryName(compileRes.AssemblyFileName);
            if (compileRes.IsUsingCachedAssembly) {
                buffer.AppendLine($"Script {name}: Using cached assembly");
                buffer.AppendLine($"   Directory: {assemblyDir}");
                buffer.AppendLine($"   Assembly:  {assemblyFileName}");
                buffer.AppendLine($"   Created:   {File.GetCreationTime(compileRes.AssemblyFileName)}");
            }
            else {
                buffer.AppendLine($"Script {name}: Compiled C# source file to cached assembly");
                buffer.AppendLine($"   Directory:   {assemblyDir}");
                buffer.AppendLine($"   Assembly:    {assemblyFileName}");
                buffer.AppendLine($"   CompileTime: {compileRes.CompileTime}");
            }
            Console.Out.WriteLine(buffer.ToString());
            Console.Out.Flush();
        }
    }

    internal class CodeToObjectScripting : CodeToObjectBase
    {
        internal override async Task<object> MakeObjectFromCode(string name, string code, IList<Assembly> refAssemblies) {

            var referencedAssemblies = new List<Assembly>();
            referencedAssemblies.Add(typeof(IList<int>).Assembly);
            referencedAssemblies.Add(typeof(System.Linq.Enumerable).Assembly);
            referencedAssemblies.Add(typeof(Timestamp).Assembly);
            referencedAssemblies.Add(typeof(Input).Assembly);
            referencedAssemblies.AddRange(refAssemblies);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            var options = ScriptOptions.Default
                           .WithImports(
                               "System",
                               "System.Collections.Generic",
                               "System.Linq",
                               "System.Globalization",
                               "System.Text",
                               "Ifak.Fast.Mediator.Calc.Adapter_CSharp",
                               "Ifak.Fast.Mediator")
                           .WithReferences(referencedAssemblies)
                           .WithEmitDebugInformation(true);

            var script = CSharpScript.
                Create<object>(code, options).
                ContinueWith($"new {className}()");

            ScriptState<object> scriptState = await script.RunAsync();
            object obj = scriptState.ReturnValue;

            sw.Stop();
            Print(sw.Elapsed, name);

            return obj;
        }

        private void Print(TimeSpan duration, string name) {
            Console.Out.WriteLine($"Script {name}: Parsed C# source file in {Duration.FromTimeSpan(duration)}");
            Console.Out.Flush();
        }
    }
}
