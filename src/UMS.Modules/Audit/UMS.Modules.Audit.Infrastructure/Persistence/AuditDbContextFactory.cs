using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Audit.Infrastructure.Persistence;

/// <summary>Design-time-only factory so <c>dotnet ef migrations add</c> can construct <see cref="AuditDbContext"/> - mirrors Identity's own <c>IdentityDbContextFactory</c> exactly.</summary>
public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<AuditDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "audit"));

        return new AuditDbContext(optionsBuilder.Options);
    }
}
