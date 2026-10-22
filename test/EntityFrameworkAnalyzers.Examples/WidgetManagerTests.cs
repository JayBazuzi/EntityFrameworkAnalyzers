using Microsoft.EntityFrameworkCore;

namespace Bazuzi.EntityFrameworkAnalyzers.Examples;

public class Widget
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

public class MyDbContext(DbContextOptions<MyDbContext> options) : DbContext(options)
{
    public DbSet<Widget> Widgets { get; set; } = null!;
}

public class WidgetManager(MyDbContext dbContext)
{
    public IQueryable<Widget> GetActiveWidgetsQuery()
    {
        return dbContext.Widgets.Where(w => w.IsActive);
    }
}

public class WidgetManagerTests
{
    [Fact]
    public async Task GetActiveWidgetsQuery_FiltersOnIsActive()
    {
        using var db = CreateDbContext();
        var manager = new WidgetManager(db);

        // begin-snippet: verify-GetActiveWidgetsQuery
        await Verify(manager.GetActiveWidgetsQuery())
            .Snapshot(
                """
                select w.Id,
                       w.IsActive
                from   Widgets as w
                where  w.IsActive = cast (1 as bit)
                """);
        // end-snippet
    }

    private static MyDbContext CreateDbContext()
    {
        return new MyDbContext(new DbContextOptionsBuilder<MyDbContext>().UseSqlServer().Options);
    }
}
