using System.Collections.Generic;
using System.Collections.Immutable;
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

    public static ImmutableHashSet<INamedTypeSymbol> FindEntityTypes(Compilation compilation)
    {
        var entityTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Stack<INamedTypeSymbol>();

        foreach (var type in GetSourceTypes(compilation.Assembly.GlobalNamespace))
        {
            if (!IsDbContextType(type))
            {
                continue;
            }

            foreach (var dbSet in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (IsDbSetType(dbSet.Type)
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
                if (GetNavigationTarget(property.Type) is { } target
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
