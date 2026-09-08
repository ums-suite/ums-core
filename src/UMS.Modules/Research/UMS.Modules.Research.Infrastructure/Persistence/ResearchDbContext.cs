using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Domain.Common;
using UMS.Modules.Research.Domain.FundingBodies;
using UMS.Modules.Research.Domain.Grants;
using UMS.Modules.Research.Domain.InstitutionalRepositoryEntries;
using UMS.Modules.Research.Domain.Publications;
using UMS.Modules.Research.Infrastructure.Persistence.Inbox;
using UMS.Shared.Outbox;

namespace UMS.Modules.Research.Infrastructure.Persistence;

/// <summary>Research's own schema (ADR-0004: <c>research</c>) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class ResearchDbContext(DbContextOptions<ResearchDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<FundingBody> FundingBodies => Set<FundingBody>();

    internal DbSet<Grant> Grants => Set<Grant>();

    internal DbSet<Publication> Publications => Set<Publication>();

    internal DbSet<PublicationDuplicateCandidate> PublicationDuplicateCandidates => Set<PublicationDuplicateCandidate>();

    internal DbSet<InstitutionalRepositoryEntry> InstitutionalRepositoryEntries => Set<InstitutionalRepositoryEntry>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<ProcessedInboundEvent> ProcessedInboundEvents => Set<ProcessedInboundEvent>();

    public void Enqueue(IDomainEvent domainEvent) => _recordedEvents.Add(domainEvent);

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    /// <summary>
    /// Genuine bug found and fixed here (not present when this same pattern is used elsewhere in
    /// this codebase, since every other module's own <c>SetExpectedVersion</c> caller always ALSO
    /// changes a genuine scalar property on the same call - e.g. <c>Notice.Status</c>): setting only
    /// the concurrency token's <c>OriginalValue</c> does nothing on its own unless EF Core's own
    /// change tracker considers the entry itself "Modified" - Grant's own investigator add/remove
    /// (RES-3) mutates ONLY the owned <c>grant_investigators</c> child collection, leaving every
    /// scalar property on the <c>Grant</c> row itself untouched, so EF never emits an UPDATE for the
    /// <c>grants</c> row at all and the xmin-based check silently never fires - two concurrent
    /// <c>AddInvestigator</c> calls against the same stale version BOTH silently succeed. Forcing the
    /// whole entry to <see cref="EntityState.Modified"/> guarantees a genuine UPDATE (touching every
    /// column back to its own already-current value where nothing logically changed) whose WHERE
    /// clause still carries the real xmin equality check - a standard, safe "touch to force a
    /// concurrency check" pattern, harmless for the ordinary scalar-mutation case too.
    /// </summary>
    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class
    {
        var entry = Entry(entity);
        entry.Property("Version").OriginalValue = expectedVersion;
        entry.State = EntityState.Modified;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AppendPendingDomainEventsToOutbox();

        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityName = ex.Entries.Count > 0 ? ex.Entries[0].Entity.GetType().Name : "Entity";
            throw new ConcurrencyConflictException(entityName);
        }
        catch (DbUpdateException ex) when (TryTranslateUniqueViolation(ex, out var duplicateValueException))
        {
            throw duplicateValueException;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("research");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ResearchDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_publications_normalized_doi":
                    translated = new DuplicateValueException("Publication", "Doi", "unknown");
                    return true;
                case "ux_grant_investigators_grant_faculty_member":
                    translated = new DuplicateValueException("Grant", "InvestigatorFacultyMemberId", "unknown");
                    return true;
            }
        }

        translated = null!;
        return false;
    }

    private void AppendPendingDomainEventsToOutbox()
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<IHasDomainEvents>().ToList())
        {
            foreach (var domainEvent in entry.Entity.DomainEvents)
            {
                _recordedEvents.Add(domainEvent);
            }

            entry.Entity.ClearDomainEvents();
        }

        foreach (var domainEvent in _recordedEvents)
        {
            OutboxMessages.Add(OutboxMessage.Create(
                domainEvent.GetType().Name,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions),
                domainEvent.OccurredAt,
                now));
        }

        _recordedEvents.Clear();
    }

    private sealed class EfUmsTransaction(IDbContextTransaction transaction) : IUmsTransaction
    {
        public System.Data.Common.DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
