using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.DbContextQueryAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class DbContextQueryAnalyzerTests
{
    private const string Preamble = """
        using System.Linq;
        using Microsoft.EntityFrameworkCore;

        public class Widget
        {
            public bool IsActive { get; set; }
        }

        public class MyDbContext : DbContext
        {
            public DbSet<Widget> Widgets { get; set; } = null!;
        }
        """;

    internal const string DirectDbSetAccessSource = Preamble + """

        public class WidgetManager
        {
            private readonly MyDbContext _dbContext = null!;

            // begin-snippet: DbContextQueryAnalyzerExample
            public Widget {|#0:GetFirstActive|}() // info: Method 'GetFirstActive' builds a query from 'MyDbContext' but returns 'Widget' instead of IQueryable<T>
            {
                return _dbContext.Widgets.Where(w => w.IsActive).First();
            }
            // end-snippet
        }
        """;

    [Fact]
    public async Task DirectDbSetAccess_NotReturningIQueryable_IsFlagged()
    {
        var expected = Verify.Diagnostic(DbContextQueryAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("GetFirstActive", "MyDbContext", "Widget");

        await Verify.VerifyAnalyzerAsync(DirectDbSetAccessSource, expected);
    }

    [Fact]
    public async Task DirectDbSetAccess_ReturningIQueryable_IsNotFlagged()
    {
        const string source = Preamble + """

            public class WidgetManager
            {
                private readonly MyDbContext _dbContext = null!;

                public IQueryable<Widget> GetActiveWidgetsQuery()
                {
                    return _dbContext.Widgets.Where(w => w.IsActive);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SetOfTAccess_NotReturningIQueryable_IsFlagged()
    {
        const string source = Preamble + """

            public class WidgetManager
            {
                private readonly MyDbContext _dbContext = null!;

                public Widget {|#0:GetFirst|}()
                {
                    return _dbContext.Set<Widget>().First();
                }
            }
            """;

        var expected = Verify.Diagnostic(DbContextQueryAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("GetFirst", "MyDbContext", "Widget");

        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task MaterializingWrapperThatDelegatesToAnotherMethod_IsNotFlagged()
    {
        const string source = Preamble + """

            public class WidgetManager
            {
                private readonly MyDbContext _dbContext = null!;

                public IQueryable<Widget> GetQuery()
                {
                    return _dbContext.Widgets.Where(w => true);
                }

                public Widget GetFirst()
                {
                    return GetQuery().First();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ChainingMoreOperatorsOntoAnotherQueryMethod_IsFlagged()
    {
        const string source = Preamble + """

            public class WidgetManager
            {
                private readonly MyDbContext _dbContext = null!;

                public IQueryable<Widget> GetQuery()
                {
                    return _dbContext.Widgets.Where(w => true);
                }

                public Widget {|#0:GetFirstMatching|}()
                {
                    return GetQuery().Where(w => true).First();
                }
            }
            """;

        var expected = Verify.Diagnostic(DbContextQueryAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("GetFirstMatching", "GetQuery()", "Widget");

        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task WriteOperationsOnDbContext_AreNotFlagged()
    {
        const string source = Preamble + """

            public class WidgetManager
            {
                private readonly MyDbContext _dbContext = null!;

                public void Save(Widget widget)
                {
                    _dbContext.Add(widget);
                    _dbContext.SaveChanges();
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
