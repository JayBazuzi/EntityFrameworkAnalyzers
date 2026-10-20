# Bazuzi.EntityFrameworkQueryAnalyzers

Roslyn analyzers for Entity Framework Core queries.

## Rules

- Method building a query must return `IQueryable<T>`<!-- singleLineInclude: test\EntityFrameworkQueryAnalyzers.Tests\RulesTableTests.RulesList.verified.md -->

## Example

For example, this method builds a query but materializes it instead of returning `IQueryable<T>`:

<!-- include: test\EntityFrameworkQueryAnalyzers.Tests\NugetReadmeTests.Example.verified.md -->
```cs
public class WidgetManager
{
    private readonly MyDbContext _dbContext = null!;

    public Widget GetFirst() // info: Method 'GetFirst' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
    {
        return _dbContext.Widgets.First();
    }
}
```
<!-- endInclude -->

For more details, see the [full README](https://github.com/JayBazuzi/EntityFrameworkQueryAnalyzers#readme).
