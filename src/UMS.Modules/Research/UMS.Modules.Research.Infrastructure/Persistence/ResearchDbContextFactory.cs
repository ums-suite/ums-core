using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Research.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` - mirrors every other module's own DbContextFactory exactly.</summary>
public sealed class ResearchDbContextFactory : IDesignTimeDbContextFactory<ResearchDbContext>
{
    public ResearchDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<ResearchDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "research"));
        return new ResearchDbContext(optionsBuilder.Options);
    }
}
