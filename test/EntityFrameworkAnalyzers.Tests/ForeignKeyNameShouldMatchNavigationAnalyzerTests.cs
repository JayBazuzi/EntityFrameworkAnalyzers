using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.ForeignKeyNameShouldMatchNavigationAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class ForeignKeyNameShouldMatchNavigationAnalyzerTests
{
    private const string Entities = """
        using Microsoft.EntityFrameworkCore;

        public class Customer
        {
            public int Id { get; set; }
        }

        public class Order
        {
            public int Id { get; set; }
            public int CustomerId { get; set; }
            public int BuyerId { get; set; }
            public virtual Customer Customer { get; set; } = null!;
        }

        """;

    [Fact]
    public async Task MismatchedForeignKey_IsFlagged()
    {
        const string source = Entities + """
            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    // begin-snippet: ForeignKeyNameShouldMatchNavigationAnalyzerExample
                    modelBuilder.Entity<Order>()
                        .HasOne(o => o.{|#0:Customer|}) // info: Navigation property 'Customer' should be named 'Buyer' to match foreign key 'BuyerId'
                        .WithMany()
                        .HasForeignKey(o => o.BuyerId);
                    // end-snippet
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(ForeignKeyNameShouldMatchNavigationAnalyzer.DiagnosticId).WithLocation(0).WithArguments("Customer", "Buyer", "BuyerId"));
    }

    [Fact]
    public async Task GenericHasOneAndHasForeignKey_AreChecked()
    {
        const string source = Entities + """
            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasOne<Customer>(o => o.{|#0:Customer|})
                        .WithMany()
                        .HasForeignKey(o => o.BuyerId);

                    modelBuilder.Entity<Order>()
                        .HasOne<Customer>({|#1:"Customer"|})
                        .WithMany()
                        .HasForeignKey("BuyerId");
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(ForeignKeyNameShouldMatchNavigationAnalyzer.DiagnosticId).WithLocation(0).WithArguments("Customer", "Buyer", "BuyerId"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchNavigationAnalyzer.DiagnosticId).WithLocation(1).WithArguments("Customer", "Buyer", "BuyerId"));
    }

    [Fact]
    public async Task HasOneWithoutNavigation_IsNotFlagged()
    {
        const string source = Entities + """
            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasOne<Customer>()
                        .WithMany()
                        .HasForeignKey(o => o.BuyerId);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task MatchingForeignKey_IsNotFlagged()
    {
        const string source = Entities + """
            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasOne(o => o.Customer)
                        .WithMany()
                        .HasForeignKey(o => o.CustomerId);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task HasForeignKeyWithoutHasOne_IsNotFlagged()
    {
        const string source = Entities + """
            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Customer>()
                        .HasMany<Order>()
                        .WithOne(o => o.Customer)
                        .HasForeignKey(o => o.BuyerId);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
