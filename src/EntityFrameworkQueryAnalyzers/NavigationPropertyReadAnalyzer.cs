using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Bazuzi.EntityFrameworkQueryAnalyzers;

/// <summary>
/// Flags reads of navigation properties (recognized by being <c>virtual</c> with an entity or
/// entity-collection type) outside of an expression tree. Without lazy loading, a navigation that
/// wasn't loaded with Include() is silently null or empty, which looks just like real data. Inside
/// an expression tree EF translates the navigation to SQL, so it's always correct; elsewhere, project
/// the needed data into a DTO in the query instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NavigationPropertyReadAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "ENTITY_FRAMEWORK_0002";

    public static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Navigation property must only be read inside a query",
        messageFormat: "Navigation property '{0}.{1}' is read outside a query; it is null or empty unless it was loaded",
        category: "Reliability",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Without lazy loading, a navigation property that wasn't loaded with Include() is null " +
            "or empty, which is indistinguishable from real data. Read navigations inside the query, e.g. by " +
            "projecting into a DTO with Select(), so EF translates them to SQL.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;
        var property = propertyReference.Property;

        if (!property.IsVirtual || property.IsStatic
            || EntityFrameworkTypes.GetNavigationTarget(property.Type) is null)
        {
            return;
        }

        if (IsWrite(propertyReference)
            || IsInsideNameOf(propertyReference)
            || IsInsideExpressionTree(propertyReference, context.CancellationToken))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            propertyReference.Syntax.GetLocation(),
            property.ContainingType.Name,
            property.Name));
    }

    // `order.Customer = customer` and `new Order { Customer = customer }`.
    private static bool IsWrite(IOperation operation)
    {
        return operation.Parent is ISimpleAssignmentOperation assignment && assignment.Target == operation;
    }

    private static bool IsInsideNameOf(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
            {
                return true;
            }
        }

        return false;
    }

    // Any lambda converted to Expression<TDelegate> — Select, Where, Include, HasOne, ... — is
    // translated by EF rather than executed against loaded entities.
    private static bool IsInsideExpressionTree(IOperation operation, System.Threading.CancellationToken cancellationToken)
    {
        var semanticModel = operation.SemanticModel;
        if (semanticModel is null)
        {
            return false;
        }

        foreach (var lambda in operation.Syntax.AncestorsAndSelf())
        {
            if (lambda is not LambdaExpressionSyntax)
            {
                continue;
            }

            var convertedType = semanticModel.GetTypeInfo(lambda, cancellationToken).ConvertedType;
            if (IsExpressionType(convertedType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsExpressionType(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "System.Linq.Expressions.LambdaExpression")
            {
                return true;
            }
        }

        return false;
    }
}
