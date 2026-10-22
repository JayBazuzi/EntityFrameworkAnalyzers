using Verify = Bazuzi.EntityFrameworkQueryAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkQueryAnalyzers.NavigationPropertyReadAnalyzer>;

namespace Bazuzi.EntityFrameworkQueryAnalyzers.Tests;

public class NavigationPropertyReadAnalyzerTests
{
    private const string Preamble = """
        using System.Collections.Generic;
        using System.Linq;
        using Microsoft.EntityFrameworkCore;

        public class Customer
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
        }

        public class OrderLine
        {
            public int Id { get; set; }
            public decimal Price { get; set; }
        }

        public class Order
        {
            public int Id { get; set; }
            public virtual string Notes { get; set; } = "";
            public virtual Customer Customer { get; set; } = null!;
            public virtual ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
        }

        public class ShopDbContext : DbContext
        {
            public DbSet<Order> Orders { get; set; } = null!;
        }
        """;

    [Fact]
    public async Task ReadAfterMaterializing_IsFlagged()
    {
        const string source = Preamble + """

            public class OrderSummary
            {
                public decimal GetTotal(ShopDbContext db, int id)
                {
                    // begin-snippet: NavigationPropertyReadAnalyzerExample
                    var order = db.Orders.Single(o => o.Id == id); // forgot .Include(o => o.Lines)
                    return {|#0:order.Lines|}.Sum(l => l.Price); // info: Navigation property 'Order.Lines' is read outside a query; it is null or empty unless it was loaded
                    // end-snippet
                }
            }
            """;

        var expected = Verify.Diagnostic(NavigationPropertyReadAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Order", "Lines");

        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ReferenceNavigationRead_IsFlagged()
    {
        const string source = Preamble + """

            public class OrderSummary
            {
                public string GetCustomerName(Order order)
                {
                    return {|#0:order.Customer|}.Name;
                }
            }
            """;

        var expected = Verify.Diagnostic(NavigationPropertyReadAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Order", "Customer");

        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ReadInsideDelegateLambda_IsFlagged()
    {
        const string source = Preamble + """

            public class OrderSummary
            {
                public IEnumerable<string> GetCustomerNames(List<Order> orders)
                {
                    return orders.Select(o => {|#0:o.Customer|}.Name);
                }
            }
            """;

        var expected = Verify.Diagnostic(NavigationPropertyReadAnalyzer.DiagnosticId)
            .WithLocation(0)
            .WithArguments("Order", "Customer");

        await Verify.VerifyAnalyzerAsync(source, expected);
    }

    [Fact]
    public async Task ReadInsideQueryExpressions_IsNotFlagged()
    {
        const string source = Preamble + """

            public class OrderSummaryDto
            {
                public string CustomerName { get; set; } = "";
                public decimal Total { get; set; }
            }

            public class OrderSummary
            {
                public IQueryable<OrderSummaryDto> GetSummaries(ShopDbContext db)
                {
                    return db.Orders
                        .Include(o => o.Lines)
                        .Where(o => o.Lines.Any())
                        .OrderBy(o => o.Customer.Name)
                        .Select(o => new OrderSummaryDto
                        {
                            CustomerName = o.Customer.Name,
                            Total = o.Lines.Sum(l => l.Price),
                        });
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task Writes_AreNotFlagged()
    {
        const string source = Preamble + """

            public class OrderFactory
            {
                public Order Create(Customer customer)
                {
                    var order = new Order { Customer = customer };
                    order.Customer = customer;
                    order.Lines = new List<OrderLine>();
                    return order;
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NameOf_IsNotFlagged()
    {
        const string source = Preamble + """

            public class OrderSummary
            {
                public string Name(Order order) => nameof(order.Customer);
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task VirtualScalarProperty_IsNotFlagged()
    {
        const string source = Preamble + """

            public class OrderSummary
            {
                public string GetNotes(Order order) => order.Notes;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task NonVirtualNavigation_IsNotFlagged()
    {
        const string source = """
            public class Customer
            {
                public string Name { get; set; } = "";
            }

            public class Order
            {
                public Customer Customer { get; set; } = null!;
            }

            public class OrderSummary
            {
                public string GetCustomerName(Order order) => order.Customer.Name;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
