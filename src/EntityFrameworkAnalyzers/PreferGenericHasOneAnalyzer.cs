using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Bazuzi.EntityFrameworkAnalyzers;

/// <summary>
/// Prefer <c>HasOne(o => o.Customer)</c> or <c>HasOne&lt;Customer&gt;("Customer")</c> over the string overloads
/// <c>HasOne("Customer")</c>, <c>HasOne("Customer", "Customer")</c> and <c>HasOne(typeof(Customer), ...)</c>.
/// Only fires when a typed alternative is certain to exist: the related type must be a CLR type that is
/// accessible from the call site, and a single-string navigation must be an accessible property of the entity.
/// Shadow navigations, private/protected navigations, shared-type entities and non-constant arguments are left alone.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferGenericHasOneAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0007";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Prefer generic `HasOne<T>()` over string `HasOne(\"...\")`",
        messageFormat: "Use a lambda or generic overload of 'HasOne' instead of strings",
        category: "Design",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "String-based HasOne is not checked by the compiler and breaks silently on rename.");

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
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name: { Identifier.ValueText: "HasOne" } name } memberAccess
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || method.IsGenericMethod
            || method.ContainingType.Name != "EntityTypeBuilder"
            || method.ContainingNamespace?.ToDisplayString() != "Microsoft.EntityFrameworkCore.Metadata.Builders"
            || method.Parameters.Length is < 1 or > 2)
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count != method.Parameters.Length)
        {
            return;
        }

        var first = arguments[0].Expression;
        var position = invocation.SpanStart;
        var model = context.SemanticModel;

        var typedAlternativeExists = method.Parameters[0].Type switch
        {
            { SpecialType: SpecialType.System_String } when method.Parameters.Length == 1
                => NavigationPropertyExists(model, memberAccess.Expression, first, position, context),
            { SpecialType: SpecialType.System_String }
                => ResolvesToAccessibleClrType(model, first, position, context),
            { Name: "Type" } => first is TypeOfExpressionSyntax typeOf && IsAccessible(model, typeOf.Type, position, context),
            _ => false,
        };

        if (typedAlternativeExists)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, name.GetLocation()));
        }
    }

    // HasOne("Customer") on `Entity(typeof(Order))`: only when Order has an accessible property Customer,
    // so that HasOne(o => o.Customer) compiles. The string overloads exist only on the non-generic builder,
    // so the entity type is recovered from the receiver; any other receiver is skipped.
    private static bool NavigationPropertyExists(
        SemanticModel model, ExpressionSyntax receiver, ExpressionSyntax argument, int position, SyntaxNodeAnalysisContext context)
    {
        if (receiver is not InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Entity" },
                ArgumentList.Arguments: { Count: 1 } entityArguments,
            }
            || entityArguments[0].Expression is not TypeOfExpressionSyntax typeOf
            || model.GetTypeInfo(typeOf.Type, context.CancellationToken).Type is not INamedTypeSymbol entityType
            || model.GetConstantValue(argument, context.CancellationToken) is not { HasValue: true, Value: string navigationName })
        {
            return false;
        }

        return model.LookupSymbols(position, entityType, navigationName, includeReducedExtensionMethods: false)
            .OfType<IPropertySymbol>()
            .Any(p => !p.IsStatic && !p.IsIndexer);
    }

    // HasOne("MyApp.Customer", "Customer"): only when the string names exactly one CLR type we can reference.
    // A string that matches no type may be a shared-type or shadow entity, which has no generic form.
    private static bool ResolvesToAccessibleClrType(
        SemanticModel model, ExpressionSyntax argument, int position, SyntaxNodeAnalysisContext context)
    {
        if (model.GetConstantValue(argument, context.CancellationToken) is not { HasValue: true, Value: string typeName })
        {
            return false;
        }

        var compilation = model.Compilation;
        ITypeSymbol? type = compilation.GetTypeByMetadataName(typeName);
        if (type is null && !typeName.Contains('.'))
        {
            var candidates = compilation.GetSymbolsWithName(typeName, SymbolFilter.Type, context.CancellationToken)
                .OfType<INamedTypeSymbol>()
                .Where(t => t.Arity == 0 && t.TypeKind is TypeKind.Class)
                .ToArray();
            type = candidates.Length == 1 ? candidates[0] : null;
        }

        return type is not null && model.IsAccessible(position, type);
    }

    private static bool IsAccessible(SemanticModel model, TypeSyntax typeSyntax, int position, SyntaxNodeAnalysisContext context)
    {
        return model.GetTypeInfo(typeSyntax, context.CancellationToken).Type is { } type
            && type.TypeKind is not TypeKind.Error
            && model.IsAccessible(position, type);
    }
}
