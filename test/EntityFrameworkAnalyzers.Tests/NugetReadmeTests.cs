using System.Text.RegularExpressions;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public partial class NugetReadmeTests
{
    // NuGet can't follow mdsnippets' "snippet source" links, so README.nuget.md
    // includes this verified file instead of using `snippet:`.
    // It's .md rather than .cs because mdsnippets only adds the code fence for `snippet:`.
    [Fact]
    public Task Example()
    {
        var source = DbContextQueryAnalyzerTests.DirectDbSetAccessSource;
        var start = source.IndexOf('\n', source.IndexOf("// begin-snippet:")) + 1;
        var end = source.IndexOf("// end-snippet");
        var code = DiagnosticMarkup().Replace(source[start..end], "$1");

        // Leading blank line: mdsnippets appends its include comment to the first line.
        var result = $"""

            ```cs
            {code.TrimEnd()}
            ```
            """;

        return Verify(result, extension: "md");
    }

    [GeneratedRegex(@"\{\|#\d+:(.*?)\|\}")]
    private static partial Regex DiagnosticMarkup();
}
