using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.PreferGenericHasOneAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class PreferGenericHasOneAnalyzerTests
{
    private static string Source(string entities, string configuration) => $$"""
        using Microsoft.EntityFrameworkCore;

        public class Customer
        {
            public int Id { get; set; }
        }

        public class Order
        {
            public int Id { get; set; }
            public virtual Customer? Customer { get; set; }
        {{entities}}
        }

        public class ShopDbContext : DbContext
        {
            public DbSet<Order> Orders { get; set; } = null!;

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                {{configuration}}
            }
        }
        """;

    [Fact]
    public async Task StringNavigationMatchingProperty_IsFlagged()
    {
        var source = Source("", """modelBuilder.Entity(typeof(Order)).{|#0:HasOne|}("Customer").WithMany();""");

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(PreferGenericHasOneAnalyzer.DiagnosticId).WithLocation(0));
    }

    [Fact]
    public async Task TypeofOverload_IsFlagged()
    {
        var source = Source("", """modelBuilder.Entity(typeof(Order)).{|#0:HasOne|}(typeof(Customer), "Customer").WithMany();""");

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(PreferGenericHasOneAnalyzer.DiagnosticId).WithLocation(0));
    }

    [Fact]
    public async Task TypeNameStringOverload_IsFlagged()
    {
        var source = Source("", """modelBuilder.Entity(typeof(Order)).{|#0:HasOne|}("Customer", "Customer").WithMany();""");

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(PreferGenericHasOneAnalyzer.DiagnosticId).WithLocation(0));
    }

    [Fact]
    public async Task LambdaAndGenericOverloads_AreNotFlagged()
    {
        var source = Source("", """
            modelBuilder.Entity<Order>().HasOne(o => o.Customer).WithMany();
            modelBuilder.Entity<Order>().HasOne<Customer>("Customer").WithMany();
            """);

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task PrivateNavigation_IsNotFlagged()
    {
        var source = Source(
            "    private Customer? Secret { get; set; }",
            """modelBuilder.Entity(typeof(Order)).HasOne("Secret").WithMany();""");

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ShadowNavigation_IsNotFlagged()
    {
        var source = Source("", """modelBuilder.Entity(typeof(Order)).HasOne("Shadow").WithMany();""");

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SharedTypeEntityName_IsNotFlagged()
    {
        var source = Source("", """modelBuilder.Entity(typeof(Order)).HasOne("Tag", "Tag").WithMany();""");

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NonConstantArguments_AreNotFlagged()
    {
        var source = Source("", """
            var name = "Customer";
            var type = typeof(Customer);
            modelBuilder.Entity(typeof(Order)).HasOne(name).WithMany();
            modelBuilder.Entity(typeof(Order)).HasOne(type, name).WithMany();
            """);

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UnknownEntityTypeWithSingleString_IsNotFlagged()
    {
        var source = Source("", """modelBuilder.Entity("Order").HasOne("Customer").WithMany();""");

        await Verify.VerifyAnalyzerAsync(source);
    }
}
