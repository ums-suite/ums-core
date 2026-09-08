using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Reporting.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` - mirrors every other module's own DbContextFactory exactly.</summary>
public sealed class ReportingDbContextFactory : IDesignTimeDbContextFactory<ReportingDbContext>
{
    public ReportingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<ReportingDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "reporting"));
        return new ReportingDbContext(optionsBuilder.Options);
    }
}
