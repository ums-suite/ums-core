using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Faculty.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` - mirrors <c>OrganizationDbContextFactory</c> exactly.</summary>
public sealed class FacultyDbContextFactory : IDesignTimeDbContextFactory<FacultyDbContext>
{
    public FacultyDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<FacultyDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "faculty"));
        return new FacultyDbContext(optionsBuilder.Options);
    }
}
