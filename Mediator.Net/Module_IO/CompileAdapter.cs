// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Emit;
using System.Reflection;

namespace Ifak.Fast.Mediator.IO
{
    public class CompileAdapter
    {
        public static string CSharpFile2Assembly(string fullFileName) {

            string code = File.ReadAllText(fullFileName, Encoding.UTF8);
            string hash = GetHash(code);
            string tempDir = Path.GetTempPath();
            string assemblyName = hash + ".dll";
            string assemblyFullName = Path.Combine(tempDir, assemblyName);
            if (File.Exists(assemblyFullName)) {
                Console.WriteLine($"Using cached adapter assembly:");
                Console.WriteLine($"\tSource:   {fullFileName}");
                Console.WriteLine($"\tAssembly: {assemblyFullName}");
                Console.WriteLine($"\tCreated:  {File.GetCreationTime(assemblyFullName)}");
                return assemblyFullName;
            }

            CSharpCompilation comp = GenerateCode(assemblyName, code);

            using (var stream = new MemoryStream()) {

                EmitResult result = comp.Emit(stream);

                if (result.Success) {
                    stream.Seek(0, SeekOrigin.Begin);
                    File.WriteAllBytes(assemblyFullName, stream.ToArray());
                    Console.WriteLine($"Compiled adapter assembly from source file:");
                    Console.WriteLine($"\tSource:   {fullFileName}");
                    Console.WriteLine($"\tAssembly: {assemblyFullName}");
                }
                else {

                    string errMsg = $"Failed to compile IO adapter {fullFileName}";
                    Console.Error.WriteLine(errMsg);

                    var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                    foreach (var dia in failures) {
                        var lineSpan = dia.Location.GetLineSpan();
                        int line = lineSpan.StartLinePosition.Line + 1;
                        int charac = lineSpan.StartLinePosition.Character + 1;
                        Console.Error.WriteLine($"{dia.Id} in line {line} pos {charac} Error: {dia.GetMessage()}");
                    }

                    throw new Exception(errMsg);
                }
            }

            return assemblyFullName;
        }

        private static CSharpCompilation GenerateCode(string assemblyName, string sourceCode) {

            var codeString = SourceText.From(sourceCode);
            var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

            var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

            string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
            var references = new List<PortableExecutableReference>();

            if (trustedAssemblies != null) {
                string[] assemblies = trustedAssemblies.Split(";", StringSplitOptions.RemoveEmptyEntries);
                references.AddRange(assemblies.Select(ass => MetadataReference.CreateFromFile(ass)));
            }
            else {
                references.Add(MetadataReference.CreateFromFile(typeof(System.Object).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.Console).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.Linq.Enumerable).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.IO.BinaryReader).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.Text.ASCIIEncoding).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.Collections.Generic.KeyValuePair).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location));
                references.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location));
            }

            references.Add(MetadataReference.CreateFromFile(typeof(AdapterBase).Assembly.Location));
            references.Add(MetadataReference.CreateFromFile(typeof(Module).Assembly.Location));

            return CSharpCompilation.Create(assemblyName,
                new[] { parsedSyntaxTree },
                references: references,
                options: new CSharpCompilationOptions(
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release,
                    assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));
        }

        private static string GetHash(string text) {
            using (var hash = SHA256.Create()) {
                byte[] data = hash.ComputeHash(Encoding.UTF8.GetBytes(text));
                var sBuilder = new StringBuilder();
                for (int i = 0; i < data.Length; i++) {
                    sBuilder.Append(data[i].ToString("x2"));
                }
                return sBuilder.ToString();
            }
        }
    }
}
