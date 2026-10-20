# Bazuzi.EntityFrameworkQueryAnalyzers

[![NuGet](https://img.shields.io/nuget/v/Bazuzi.EntityFrameworkQueryAnalyzers.svg)](https://www.nuget.org/packages/Bazuzi.EntityFrameworkQueryAnalyzers)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bazuzi.EntityFrameworkQueryAnalyzers.svg)](https://www.nuget.org/packages/Bazuzi.EntityFrameworkQueryAnalyzers)
[![Build and Test](https://github.com/JayBazuzi/EntityFrameworkQueryAnalyzers/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/JayBazuzi/EntityFrameworkQueryAnalyzers/actions/workflows/build-and-test.yml)

Roslyn analyzers for Entity Framework Core queries.

## Rules

<!-- include: test\EntityFrameworkQueryAnalyzers.Tests\RulesTableTests.RulesTable.verified.md -->
| Id | Title |
| --- | --- |
| ENTITY_FRAMEWORK_0001 | Method building a query must return `IQueryable<T>` |
<!-- endInclude -->

## Examples

Keep query construction separate from execution: a method that builds a query from a `DbContext` should return `IQueryable<T>`. The query can then be tested with Verify unit tests. See [Verify.EntityFramework](https://github.com/VerifyTests/Verify.EntityFramework).

<!-- snippet: DbContextQueryAnalyzerExample -->
<a id='snippet-DbContextQueryAnalyzerExample'></a>
```cs
public class WidgetManager
{
    private readonly MyDbContext _dbContext = null!;

    public Widget {|#0:GetFirstActive|}() // info: Method 'GetFirstActive' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
    {
        return _dbContext.Widgets.Where(w => w.IsActive).First();
    }
}
```
<sup><a href='/test/EntityFrameworkQueryAnalyzers.Tests/DbContextQueryAnalyzerTests.cs#L23-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-DbContextQueryAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
