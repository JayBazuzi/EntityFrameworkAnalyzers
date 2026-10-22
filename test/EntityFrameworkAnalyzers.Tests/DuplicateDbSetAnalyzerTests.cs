using Verify = Bazuzi.EntityFrameworkAnalyzers.Tests.CSharpAnalyzerVerifier<Bazuzi.EntityFrameworkAnalyzers.DuplicateDbSetAnalyzer>;

namespace Bazuzi.EntityFrameworkAnalyzers.Tests;

public class DuplicateDbSetAnalyzerTests
{
    [Fact]
    public async Task DuplicateDbSet_IsFlagged()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class ClassInstructor
            {
                public int Id { get; set; }
            }

            public class SchoolDbContext : DbContext
            {
                public virtual DbSet<ClassInstructor> ClassInstructors { get; set; } = null!;

                public virtual DbSet<ClassInstructor> {|#0:ClassInstructors1|} { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(
            source,
            Verify.Diagnostic(DuplicateDbSetAnalyzer.DiagnosticId).WithLocation(0)
                .WithArguments("ClassInstructors1", "ClassInstructor", "ClassInstructors"));
    }

    [Fact]
    public async Task DistinctDbSets_AreNotFlagged()
    {
        const string source = """
            using Microsoft.EntityFrameworkCore;

            public class Student { public int Id { get; set; } }
            public class Teacher { public int Id { get; set; } }

            public class SchoolDbContext : DbContext
            {
                public DbSet<Student> Students { get; set; } = null!;
                public DbSet<Teacher> Teachers { get; set; } = null!;
            }
            """;

        await Verify.VerifyAnalyzerAsync(source);
    }
}
