
```cs
    public Widget GetFirstActive() // info: Method 'GetFirstActive' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
    {
        return _dbContext.Widgets.Where(w => w.IsActive).First();
    }
```