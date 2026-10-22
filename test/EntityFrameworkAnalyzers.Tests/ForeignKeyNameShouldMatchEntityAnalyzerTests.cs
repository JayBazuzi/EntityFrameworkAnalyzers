using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.ForeignKeyNameShouldMatchEntityAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class ForeignKeyNameShouldMatchEntityAnalyzerTests
{
    private const string Usings = """
        using System.ComponentModel.DataAnnotations.Schema;
        using Microsoft.EntityFrameworkCore;

        """;

    private static string Context(params string[] dbSets) => $$"""

        public class ShopDbContext : DbContext
        {
        {{string.Join("\n", dbSets.Select(s => $"    public DbSet<{s}> {s}s {{ get; set; }} = null!;"))}}
        }
        """;

    [Fact]
    public async Task ConventionalForeignKeyNotNamedAfterEntity_IsFlagged()
    {
        const string source = Usings + """
            public class User
            {
                public int Id { get; set; }
            }

            public class Order
            {
                public int Id { get; set; }
                public int {|#0:CreatedById|} { get; set; }
                public virtual User CreatedBy { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source + Context("Order"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(0).WithArguments("CreatedById", "UserId"));
    }

    [Fact]
    public async Task ForeignKeyEndingWithEntityNameAndId_IsNotFlagged()
    {
        const string source = Usings + """
            public class User
            {
                public int Id { get; set; }
            }

            public class Address
            {
                public int Id { get; set; }
            }

            public class Order
            {
                public int Id { get; set; }
                public int CreatedByUserId { get; set; }
                public virtual User CreatedByUser { get; set; } = null!;
                public int UserId { get; set; }
                public virtual User User { get; set; } = null!;
                public int BillingAddressId { get; set; }
                public virtual Address BillingAddress { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source + Context("Order"));
    }

    [Fact]
    public async Task SelfReference_MustEndWithEntityName()
    {
        const string source = Usings + """
            public class Category
            {
                public int Id { get; set; }
                public int {|#0:ParentId|} { get; set; }
                public virtual Category Parent { get; set; } = null!;
                public int GrandparentCategoryId { get; set; }
                public virtual Category Grandparent { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source + Context("Category"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(0).WithArguments("ParentId", "CategoryId"));
    }

    [Fact]
    public async Task ForeignKeyAttribute_IsChecked()
    {
        const string source = Usings + """
            public class User
            {
                public int Id { get; set; }
            }

            public class Order
            {
                public int Id { get; set; }
                public int {|#0:OwnerKey|} { get; set; }
                [ForeignKey("OwnerKey")]
                public virtual User Owner { get; set; } = null!;
                [ForeignKey("Reviewer")]
                public int {|#1:ReviewerRef|} { get; set; }
                public virtual User Reviewer { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source + Context("Order"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(0).WithArguments("OwnerKey", "UserId"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(1).WithArguments("ReviewerRef", "UserId"));
    }

    [Fact]
    public async Task PropertyThatIsNotAForeignKey_IsNotFlagged()
    {
        const string source = Usings + """
            public class User
            {
                public int Id { get; set; }
            }

            public class Order
            {
                public int Id { get; set; }
                public int OwnerId { get; set; }
                public virtual User Buyer { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source + Context("Order"));
    }

    [Fact]
    public async Task InheritedPrincipal_UsesTheTypeTheNavigationPointsAt()
    {
        const string source = Usings + """
            public class Person
            {
                public int Id { get; set; }
            }

            public class Employee : Person
            {
            }

            public class Order
            {
                public int Id { get; set; }
                public int EmployeeId { get; set; }
                public virtual Employee Employee { get; set; } = null!;
                [ForeignKey("Seller")]
                public int {|#0:PersonId|} { get; set; }
                public virtual Employee Seller { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source + Context("Order"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(0).WithArguments("PersonId", "EmployeeId"));
    }

    [Fact]
    public async Task FluentForeignKey_IsChecked()
    {
        const string source = Usings + """
            public class User
            {
                public int Id { get; set; }
            }

            public class Order
            {
                public int Id { get; set; }
                public int OwnerId { get; set; }
                public int ApproverId { get; set; }
                public int ShipperId { get; set; }
                public virtual User Owner { get; set; } = null!;
                public virtual User Approver { get; set; } = null!;
            }

            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasOne(o => o.Owner)
                        .WithMany()
                        .HasForeignKey(o => o.{|#0:OwnerId|});

                    modelBuilder.Entity<Order>()
                        .HasOne(o => o.Approver)
                        .WithMany()
                        .HasForeignKey({|#1:"ApproverId"|});

                    modelBuilder.Entity<User>()
                        .HasMany<Order>()
                        .WithOne()
                        .HasForeignKey(o => o.{|#2:ShipperId|});
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(0).WithArguments("OwnerId", "UserId"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(1).WithArguments("ApproverId", "UserId"),
            Verify.Diagnostic(ForeignKeyNameShouldMatchEntityAnalyzer.DiagnosticId).WithLocation(2).WithArguments("ShipperId", "UserId"));
    }

    [Fact]
    public async Task FluentCompositeAndAlternateKeys_AreSkipped()
    {
        const string source = Usings + """
            public class User
            {
                public int Id { get; set; }
                public string Code { get; set; } = null!;
            }

            public class Order
            {
                public int Id { get; set; }
                public string OwnerCode { get; set; } = null!;
                public int A { get; set; }
                public int B { get; set; }
                public virtual User Owner { get; set; } = null!;
                public virtual User Other { get; set; } = null!;
            }

            public class ShopDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<Order>()
                        .HasOne(o => o.Owner)
                        .WithMany()
                        .HasForeignKey(o => o.OwnerCode)
                        .HasPrincipalKey(u => u.Code);

                    modelBuilder.Entity<Order>()
                        .HasOne(o => o.Other)
                        .WithMany()
                        .HasForeignKey(o => new { o.A, o.B });
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
