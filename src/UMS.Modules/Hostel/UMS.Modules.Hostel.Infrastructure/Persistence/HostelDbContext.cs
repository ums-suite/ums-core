using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Domain.Allocations;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.ApplicationWindows;
using UMS.Modules.Hostel.Domain.Common;
using UMS.Modules.Hostel.Domain.Complaints;
using UMS.Modules.Hostel.Domain.Hostels;
using UMS.Modules.Hostel.Infrastructure.Persistence.Inbox;
using UMS.Shared.Outbox;

namespace UMS.Modules.Hostel.Infrastructure.Persistence;

/// <summary>Hostel's own schema (ADR-0004: <c>hostel</c>) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class HostelDbContext(DbContextOptions<HostelDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<Domain.Hostels.Hostel> Hostels => Set<Domain.Hostels.Hostel>();

    internal DbSet<Building> Buildings => Set<Building>();

    internal DbSet<Room> Rooms => Set<Room>();

    internal DbSet<Bed> Beds => Set<Bed>();

    internal DbSet<ApplicationWindow> ApplicationWindows => Set<ApplicationWindow>();

    internal DbSet<HostelApplication> HostelApplications => Set<HostelApplication>();

    internal DbSet<Allocation> Allocations => Set<Allocation>();

    internal DbSet<AllocationReviewFlag> AllocationReviewFlags => Set<AllocationReviewFlag>();

    internal DbSet<Complaint> Complaints => Set<Complaint>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<ProcessedInboundEvent> ProcessedInboundEvents => Set<ProcessedInboundEvent>();

    public void Enqueue(IDomainEvent domainEvent) => _recordedEvents.Add(domainEvent);

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class =>
        Entry(entity).Property("Version").OriginalValue = expectedVersion;

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
        modelBuilder.HasDefaultSchema("hostel");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HostelDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_allocations_bed_nonterminal":
                    translated = new DuplicateValueException("Allocation", "BedId(NonTerminal)", "unknown");
                    return true;
                case "ux_allocations_student_nonterminal":
                    translated = new DuplicateValueException("Allocation", "StudentId(NonTerminal)", "unknown");
                    return true;
                case "ux_hostel_applications_student_window_open":
                    translated = new DuplicateValueException("HostelApplication", "StudentId+ApplicationWindowId(Open)", "unknown");
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
