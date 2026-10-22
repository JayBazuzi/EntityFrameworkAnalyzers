using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.NavigationPropertyShouldBeVirtualAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class NavigationPropertyShouldBeVirtualAnalyzerTests
{
    [Fact]
    public async Task NonVirtualNavigations_AreFlagged()
    {
        const string source = """
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            public class Customer
            {
                public int Id { get; set; }
            }

            public class OrderLine
            {
                public int Id { get; set; }
            }

            // begin-snippet: NavigationPropertyShouldBeVirtualAnalyzerExample
            public class Order
            {
                public int Id { get; set; }
                public Customer {|#0:Customer|} { get; set; } = null!; // info: Navigation property 'Order.Customer' should be virtual
                public ICollection<OrderLine> {|#1:Lines|} { get; set; } = new List<OrderLine>(); // info: Navigation property 'Order.Lines' should be virtual
            }
            // end-snippet

            public class ShopDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(NavigationPropertyShouldBeVirtualAnalyzer.DiagnosticId).WithLocation(0).WithArguments("Order", "Customer"),
            Verify.Diagnostic(NavigationPropertyShouldBeVirtualAnalyzer.DiagnosticId).WithLocation(1).WithArguments("Order", "Lines"));
    }

    [Fact]
    public async Task EntitiesReachableThroughNavigations_AreChecked()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class Address
            {
                public int Id { get; set; }
            }

            public class Customer
            {
                public int Id { get; set; }
                public Address {|#0:Address|} { get; set; } = null!;
            }

            public class Order
            {
                public int Id { get; set; }
                public virtual Customer Customer { get; set; } = null!;
            }

            public class ShopDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(NavigationPropertyShouldBeVirtualAnalyzer.DiagnosticId).WithLocation(0).WithArguments("Customer", "Address"));
    }

    [Fact]
    public async Task VirtualNavigationsAndScalars_AreNotFlagged()
    {
        const string source = """
            using System;
            using System.Collections.Generic;
            using Microsoft.EntityFrameworkCore;

            public class Customer
            {
                public int Id { get; set; }
            }

            public class Order
            {
                public int Id { get; set; }
                public string Notes { get; set; } = "";
                public byte[] Signature { get; set; } = [];
                public DateTime PlacedAt { get; set; }
                public List<string> Tags { get; set; } = new();
                public virtual Customer Customer { get; set; } = null!;
            }

            public class ShopDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task SealedEntity_IsNotFlagged()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class Customer
            {
                public int Id { get; set; }
            }

            public sealed class Order
            {
                public int Id { get; set; }
                public Customer Customer { get; set; } = null!;
            }

            public class ShopDbContext : DbContext
            {
                public DbSet<Order> Orders { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task ClassesNotReachableFromADbContext_AreNotFlagged()
    {
        const string source = """
            public class Customer
            {
                public int Id { get; set; }
            }

            public class OrderViewModel
            {
                public Customer Customer { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
