using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers;

/// <summary>
/// A foreign key property's name must end with the name of the entity it points to followed by "Id",
/// e.g. <c>CreatedByUserId</c> rather than <c>CreatedById</c> for a key to <c>User</c>.
/// Foreign keys are found by EF's naming convention, by [ForeignKey], and by fluent HasForeignKey.
/// Composite keys, keys to alternate keys (HasPrincipalKey) and shadow properties are skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ForeignKeyNameShouldMatchEntityAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0005";

    private const string KeySuffix = "Id";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Foreign key name should end with the entity name and `Id`",
        messageFormat: "Foreign key '{0}' should end with '{1}'",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Name a foreign key after the entity it points to, followed by 'Id', so the name says what it references. " +
            "A prefix can describe the role, e.g. 'CreatedByUserId'.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            var entityTypes = EntityFrameworkTypes.FindEntityTypes(compilationContext.Compilation);
            if (entityTypes.Count > 0)
            {
                compilationContext.RegisterSymbolAction(
                    symbolContext => AnalyzeEntityType(symbolContext, entityTypes),
                    SymbolKind.NamedType);
            }

            compilationContext.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
        });
    }

    // Conventional and [ForeignKey] foreign keys, found from the reference navigations of an entity type.
    private static void AnalyzeEntityType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> entityTypes)
    {
        var entityType = (INamedTypeSymbol)context.Symbol;
        if (!entityTypes.Contains(entityType))
        {
            return;
        }

        var reported = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);

        foreach (var navigation in entityType.GetMembers().OfType<IPropertySymbol>())
        {
            var principal = EntityFrameworkTypes.GetNavigationTarget(navigation.Type);
            if (principal is null || !SymbolEqualityComparer.Default.Equals(principal, navigation.Type))
            {
                continue;
            }

            var foreignKey = FindForeignKey(entityType, navigation, principal);
            if (foreignKey is not null
                && foreignKey.Locations[0].IsInSource
                && reported.Add(foreignKey)
                && !HasExpectedSuffix(foreignKey.Name, principal.Name))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Rule,
                    foreignKey.Locations[0],
                    foreignKey.Name,
                    principal.Name + KeySuffix));
            }
        }
    }

    private static IPropertySymbol? FindForeignKey(INamedTypeSymbol entityType, IPropertySymbol navigation, INamedTypeSymbol principal)
    {
        var properties = entityType.GetMembers().OfType<IPropertySymbol>().ToArray();

        // [ForeignKey("X")] on the navigation names the foreign key property.
        if (GetForeignKeyAttributeArgument(navigation) is { } named)
        {
            return named.Contains(",") ? null : properties.FirstOrDefault(p => p.Name == named);
        }

        // [ForeignKey("Nav")] on a property names the navigation it belongs to.
        var annotated = properties.FirstOrDefault(p => GetForeignKeyAttributeArgument(p) == navigation.Name);
        if (annotated is not null)
        {
            return annotated;
        }

        // EF convention: <Navigation>Id or <PrincipalType>Id.
        return properties.FirstOrDefault(p => p.Name == navigation.Name + KeySuffix)
            ?? properties.FirstOrDefault(p => p.Name == principal.Name + KeySuffix);
    }

    private static string? GetForeignKeyAttributeArgument(IPropertySymbol property)
    {
        foreach (var attribute in property.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is string value)
            {
                return value;
            }
        }

        return null;
    }

    // Fluent HasForeignKey(o => o.X) or HasForeignKey("X"). The principal comes from the builder's type arguments.
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetMethodName(invocation) != "HasForeignKey"
            || invocation.ArgumentList.Arguments.Count != 1
            || ChainContains(invocation, "HasPrincipalKey")
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) != true)
        {
            return;
        }

        var principal = GetPrincipal(method);
        var (foreignKey, location) = GetNameAndLocation(invocation.ArgumentList.Arguments[0].Expression);
        if (principal is null || foreignKey is null || location is null || HasExpectedSuffix(foreignKey, principal.Name))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, foreignKey, principal.Name + KeySuffix));
    }

    // ReferenceCollectionBuilder<TPrincipal, TDependent>.HasForeignKey, or
    // ReferenceReferenceBuilder<TEntity, TRelated>.HasForeignKey<TDependent>, where the principal is the other type.
    private static INamedTypeSymbol? GetPrincipal(IMethodSymbol method)
    {
        var builder = method.ContainingType;
        if (builder.TypeArguments.Length != 2)
        {
            return null;
        }

        INamedTypeSymbol? principal;
        if (builder.Name == "ReferenceCollectionBuilder")
        {
            principal = builder.TypeArguments[0] as INamedTypeSymbol;
        }
        else if (builder.Name == "ReferenceReferenceBuilder" && method.TypeArguments.Length == 1)
        {
            principal = Enumerable.FirstOrDefault(builder.TypeArguments,
                t => !SymbolEqualityComparer.Default.Equals(t, method.TypeArguments[0])) as INamedTypeSymbol;
        }
        else
        {
            return null;
        }

        return principal is { TypeKind: TypeKind.Class } ? principal : null;
    }

    private static bool HasExpectedSuffix(string foreignKey, string principalName)
    {
        return foreignKey.EndsWith(principalName + KeySuffix, StringComparison.Ordinal);
    }

    // Looks at the whole fluent chain: the calls before this one and the calls chained after it.
    private static bool ChainContains(InvocationExpressionSyntax invocation, string methodName)
    {
        for (var parent = invocation.Parent; parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax next }; parent = next.Parent)
        {
            if (GetMethodName(next) == methodName)
            {
                return true;
            }
        }

        ExpressionSyntax? current = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
        while (current is InvocationExpressionSyntax call)
        {
            if (GetMethodName(call) == methodName)
            {
                return true;
            }

            current = (call.Expression as MemberAccessExpressionSyntax)?.Expression;
        }

        return false;
    }

    // Extracts the member name from `x => x.Name` or "Name", with the location to report on.
    // Anything else, such as `x => new { x.A, x.B }` for a composite key, is skipped.
    private static (string? name, Location? location) GetNameAndLocation(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText.Contains(",") ? (null, null) : (literal.Token.ValueText, literal.GetLocation());
            case SimpleLambdaExpressionSyntax { ExpressionBody: MemberAccessExpressionSyntax access }:
                return (access.Name.Identifier.ValueText, access.Name.GetLocation());
            case ParenthesizedLambdaExpressionSyntax { ExpressionBody: MemberAccessExpressionSyntax access }:
                return (access.Name.Identifier.ValueText, access.Name.GetLocation());
            default:
                return (null, null);
        }
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };
    }
}
