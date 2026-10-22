using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers;

/// <summary>
/// A DbContext should expose at most one <c>DbSet&lt;T&gt;</c> property per entity type.
/// Duplicates such as <c>ClassInstructors</c> and <c>ClassInstructors1</c> typically come from scaffolding
/// and are reported on every property after the first.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateDbSetAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0008";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "DbContext should have one `DbSet<T>` per entity type",
        messageFormat: "'{0}' duplicates 'DbSet<{1}>' property '{2}'",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Two DbSet properties of the same entity type are redundant. Remove the duplicate.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!EntityFrameworkTypes.IsDbContextType(type))
        {
            return;
        }

        var seen = new Dictionary<ITypeSymbol, IPropertySymbol>(SymbolEqualityComparer.Default);
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            if (!EntityFrameworkTypes.IsDbSetType(property.Type)
                || ((INamedTypeSymbol)property.Type).TypeArguments[0] is not { } entityType)
            {
                continue;
            }

            if (!seen.TryGetValue(entityType, out var first))
            {
                seen.Add(entityType, property);
            }
            else if (property.Locations[0].IsInSource)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule, property.Locations[0], property.Name, entityType.Name, first.Name));
            }
        }
    }
}
