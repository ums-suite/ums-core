using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Notifications.Infrastructure.Persistence;

/// <summary>Design-time-only factory so <c>dotnet ef migrations add</c> can construct <see cref="NotificationsDbContext"/> - mirrors Audit's own <c>AuditDbContextFactory</c> exactly.</summary>
public sealed class NotificationsDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<NotificationsDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "notifications"));

        return new NotificationsDbContext(optionsBuilder.Options);
    }
}
