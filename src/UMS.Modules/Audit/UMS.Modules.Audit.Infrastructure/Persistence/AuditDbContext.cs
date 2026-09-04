using Microsoft.EntityFrameworkCore;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Domain.Entries;
using UMS.Modules.Audit.Domain.Exports;
using UMS.Shared.Outbox;

namespace UMS.Modules.Audit.Infrastructure.Persistence;

/// <summary>
/// Audit's own schema (ADR-0004: <c>audit</c>). Used two different ways:
/// <list type="bullet">
/// <item>DI-scoped, connection-string-configured, for Audit's own read path (AUD-6/7/8) and the
/// export job-tracking write path (AUD-9/10) - the normal pattern every other module's DbContext
/// follows.</item>
/// <item>Constructed ad hoc, bound to a caller's own open <see cref="System.Data.Common.DbTransaction"/>,
/// by <c>Writing.AuditRecorder</c> for the cross-module <c>RecordEntry</c> write path (AUD-1) -
/// see <c>UMS.Shared.Audit.IAuditRecorder</c>'s remarks for why this is the correct EF Core
/// cross-context-transaction pattern rather than a bespoke ambient-connection framework.</item>
/// </list>
/// </summary>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options)
    : DbContext(options), IUnitOfWork, IOutboxEnqueuer
{
    internal DbSet<AuditLogEntry> Entries => Set<AuditLogEntry>();

    internal DbSet<AuditExportRequest> ExportRequests => Set<AuditExportRequest>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void Enqueue(string eventType, string payloadJson, DateTimeOffset occurredAt) =>
        OutboxMessages.Add(OutboxMessage.Create(eventType, payloadJson, occurredAt, DateTimeOffset.UtcNow));

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("audit");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }
}
