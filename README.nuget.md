# Bazuzi.EntityFrameworkAnalyzers

Roslyn analyzers for Entity Framework Core queries.

## Rules

- Method building a query must return `IQueryable<T>`<!-- include: test\EntityFrameworkAnalyzers.Tests\RulesTableTests.RulesList.verified.md -->
- Navigation property must only be read inside a query
- Navigation property should be `virtual`
- Navigation property name should match the foreign key<!-- endInclude -->

## Example

For example, this method builds a query but materializes it instead of returning `IQueryable<T>`:

<!-- include: test\EntityFrameworkAnalyzers.Tests\NugetReadmeTests.Example.verified.md -->
```cs
    public Widget GetFirstActive() // info: Method 'GetFirstActive' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
    {
        return _dbContext.Widgets.Where(w => w.IsActive).First();
    }
```
<!-- endInclude -->

For more details, see the [full README](https://github.com/JayBazuzi/EntityFrameworkAnalyzers#readme).
