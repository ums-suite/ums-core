using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory so <c>dotnet ef migrations add</c> can construct
/// <see cref="IdentityDbContext"/> without running the full Host composition root. Never used at
/// runtime - <see cref="DependencyInjection.AddIdentityModule"/> registers the real,
/// configuration-driven options instead. The connection string here only needs to be valid enough
/// for EF's migration-generation tooling to introspect the provider; it matches
/// <c>src/Host/appsettings.Development.json</c>'s local dev default, overridable via the
/// <c>ConnectionStrings__Postgres</c> environment variable for a non-default local Postgres.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"));

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
