# Bazuzi.EntityFrameworkAnalyzers

[![NuGet](https://img.shields.io/nuget/v/Bazuzi.EntityFrameworkAnalyzers.svg)](https://www.nuget.org/packages/Bazuzi.EntityFrameworkAnalyzers)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Bazuzi.EntityFrameworkAnalyzers.svg)](https://www.nuget.org/packages/Bazuzi.EntityFrameworkAnalyzers)
[![Build and Test](https://github.com/JayBazuzi/EntityFrameworkAnalyzers/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/JayBazuzi/EntityFrameworkAnalyzers/actions/workflows/build-and-test.yml)

Roslyn analyzers for Entity Framework Core queries.

## Rules

<!-- include: test\EntityFrameworkAnalyzers.Tests\RulesTableTests.RulesTable.verified.md -->
| Id | Title |
| --- | --- |
| ENTITY_FRAMEWORK_0001 | Method building a query must return `IQueryable<T>` |
| ENTITY_FRAMEWORK_0002 | Navigation property must only be read inside a query |
| ENTITY_FRAMEWORK_0003 | Navigation property should be `virtual` |
| ENTITY_FRAMEWORK_0004 | Navigation property name should match the foreign key |
| ENTITY_FRAMEWORK_0005 | Foreign key name should end with the entity name and `Id` |
| ENTITY_FRAMEWORK_0006 | Primary key should be named `Id` |
<!-- endInclude -->

ENTITY_FRAMEWORK_0002 is Info by default because you may prefer to use lazy loading and/or `Include()`. But if your style is to always project your read queries, you can elevate it to Warning.

## Method building a query must return `IQueryable<T>`

Keep query construction separate from execution: a method that builds a query from a `DbContext` should return `IQueryable<T>`. The query can then be tested with Verify unit tests. See [Verify.EntityFramework](https://github.com/VerifyTests/Verify.EntityFramework).

<!-- snippet: DbContextQueryAnalyzerExample -->
<a id='snippet-DbContextQueryAnalyzerExample'></a>
```cs
public Widget {|#0:GetFirstActive|}() // info: Method 'GetFirstActive' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
{
    return _dbContext.Widgets.Where(w => w.IsActive).First();
}
```
<sup><a href='/test/EntityFrameworkAnalyzers.Tests/DbContextQueryAnalyzerTests.cs#L28-L33' title='Snippet source file'>snippet source</a> | <a href='#snippet-DbContextQueryAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Navigation property must only be read inside a query

Without lazy loading, a navigation property that wasn't loaded with `Include()` is silently `null` or empty, which looks just like real data. Read navigations only inside the query (`Select`, `Where`, `OrderBy`, ...), for example by projecting into a DTO, so EF translates them to SQL:

<!-- snippet: NavigationPropertyReadAnalyzerExample -->
<a id='snippet-NavigationPropertyReadAnalyzerExample'></a>
```cs
var order = db.Orders.Single(o => o.Id == id); // forgot .Include(o => o.Lines)
return {|#0:order.Lines|}.Sum(l => l.Price); // info: Navigation property 'Order.Lines' is read outside a query; it is null or empty unless it was loaded
```
<sup><a href='/test/EntityFrameworkAnalyzers.Tests/NavigationPropertyReadAnalyzerTests.cs#L47-L50' title='Snippet source file'>snippet source</a> | <a href='#snippet-NavigationPropertyReadAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

That rule recognizes navigation properties by `virtual`, so mark navigations on your entity types `virtual`:

## Navigation property should be `virtual`

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
<sup><a href='/test/EntityFrameworkAnalyzers.Tests/NavigationPropertyShouldBeVirtualAnalyzerTests.cs#L24-L31' title='Snippet source file'>snippet source</a> | <a href='#snippet-NavigationPropertyShouldBeVirtualAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Navigation property name should match the foreign key

When a navigation property and its foreign key describe the same relationship, their names should agree: `CustomerId` goes with `Customer`, `BuyerId` goes with `Buyer`. A mismatch makes it unclear which property is the other's partner.

<!-- snippet: ForeignKeyNameShouldMatchNavigationAnalyzerExample -->
<a id='snippet-ForeignKeyNameShouldMatchNavigationAnalyzerExample'></a>
```cs
modelBuilder.Entity<Order>()
    .HasOne(o => o.{|#0:Customer|}) // info: Navigation property 'Customer' should be named 'Buyer' to match foreign key 'BuyerId'
    .WithMany()
    .HasForeignKey(o => o.BuyerId);
```
<sup><a href='/test/EntityFrameworkAnalyzers.Tests/ForeignKeyNameShouldMatchNavigationAnalyzerTests.cs#L33-L38' title='Snippet source file'>snippet source</a> | <a href='#snippet-ForeignKeyNameShouldMatchNavigationAnalyzerExample' title='Start of snippet'>anchor</a></sup>
<!-- endSnippet -->

## Foreign key name should end with the entity name and `Id`

A foreign key name should say what it references. `OwnerId` doesn't tell you whether it points to a `User`, a `Customer` or a `Team`. `CreatedByUserId` does, and it still lets the prefix describe the role.

The rule reports a foreign key property whose name does not end with `{PrincipalEntityName}Id`:

| Property | Principal | Result |
| --- | --- | --- |
| `UserId` | `User` | OK |
| `CreatedByUserId` | `User` | OK |
| `ParentCategoryId` | `Category` | OK |
| `CreatedById` | `User` | Reported |
| `OwnerId` | `User` | Reported |

Foreign keys are read from the EF model, so keys configured by convention, `[ForeignKey]` or the Fluent API are all covered. Composite keys, foreign keys to alternate keys and shadow properties are skipped. See [docs/foreign-key-naming.md](docs/foreign-key-naming.md) for details.

## Primary key should be named `Id`

Name every primary key `Id`, not `OrderId` or `Number`.

Primary keys are read from the EF model, so keys configured by convention (`Id` or `{TypeName}Id`), `[Key]` or `HasKey` are all covered. Composite keys are skipped.
