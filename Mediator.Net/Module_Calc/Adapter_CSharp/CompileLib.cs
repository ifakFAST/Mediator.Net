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
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
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

    private static int warmUpStarted = 0;

    /// <summary>
    /// Starts a background compilation of a small script (no assembly file is written).
    /// Without it, the first compilation after module start takes several seconds, because
    /// the compiler code itself needs to be JIT compiled and the metadata of the base references
    /// needs to be loaded. Subsequent calls do nothing.
    /// </summary>
    public static void StartWarmUp() {
        if (Interlocked.Exchange(ref warmUpStarted, 1) == 1) return;
        var thread = new Thread(WarmUp) {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal,
            Name = "C# compiler warm-up",
        };
        thread.Start();
    }

    private static void WarmUp() {
        try {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            EmitResult result = CompileWarmUpCode();
            sw.Stop();
            if (result.Success) {
                Console.Out.WriteLine($"C# compiler warm-up completed in {Duration.FromMilliseconds(sw.ElapsedMilliseconds)}");
            }
            else {
                Diagnostic? error = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
                Console.Out.WriteLine($"WARN: C# compiler warm-up: Compile failed: {error?.GetMessage()}");
            }
        }
        catch (Exception e) {
            Exception exp = e.GetBaseException() ?? e;
            Console.Out.WriteLine($"WARN: C# compiler warm-up failed: {exp.Message}");
        }
        Console.Out.Flush();
    }

    internal static EmitResult CompileWarmUpCode() {
        CSharpCompilation comp = GenerateCode("WarmUp.dll", WarmUpCode, []);
        using var stream = new MemoryStream();
        return comp.Emit(stream);
    }

    // Uses the language features and library types that typical calculation scripts use,
    // so that the respective compiler code paths are JIT compiled:
    private const string WarmUpCode = """
        using System;
        using System.Collections.Generic;
        using System.Linq;
        using Ifak.Fast.Mediator.Calc.Adapter_CSharp;
        using Ifak.Fast.Mediator;

        class Script {

            Input  X = new Input(name: "X", unit: "mg/L", defaultValue: 5);
            Output Y = new Output("Y", "m³/d", roundDigits: 3);
            State  counter = new State("Counter", "", 0);
            List<double> values = new();

            public void Step(Timestamp t, Duration dt) {
                values.Add(X);
                double avg = values.Where(v => !double.IsNaN(v)).Select(v => 2 * v).DefaultIfEmpty().Average();
                Y.Value = X > 10.0 ? avg : Math.Round(avg, 2);
                counter.Value = counter + 1;
                Console.WriteLine($"{t}: {avg} (dt = {dt})");
            }
        }
        """;

    private static readonly object baseReferencesLock = new();
    private static PortableExecutableReference[]? baseReferences = null;

    // The base references are created only once and shared by all compilations,
    // so that the compiler can reuse the metadata it has already loaded for them.
    private static PortableExecutableReference[] GetBaseReferences() {
        lock (baseReferencesLock) {
            baseReferences ??= CreateBaseReferences();
            return baseReferences;
        }
    }

    private static PortableExecutableReference[] CreateBaseReferences() {

        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        var files = new List<string>();

        if (trustedAssemblies != null) {
            files.AddRange(trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        }
        else {
            files.Add(typeof(System.Object).Assembly.Location);
            files.Add(typeof(System.Console).Assembly.Location);
            files.Add(typeof(System.Linq.Enumerable).Assembly.Location);
            files.Add(typeof(System.IO.BinaryReader).Assembly.Location);
            files.Add(typeof(System.Text.ASCIIEncoding).Assembly.Location);
            files.Add(typeof(System.Collections.Generic.KeyValuePair).Assembly.Location);
            files.Add(typeof(System.Runtime.AssemblyTargetedPatchBandAttribute).Assembly.Location);
            files.Add(typeof(Microsoft.CSharp.RuntimeBinder.CSharpArgumentInfo).Assembly.Location);
            files.Add(Assembly.Load("netstandard").Location);
        }

        files.Add(typeof(CSharp).Assembly.Location);
        files.Add(typeof(SqliteConnection).Assembly.Location);
        files.Add(typeof(SqlConnection).Assembly.Location);
        files.Add(typeof(MySqlConnection).Assembly.Location);
        files.Add(typeof(NpgsqlConnection).Assembly.Location);
        files.Add(typeof(Ifak.Fast.Mediator.Timestamp).Assembly.Location);

        return files
            .Distinct(StringComparer.Ordinal)
            .Select(CreateMemoryMappedReference)
            .ToArray();
    }

    // Unlike MetadataReference.CreateFromFile, which copies the entire file into private memory,
    // this keeps the file open and memory-maps it. Matters because the base references are kept
    // for the lifetime of the process (TRUSTED_PLATFORM_ASSEMBLIES amounts to more than 100 MB).
    private static PortableExecutableReference CreateMemoryMappedReference(string file) {
        var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        try {
            return AssemblyMetadata.CreateFromStream(stream).GetReference(filePath: file);
        }
        catch {
            stream.Dispose();
            throw;
        }
    }

    private static CSharpCompilation GenerateCode(string assemblyName, string sourceCode, IList<Assembly> refAssemblies) {

        var codeString = SourceText.From(sourceCode);
        var options = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

        var parsedSyntaxTree = SyntaxFactory.ParseSyntaxTree(codeString, options);

        var references = new List<PortableExecutableReference>(GetBaseReferences());

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
