using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers;

/// <summary>
/// An entity's primary key property must be named "Id", not "OrderId" or "Number".
/// Primary keys are found by EF's naming convention (<c>Id</c> or <c>&lt;Type&gt;Id</c>), by [Key],
/// and by fluent HasKey. Composite keys are skipped.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PrimaryKeyShouldBeNamedIdAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0006";

    private const string KeyName = "Id";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Primary key should be named `Id`",
        messageFormat: "Primary key '{0}' should be named 'Id'",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Name every primary key 'Id'. A foreign key then reads as the entity name followed by 'Id'.");

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

    // Conventional and [Key] primary keys.
    private static void AnalyzeEntityType(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> entityTypes)
    {
        var entityType = (INamedTypeSymbol)context.Symbol;
        if (!entityTypes.Contains(entityType))
        {
            return;
        }

        var properties = entityType.GetMembers().OfType<IPropertySymbol>().ToArray();

        var keyed = properties.Where(HasKeyAttribute).ToArray();
        IPropertySymbol? primaryKey;
        if (keyed.Length > 0)
        {
            primaryKey = keyed.Length == 1 ? keyed[0] : null;
        }
        else if (properties.Any(p => p.Name == KeyName))
        {
            primaryKey = null;
        }
        else
        {
            primaryKey = properties.FirstOrDefault(p => p.Name == entityType.Name + KeyName);
        }

        if (primaryKey is not null && primaryKey.Name != KeyName && primaryKey.Locations[0].IsInSource)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, primaryKey.Locations[0], primaryKey.Name));
        }
    }

    private static bool HasKeyAttribute(IPropertySymbol property)
    {
        return property.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == "System.ComponentModel.DataAnnotations.KeyAttribute");
    }

    // Fluent HasKey(o => o.X) or HasKey("X"). A composite key such as `o => new { o.A, o.B }` is skipped.
    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetMethodName(invocation) != "HasKey"
            || invocation.ArgumentList.Arguments.Count != 1
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.ContainingType.Name != "EntityTypeBuilder"
            || method.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) != true)
        {
            return;
        }

        var (name, location) = GetNameAndLocation(invocation.ArgumentList.Arguments[0].Expression);
        if (name is null || location is null || name == KeyName)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, name));
    }

    private static (string? name, Location? location) GetNameAndLocation(ExpressionSyntax expression)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return (literal.Token.ValueText, literal.GetLocation());
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
