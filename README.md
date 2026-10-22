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
| ENTITY_FRAMEWORK_0002 | Navigation property must only be read inside a query |
| ENTITY_FRAMEWORK_0003 | Navigation property should be `virtual` |
| ENTITY_FRAMEWORK_0004 | Navigation property name should match the foreign key |
<!-- endInclude -->

## Examples

Keep query construction separate from execution: a method that builds a query from a `DbContext` should return `IQueryable<T>`. The query can then be tested with Verify unit tests. See [Verify.EntityFramework](https://github.com/VerifyTests/Verify.EntityFramework).

<!-- snippet: DbContextQueryAnalyzerExample -->
<a id='snippet-DbContextQueryAnalyzerExample'></a>
```cs
public Widget {|#0:GetFirstActive|}() // info: Method 'GetFirstActive' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
{
    return _dbContext.Widgets.Where(w => w.IsActive).First();
}
```
<sup><a href='/test/EntityFrameworkQueryAnalyzers.Tests/DbContextQueryAnalyzerTests.cs#L28-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-DbContextQueryAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

Without lazy loading, a navigation property that wasn't loaded with `Include()` is silently `null` or empty, which looks just like real data. Read navigations only inside the query (`Select`, `Where`, `OrderBy`, ...), for example by projecting into a DTO, so EF translates them to SQL:

<!-- snippet: NavigationPropertyReadAnalyzerExample -->
<a id='snippet-NavigationPropertyReadAnalyzerExample'></a>
```cs
var order = db.Orders.Single(o => o.Id == id); // forgot .Include(o => o.Lines)
return {|#0:order.Lines|}.Sum(l => l.Price); // info: Navigation property 'Order.Lines' is read outside a query; it is null or empty unless it was loaded
```
<sup><a href='/test/EntityFrameworkQueryAnalyzers.Tests/NavigationPropertyReadAnalyzerTests.cs#L47-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-NavigationPropertyReadAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

That rule recognizes navigation properties by `virtual`, so mark navigations on your entity types `virtual`:

<!-- snippet: NavigationPropertyShouldBeVirtualAnalyzerExample -->
<a id='snippet-NavigationPropertyShouldBeVirtualAnalyzerExample'></a>
```cs
public class Order
{
    public int Id { get; set; }
    public Customer {|#0:Customer|} { get; set; } = null!; // info: Navigation property 'Order.Customer' should be virtual
    public ICollection<OrderLine> {|#1:Lines|} { get; set; } = new List<OrderLine>(); // info: Navigation property 'Order.Lines' should be virtual
}
```
<sup><a href='/test/EntityFrameworkQueryAnalyzers.Tests/NavigationPropertyShouldBeVirtualAnalyzerTests.cs#L24-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-NavigationPropertyShouldBeVirtualAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->
