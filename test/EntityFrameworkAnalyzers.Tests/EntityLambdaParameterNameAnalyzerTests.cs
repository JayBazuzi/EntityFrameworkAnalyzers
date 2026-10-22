using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.EntityLambdaParameterNameAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class EntityLambdaParameterNameAnalyzerTests
{
    [Fact]
    public async Task GenericName_IsFlagged()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class ClassSchedule
            {
                public int Id { get; set; }
            }

            public class SchoolDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<ClassSchedule>({|#0:entity|} => entity.HasKey(s => s.Id));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(EntityLambdaParameterNameAnalyzer.DiagnosticId).WithLocation(0)
                .WithArguments("entity", "classScheduleEntity", "ClassSchedule"));
    }

    [Fact]
    public async Task ParenthesizedLambda_IsFlagged()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class ClassSchedule
            {
                public int Id { get; set; }
            }

            public class SchoolDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<ClassSchedule>(({|#0:entity|}) =>
                    {
                        entity.HasKey(s => s.Id);
                    });
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(EntityLambdaParameterNameAnalyzer.DiagnosticId).WithLocation(0)
                .WithArguments("entity", "classScheduleEntity", "ClassSchedule"));
    }

    [Theory]
    [InlineData("classScheduleEntity")]
    [InlineData("classSchedule")]
    [InlineData("classScheduleBuilder")]
    public async Task NameStartingWithEntityName_IsNotFlagged(string parameter)
    {
        var source = $$"""
            using Microsoft.EntityFrameworkCore;

            public class ClassSchedule
            {
                public int Id { get; set; }
            }

            public class SchoolDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<ClassSchedule>({{parameter}} => {{parameter}}.HasKey(s => s.Id));
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task EntityWithoutLambda_IsNotFlagged()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class ClassSchedule
            {
                public int Id { get; set; }
            }

            public class SchoolDbContext : DbContext
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    modelBuilder.Entity<ClassSchedule>().HasKey(s => s.Id);
                }
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task UnrelatedEntityMethod_IsNotFlagged()
    {
        const string source = """
            using System;

            public class Registry
            {
                public void Entity<T>(Action<T> configure) { }

                public void Run() => Entity<string>(entity => { });
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
