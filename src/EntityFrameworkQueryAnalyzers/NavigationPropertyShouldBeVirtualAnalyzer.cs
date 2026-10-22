using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkQueryAnalyzers;

/// <summary>
/// Flags non-virtual navigation properties on entity types. Entity types are those exposed as
/// DbSet&lt;T&gt; on a DbContext in this compilation, plus any source types reachable from them through
/// navigations (as EF discovers them). Marking navigations virtual lets ENTITY_FRAMEWORK_0002 recognize
/// them without guessing, and is what EF's lazy-loading proxies require anyway.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NavigationPropertyShouldBeVirtualAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0003";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Navigation property should be `virtual`",
        messageFormat: "Navigation property '{0}.{1}' should be virtual",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Mark navigation properties virtual so that ENTITY_FRAMEWORK_0002 can recognize them " +
            "and check that they are only read inside queries.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var entityTypes = FindEntityTypes(compilationContext.Compilation);
            if (entityTypes.Count == 0)
            {
                return;
            }

            compilationContext.RegisterSymbolAction(
                symbolContext => AnalyzeProperty(symbolContext, entityTypes),
                SymbolKind.Property);
        });
    }

    private static void AnalyzeProperty(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> entityTypes)
    {
        var property = (IPropertySymbol)context.Symbol;

        if (property.IsVirtual || property.IsOverride || property.IsAbstract || property.IsStatic
            || property.ContainingType is not { IsSealed: false, TypeKind: TypeKind.Class } containingType
            || !entityTypes.Contains(containingType))
        {
            return;
        }

        var target = EntityFrameworkTypes.GetNavigationTarget(property.Type);
        if (target is null || !entityTypes.Contains(target))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            property.Locations[0],
            containingType.Name,
            property.Name));
    }

    private static ImmutableHashSet<INamedTypeSymbol> FindEntityTypes(Compilation compilation)
    {
        var entityTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<INamedTypeSymbol>();

        foreach (var type in GetSourceTypes(compilation.Assembly.GlobalNamespace))
        {
            if (!EntityFrameworkTypes.IsDbContextType(type))
            {
                continue;
            }

            foreach (var dbSet in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (EntityFrameworkTypes.IsDbSetType(dbSet.Type)
                    && ((INamedTypeSymbol)dbSet.Type).TypeArguments[0] is INamedTypeSymbol entityType)
                {
                    pending.Push(entityType);
                }
            }
        }

        while (pending.Count > 0)
        {
            var entityType = pending.Pop();
            if (!entityTypes.Add(entityType))
            {
                continue;
            }

            foreach (var property in entityType.GetMembers().OfType<IPropertySymbol>())
            {
                if (EntityFrameworkTypes.GetNavigationTarget(property.Type) is { } target
                    && SymbolEqualityComparer.Default.Equals(target.ContainingAssembly, compilation.Assembly))
                {
                    pending.Push(target);
                }
            }
        }

        return entityTypes.ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
    }

    private static IEnumerable<INamedTypeSymbol> GetSourceTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            yield return type;
        }

        foreach (var child in @namespace.GetNamespaceMembers())
        {
            foreach (var type in GetSourceTypes(child))
            {
                yield return type;
            }
        }
    }
}
