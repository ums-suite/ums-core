using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.BulkJobs;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Modules.Documents.Domain.UploadedArtifacts;
using UMS.Shared.Outbox;

namespace UMS.Modules.Documents.Infrastructure.Persistence;

/// <summary>Documents' own schema (ADR-0004: <c>documents</c>) - mirrors Audit's own <c>AuditDbContext</c> shape exactly.</summary>
public sealed class DocumentsDbContext(DbContextOptions<DocumentsDbContext> options)
    : DbContext(options), IUnitOfWork, IOutboxEnqueuer
{
    internal DbSet<DocumentTemplate> Templates => Set<DocumentTemplate>();

    internal DbSet<GeneratedDocument> GeneratedDocuments => Set<GeneratedDocument>();

    internal DbSet<BulkGenerationJob> BulkGenerationJobs => Set<BulkGenerationJob>();

    internal DbSet<BulkGenerationJobItem> BulkGenerationJobItems => Set<BulkGenerationJobItem>();

    internal DbSet<UploadedArtifact> UploadedArtifacts => Set<UploadedArtifact>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void Enqueue(string eventType, string payloadJson, DateTimeOffset occurredAt) =>
        OutboxMessages.Add(OutboxMessage.Create(eventType, payloadJson, occurredAt, DateTimeOffset.UtcNow));

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("documents");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentsDbContext).Assembly);
    }

    /// <summary>Adapts EF Core's <see cref="IDbContextTransaction"/> to the Application layer's own, EF-agnostic <see cref="IUmsTransaction"/> port - mirrors Identity's own <c>EfUmsTransaction</c>.</summary>
    private sealed class EfUmsTransaction(IDbContextTransaction transaction) : IUmsTransaction
    {
        public DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
