using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class RulesTableTests
{
    [Fact]
    public Task RulesTable()
    {
        var rows =
            from descriptor in GetDescriptors()
            select $"| {descriptor.Id} | {descriptor.Title} |";

        var result = $"""
        | Id | Title |
        | --- | --- |
        {string.Join("\n", rows)}
        """;

        return Verify(result, extension: "md");
    }

    [Fact]
    public Task RulesList()
    {
        var items =
            from descriptor in GetDescriptors()
            select $"- {descriptor.Title}";

        return Verify(string.Join("\n", items), extension: "md");
    }

    private static IEnumerable<DiagnosticDescriptor> GetDescriptors()
        => from type in typeof(DbContextQueryAnalyzer).Assembly.GetTypes()
           where typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && !type.IsAbstract
           from fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.Static)
           where fieldInfo.FieldType == typeof(DiagnosticDescriptor)
           let descriptor = (DiagnosticDescriptor)fieldInfo.GetValue(null)!
           orderby descriptor.Id
           select descriptor;
}
