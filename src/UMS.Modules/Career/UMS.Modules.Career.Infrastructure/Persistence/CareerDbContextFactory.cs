using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Career.Infrastructure.Persistence;

/// <summary>Design-time factory for `dotnet ef` - mirrors every other module's own DbContextFactory exactly.</summary>
public sealed class CareerDbContextFactory : IDesignTimeDbContextFactory<CareerDbContext>
{
    public CareerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<CareerDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "career"));
        return new CareerDbContext(optionsBuilder.Options);
    }
}
