using Microsoft.CodeAnalysis;

namespace Bazuzi.EntityFrameworkAnalyzers;

internal static class EntityFrameworkTypes
{
    public static bool IsDbContextType(ITypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == "Microsoft.EntityFrameworkCore.DbContext")
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsDbSetType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { IsGenericType: true } named
            && named.ConstructedFrom.ToDisplayString() == "Microsoft.EntityFrameworkCore.DbSet<TEntity>";
    }

    // Returns the entity type a property of this type would navigate to — the type itself for a
    // reference navigation, or the element type for a collection navigation — or null when the
    // type can't be a navigation (scalars, strings, byte[], framework types, ...).
    public static INamedTypeSymbol? GetNavigationTarget(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
        {
            return null;
        }

        var elementType = GetEnumerableElementType(named);
        if (elementType is not null)
        {
            return IsEntityShaped(elementType) ? (INamedTypeSymbol)elementType : null;
        }

        return IsEntityShaped(named) ? named : null;
    }

    private static ITypeSymbol? GetEnumerableElementType(INamedTypeSymbol type)
    {
        if (IsGenericEnumerable(type))
        {
            return type.TypeArguments[0];
        }

        foreach (var @interface in type.AllInterfaces)
        {
            if (IsGenericEnumerable(@interface))
            {
                return @interface.TypeArguments[0];
            }
        }

        return null;
    }

    private static bool IsGenericEnumerable(INamedTypeSymbol type)
    {
        return type.IsGenericType
            && type.ConstructedFrom.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T;
    }

    private static bool IsEntityShaped(ITypeSymbol type)
    {
        return type is INamedTypeSymbol { TypeKind: TypeKind.Class, SpecialType: SpecialType.None } named
            && !IsInSystemNamespace(named)
            && GetEnumerableElementType(named) is null;
    }

    private static bool IsInSystemNamespace(INamedTypeSymbol type)
    {
        var root = type.ContainingNamespace;
        while (root is { ContainingNamespace.IsGlobalNamespace: false })
        {
            root = root.ContainingNamespace;
        }

        return root is { Name: "System" };
    }
}
