// Licensed to ifak e.V. under one or more agreements.
// ifak e.V. licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Ifak.Fast.Mediator.Calc.Adapter_CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace MediatorLib_Test.Calc;

public class Test_CompileLib
{
    // The warm-up runs in the background and only logs a warning on failure,
    // so a change of the script API that breaks the warm-up code would go unnoticed.
    [Fact]
    public void WarmUp_code_compiles() {
        EmitResult result = CompileLib.CompileWarmUpCode();
        string errors = string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
        Assert.True(result.Success, errors);
    }

    // Consecutive compilations share the cached (memory-mapped) base references.
    [Fact]
    public void Consecutive_compilations_share_base_references() {
        for (int i = 0; i < 2; i++) {
            string code = $$"""
                using System.Linq;
                using Ifak.Fast.Mediator.Calc.Adapter_CSharp;
                // {{Guid.NewGuid()}}
                class Script {
                    Input X = new Input("X", "", 1);
                    Output Y = new Output("Y");
                    public void Step() { Y.Value = Enumerable.Range(0, {{i}}).Sum() + X; }
                }
                """;
            CompileResult res = CompileLib.CSharpCode2Assembly(code, []);
            try {
                Assert.False(res.IsUsingCachedAssembly);
                Assert.True(File.Exists(res.AssemblyFileName));
            }
            finally {
                File.Delete(res.AssemblyFileName);
            }
        }
    }
}
