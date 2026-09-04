using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UMS.Modules.Documents.Infrastructure.Persistence;

/// <summary>Design-time-only factory so <c>dotnet ef migrations add</c> can construct <see cref="DocumentsDbContext"/> - mirrors Audit's own <c>AuditDbContextFactory</c> exactly.</summary>
public sealed class DocumentsDbContextFactory : IDesignTimeDbContextFactory<DocumentsDbContext>
{
    public DocumentsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5433;Database=ums;Username=ums;Password=ums_dev_password";

        var optionsBuilder = new DbContextOptionsBuilder<DocumentsDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "documents"));

        return new DocumentsDbContext(optionsBuilder.Options);
    }
}
