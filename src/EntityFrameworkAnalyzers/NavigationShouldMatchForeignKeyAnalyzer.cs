using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers;

/// <summary>
/// In fluent configuration such as <c>HasOne(o =&gt; o.Customer)...HasForeignKey(o =&gt; o.CustomerId)</c>,
/// the navigation property should be named after the foreign key: the foreign key name without its "Id" suffix.
/// The foreign key is a database column, so the navigation is the side that can be renamed without a migration.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NavigationShouldMatchForeignKeyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0004";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Navigation property name should match the foreign key",
        messageFormat: "Navigation property '{0}' should be named '{1}' to match foreign key '{2}'",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Name the navigation property after the foreign key it is backed by, without the 'Id' suffix, " +
            "so HasOne and HasForeignKey visibly refer to the same relationship.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetMethodName(invocation) != "HasForeignKey"
            || invocation.ArgumentList.Arguments.Count != 1
            || !IsEntityFrameworkMethod(context, invocation))
        {
            return;
        }

        var (hasOne, navigation, location) = FindHasOneNavigation(invocation);
        if (hasOne is null || navigation is null || location is null)
        {
            return;
        }

        var foreignKey = GetNameAndLocation(invocation.ArgumentList.Arguments[0].Expression).name;
        if (foreignKey is null
            || foreignKey.Length <= "Id".Length
            || !foreignKey.EndsWith("Id", StringComparison.Ordinal))
        {
            return;
        }

        var expected = foreignKey.Substring(0, foreignKey.Length - "Id".Length);
        if (string.Equals(navigation, expected, StringComparison.Ordinal))
        {
            return;
        }

        if (PrincipalHasProperty(context, hasOne, foreignKey))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, location, navigation, expected, foreignKey));
    }

    private static bool IsEntityFrameworkMethod(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var method = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol as IMethodSymbol;
        return method?.ContainingNamespace?.ToDisplayString().StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true;
    }

    // When the principal has a property with the foreign key's name, the foreign key is named after the
    // principal's key rather than the principal, as in a shared primary key (Customer.PersonId as both key and FK to Person).
    private static bool PrincipalHasProperty(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax hasOne, string propertyName)
    {
        var method = context.SemanticModel.GetSymbolInfo(hasOne, context.CancellationToken).Symbol as IMethodSymbol;
        if (method is null || method.TypeArguments.Length != 1)
        {
            return false;
        }

        for (var type = method.TypeArguments[0]; type is not null; type = type.BaseType)
        {
            foreach (var member in type.GetMembers(propertyName))
            {
                if (member is IPropertySymbol)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Walks back along the fluent chain looking for HasOne(x => x.Nav).
    private static (InvocationExpressionSyntax? hasOne, string? name, Location? location) FindHasOneNavigation(InvocationExpressionSyntax invocation)
    {
        ExpressionSyntax? current = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
        while (current is InvocationExpressionSyntax call)
        {
            if (GetMethodName(call) == "HasOne")
            {
                if (call.ArgumentList.Arguments.Count != 1)
                {
                    return (null, null, null);
                }

                var (name, location) = GetNameAndLocation(call.ArgumentList.Arguments[0].Expression);
                return (call, name, location);
            }

            current = (call.Expression as MemberAccessExpressionSyntax)?.Expression;
        }

        return (null, null, null);
    }

    // Extracts the member name from `x => x.Name` or "Name", with the location to report on.
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
