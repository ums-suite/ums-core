using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Content.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` - mirrors every other module's own DbContextFactory exactly.</summary>
public sealed class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<ContentDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "content"));
        return new ContentDbContext(optionsBuilder.Options);
    }
}
