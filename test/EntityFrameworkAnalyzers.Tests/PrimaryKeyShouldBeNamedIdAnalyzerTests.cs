using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.PrimaryKeyShouldBeNamedIdAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class PrimaryKeyShouldBeNamedIdAnalyzerTests
{
    private const string Usings = """
        using System.ComponentModel.DataAnnotations;
        using Microsoft.EntityFrameworkCore;

        """;

    private static string Context(params string[] dbSets) => $$"""

        public class ShopDbContext : DbContext
        {
        {{string.Join("\n", dbSets.Select(s => $"    public DbSet<{s}> {s}s {{ get; set; }} = null!;"))}}
        }
        """;

    [Fact]
    public async Task ConventionalTypeNameId_IsFlagged()
    {
        const string source = Usings + """
            public class Order
            {
                public int {|#0:OrderId|} { get; set; }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source + Context("Order"),
            Verify.Diagnostic(PrimaryKeyShouldBeNamedIdAnalyzer.DiagnosticId).WithLocation(0).WithArguments("OrderId"));
    }

    [Fact]
    public async Task KeyAttributeOnOtherName_IsFlagged()
    {
        const string source = Usings + """
            public class Order
            {
                [Key]
                public int {|#0:Number|} { get; set; }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source + Context("Order"),
            Verify.Diagnostic(PrimaryKeyShouldBeNamedIdAnalyzer.DiagnosticId).WithLocation(0).WithArguments("Number"));
    }

    [Fact]
    public async Task FluentHasKeyOnOtherName_IsFlagged()
    {
        const string source = Usings + """
            public class Order
            {
                public int Number { get; set; }
            }

            public class ShopDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;

                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>().HasKey(o => o.{|#0:Number|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(PrimaryKeyShouldBeNamedIdAnalyzer.DiagnosticId).WithLocation(0).WithArguments("Number"));
    }

    [Fact]
    public async Task KeyNamedId_IsNotFlagged()
    {
        const string source = Usings + """
            public class Order
            {
                public int Id { get; set; }
                public int OrderId { get; set; }
            }

            public class Line
            {
                [Key]
                public int Id { get; set; }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source + Context("Order", "Line"));
    }

    [Fact]
    public async Task CompositeKey_IsNotFlagged()
    {
        const string source = Usings + """
            public class Order
            {
                [Key]
                public int A { get; set; }
                [Key]
                public int B { get; set; }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source + Context("Order"));
    }
}
