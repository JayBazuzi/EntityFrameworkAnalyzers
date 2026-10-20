
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