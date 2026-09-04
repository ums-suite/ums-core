using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Organization.Infrastructure.Persistence;

/// <summary>Design-time-only factory so `dotnet ef migrations add` can construct <see cref="OrganizationDbContext"/> - mirrors <c>IdentityDbContextFactory</c> exactly.</summary>
public sealed class OrganizationDbContextFactory : IDesignTimeDbContextFactory<OrganizationDbContext>
{
    public OrganizationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<OrganizationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "organization"));

        return new OrganizationDbContext(optionsBuilder.Options);
    }
}
