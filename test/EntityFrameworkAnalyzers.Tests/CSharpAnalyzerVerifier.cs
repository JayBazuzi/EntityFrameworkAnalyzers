using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

internal static class CSharpAnalyzerVerifier<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    // Restored from NuGet at test time, so the test project doesn't need an EF Core reference
    // that would have to stay compatible with its own target framework.
    private static readonly ReferenceAssemblies References = ReferenceAssemblies.Net.Net80.AddPackages(
        [new PackageIdentity("Microsoft.EntityFrameworkCore", "8.0.10")]);

    public static DiagnosticResult Diagnostic()
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic();

    public static DiagnosticResult Diagnostic(string diagnosticId)
        => CSharpAnalyzerVerifier<TAnalyzer, DefaultVerifier>.Diagnostic(diagnosticId);

    public static Task VerifyAnalyzerAsync(string source, params DiagnosticResult[] expected)
    {
        var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
        {
            TestCode = source,
            ReferenceAssemblies = References,
        };

        test.ExpectedDiagnostics.AddRange(expected);
        return test.RunAsync();
    }
}
