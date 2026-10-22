using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkQueryAnalyzers;

/// <summary>
/// Flags methods that build a query — either by reading a DbContext's DbSet (directly, or via
/// DbContext.Set&lt;T&gt;()), or by chaining more LINQ operators onto a call to another method that
/// already returns IQueryable&lt;T&gt; — but don't themselves return IQueryable&lt;T&gt;. Query
/// construction should stay lazy and composable up to the caller; only the caller should decide
/// when/how to materialize.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DbContextQueryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0001";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Method building a query must return `IQueryable<T>`",
        messageFormat: "Method '{0}' builds a query from '{1}' but returns '{2}' instead of IQueryable<T>",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Methods that build a query, whether by reading a DbContext's DbSet directly or by " +
            "adding more operators onto another method's IQueryable<T> result, should themselves return " +
            "IQueryable<T> so callers control query composition and materialization.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        if (methodDeclaration.Body is null && methodDeclaration.ExpressionBody is null)
        {
            return;
        }

        var semanticModel = context.SemanticModel;
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken);
        if (methodSymbol is null)
        {
            return;
        }

        if (IsIQueryable(methodSymbol.ReturnType))
        {
            return;
        }

        var querySource = FindDbSetAccess(methodDeclaration, semanticModel, context.CancellationToken)
            ?? FindComposedQueryableCall(methodDeclaration, semanticModel, context.CancellationToken);
        if (querySource is null)
        {
            return;
        }

        var diagnostic = Diagnostic.Create(
            Rule,
            methodDeclaration.Identifier.GetLocation(),
            methodSymbol.Name,
            querySource.Value.sourceDescription,
            methodSymbol.ReturnType.ToDisplayString());

        context.ReportDiagnostic(diagnostic);
    }

    // Flags `GetQuery().Where(...)` inside a method that isn't itself IQueryable<T>-returning:
    // GetQuery() is already its own testable/composable unit, so the extra `.Where(...)` is new,
    // un-extracted query logic buried alongside whatever else the method does.
    private static (string sourceDescription, Location location)? FindComposedQueryableCall(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        foreach (var invocation in methodDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
                || memberAccess.Expression is not InvocationExpressionSyntax receiverInvocation)
            {
                continue;
            }

            if (!IsIQueryable(semanticModel.GetTypeInfo(invocation, cancellationToken).Type))
            {
                continue;
            }

            if (semanticModel.GetSymbolInfo(receiverInvocation, cancellationToken).Symbol is not IMethodSymbol { DeclaringSyntaxReferences.Length: > 0 } receiverSymbol
                || !IsIQueryable(receiverSymbol.ReturnType))
            {
                continue;
            }

            return ($"{receiverSymbol.Name}()", memberAccess.GetLocation());
        }

        return null;
    }

    private static (string sourceDescription, Location location)? FindDbSetAccess(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        System.Threading.CancellationToken cancellationToken)
    {
        // Direct member access, e.g. `_dbContext.AspnetUsers`.
        foreach (var memberAccess in methodDeclaration.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            var symbolInfo = semanticModel.GetSymbolInfo(memberAccess, cancellationToken);
            var symbol = symbolInfo.Symbol;

            if (symbol is IPropertySymbol propertySymbol
                && EntityFrameworkTypes.IsDbContextType(propertySymbol.ContainingType)
                && EntityFrameworkTypes.IsDbSetType(propertySymbol.Type))
            {
                if (IsWriteAccess(memberAccess))
                {
                    continue;
                }

                return (propertySymbol.ContainingType.ToDisplayString(), memberAccess.GetLocation());
            }

            // DbContext.Set<T>() calls.
            if (symbol is IMethodSymbol methodSymbol
                && methodSymbol.Name == "Set"
                && EntityFrameworkTypes.IsDbContextType(methodSymbol.ContainingType)
                && EntityFrameworkTypes.IsDbSetType(methodSymbol.ReturnType))
            {
                var receiverType = semanticModel.GetTypeInfo(memberAccess.Expression, cancellationToken).Type;
                return (receiverType?.ToDisplayString() ?? methodSymbol.ContainingType.ToDisplayString(), memberAccess.GetLocation());
            }
        }

        return null;
    }

    private static readonly ImmutableHashSet<string> WriteMethodNames = ImmutableHashSet.Create(
        "Add", "AddAsync", "AddRange", "AddRangeAsync",
        "Remove", "RemoveRange",
        "Update", "UpdateRange",
        "Attach", "AttachRange");

    // True when a DbSet access like `_dbContext.Classes` is only used as the receiver of a
    // write call (`.Add(...)`, `.Remove(...)`, etc.), not to build or materialize a query.
    private static bool IsWriteAccess(MemberAccessExpressionSyntax memberAccess)
    {
        return memberAccess.Parent is MemberAccessExpressionSyntax parentAccess
            && parentAccess.Expression == memberAccess
            && WriteMethodNames.Contains(parentAccess.Name.Identifier.Text);
    }

    private static bool IsIQueryable(ITypeSymbol? type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } named
            && named.ConstructedFrom.ToDisplayString() == "System.Linq.IQueryable<T>";
    }
}
