using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Common;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Employers;
using UMS.Modules.Career.Domain.Internships;
using UMS.Modules.Career.Domain.ResumeProfiles;
using UMS.Modules.Career.Infrastructure.Persistence.Inbox;
using UMS.Shared.Outbox;

namespace UMS.Modules.Career.Infrastructure.Persistence;

/// <summary>Career's own schema (ADR-0004: `career`) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class CareerDbContext(DbContextOptions<CareerDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<EmployerProfile> EmployerProfiles => Set<EmployerProfile>();

    internal DbSet<Internship> Internships => Set<Internship>();

    internal DbSet<CampusRecruitmentDrive> Drives => Set<CampusRecruitmentDrive>();

    internal DbSet<InterviewSlot> InterviewSlots => Set<InterviewSlot>();

    internal DbSet<CareerApplication> CareerApplications => Set<CareerApplication>();

    internal DbSet<ResumeProfile> ResumeProfiles => Set<ResumeProfile>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<ProcessedInboundEvent> ProcessedInboundEvents => Set<ProcessedInboundEvent>();

    public void Enqueue(IDomainEvent domainEvent) => _recordedEvents.Add(domainEvent);

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    /// <summary>See Research's/Alumni's own `SetExpectedVersion` remarks: forcing the whole entry to `Modified` is required so an owned-collection-only mutation still emits an UPDATE, never silently skipping the xmin check.</summary>
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
        modelBuilder.HasDefaultSchema("career");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CareerDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_resume_profiles_student_default":
                    translated = new DuplicateValueException("ResumeProfile", "IsDefault", "true");
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
