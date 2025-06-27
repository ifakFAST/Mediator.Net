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
using System.Threading;

namespace Ifak.Fast.Mediator.Calc.Adapter_CSharp;

public class CompileLib
{
    public static CompileResult CSharpFile2Assembly(string fullFileName) {
        string code = File.ReadAllText(fullFileName, Encoding.UTF8);
        return CSharpCode2Assembly(code, new Assembly[0]);
    }

    public static CompileResult CSharpCode2Assembly(string code, IList<Assembly> refAssemblies) {

        string version = Util.VersionInfo.ifakFAST_Str();

        string hash = GetHash(code);
        string tempDir = Path.Combine(Path.GetTempPath(), "ifakFAST", version);
        Directory.CreateDirectory(tempDir);

        string assemblyName = hash + ".dll";
        string assemblyFullName = Path.Combine(tempDir, assemblyName);
        if (File.Exists(assemblyFullName)) {
            return CompileResult.Make(cached: true, assembly: assemblyFullName, time: Duration.FromSeconds(0));
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();

        CSharpCompilation comp = GenerateCode(assemblyName, code, refAssemblies);

        using (var stream = new MemoryStream()) {

            EmitResult result = comp.Emit(stream);

            sw.Stop();

            if (result.Success) {
                stream.Seek(0, SeekOrigin.Begin);
                byte[] bytes = stream.ToArray();

                const int Tries = 5;
                for (int n = 1; n <= Tries; ++n) {
                    try {
                        File.WriteAllBytes(assemblyFullName, bytes);
                        return CompileResult.Make(cached: false, assembly: assemblyFullName, time: Duration.FromMilliseconds(sw.ElapsedMilliseconds));
                    }
                    catch (Exception e) {
                        Exception exp = e.GetBaseException() ?? e;
                        Console.Out.WriteLine($"WARN: Failed to write assembly file {assemblyFullName}: {e.Message} [Try {n}/{Tries}]");
                        Console.Out.Flush();
                        Thread.Sleep(n * 100);
                        if (n >= Tries) {
                            throw new IOException($"Failed to write assembly file {assemblyFullName}: {e.Message}");
                        }
                    }
                }
                throw new IOException($"Failed to write assembly file {assemblyFullName}");
            }
            else {

                var buffer = new StringBuilder();
                buffer.AppendLine($"Failed to compile C# lib");

                var failures = result.Diagnostics.Where(diagnostic => diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var dia in failures) {
                    var lineSpan = dia.Location.GetLineSpan();
                    int line = lineSpan.StartLinePosition.Line + 1;
                    int charac = lineSpan.StartLinePosition.Character + 1;
                    buffer.AppendLine($"{dia.Id} in line {line} pos {charac} Error: {dia.GetMessage()}");
                }

                throw new Exception(buffer.ToString());
            }
        }
    }

    private static CSharpCompilation GenerateCode(string assemblyName, string sourceCode, IList<Assembly> refAssemblies) {

        var codeString = SourceText.From(sourceCode);
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var references = new List<PortableExecutableReference>();

        if (trustedAssemblies != null) {
            string[] assemblies = trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
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

        references.Add(MetadataReference.CreateFromFile(typeof(CSharp).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Ifak.Fast.Mediator.Timestamp).Assembly.Location));

        foreach (Assembly ass in refAssemblies) {
            references.Add(MetadataReference.CreateFromFile(ass.Location));
        }

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

public struct CompileResult
{
    public bool IsUsingCachedAssembly { get; private set; }
    public string AssemblyFileName    { get; private set; }
    public Duration CompileTime       { get; private set; }

    public static CompileResult Make(bool cached, string assembly, Duration time) {
        return new CompileResult() {
            IsUsingCachedAssembly = cached,
            AssemblyFileName = assembly,
            CompileTime = time,
        };
    }
}
