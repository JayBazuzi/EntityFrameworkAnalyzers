using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers;

/// <summary>
/// The lambda passed to <c>modelBuilder.Entity&lt;T&gt;(...)</c> should name its parameter after <c>T</c>,
/// such as <c>classScheduleEntity</c>, not a generic <c>entity</c>. Any name starting with the
/// camelCase entity name is accepted (<c>classSchedule</c>, <c>classScheduleBuilder</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EntityLambdaParameterNameAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0009";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "`Entity<T>()` lambda parameter should be named after the entity type",
        messageFormat: "Parameter '{0}' should be named '{1}' to match entity type '{2}'",
        category: "Design",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Name the parameter of the lambda passed to Entity<T>() after T, such as 'classScheduleEntity'. "
            + "A generic name like 'entity' makes long OnModelCreating methods hard to follow.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ArgumentList.Arguments.Count != 1
            || !IsEntityCall(invocation)
            || GetParameter(invocation.ArgumentList.Arguments[0].Expression) is not { } parameter
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol
            {
                Name: "Entity",
                TypeArguments.Length: 1,
                ContainingType: { Name: "ModelBuilder" } containingType,
            } method
            || containingType.ContainingNamespace?.ToDisplayString() != "Microsoft.EntityFrameworkCore")
        {
            return;
        }

        var entityType = method.TypeArguments[0];

        var prefix = ToCamelCase(entityType.Name);
        var name = parameter.Identifier.ValueText;
        if (name.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule, parameter.Identifier.GetLocation(), name, prefix + "Entity", entityType.Name));
    }

    private static bool IsEntityCall(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == "Entity",
            GenericNameSyntax generic => generic.Identifier.ValueText == "Entity",
            _ => false,
        };
    }

    private static ParameterSyntax? GetParameter(ExpressionSyntax expression)
    {
        return expression switch
        {
            SimpleLambdaExpressionSyntax lambda => lambda.Parameter,
            ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 1 } lambda => lambda.ParameterList.Parameters[0],
            _ => null,
        };
    }

    private static string ToCamelCase(string name)
    {
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
