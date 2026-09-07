using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.Applicants;
using UMS.Modules.Admission.Domain.Applications;
using UMS.Modules.Admission.Domain.Campaigns;
using UMS.Modules.Admission.Domain.Common;
using UMS.Modules.Admission.Domain.ExamAttempts;
using UMS.Modules.Admission.Domain.MeritLists;
using UMS.Modules.Admission.Domain.Publishing;
using UMS.Modules.Admission.Domain.Results;
using UMS.Modules.Admission.Domain.Tests;
using UMS.Modules.Admission.Infrastructure.Persistence.Inbox;
using UMS.Shared.Outbox;

namespace UMS.Modules.Admission.Infrastructure.Persistence;

/// <summary>Admission's own schema (ADR-0004: <c>admission</c>) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<AdmissionCampaign> Campaigns => Set<AdmissionCampaign>();

    internal DbSet<Applicant> Applicants => Set<Applicant>();

    internal DbSet<Domain.Applications.Application> Applications => Set<Domain.Applications.Application>();

    internal DbSet<AdmissionTest> AdmissionTests => Set<AdmissionTest>();

    internal DbSet<ExamAttempt> ExamAttempts => Set<ExamAttempt>();

    internal DbSet<MeritList> MeritLists => Set<MeritList>();

    internal DbSet<AdmissionResult> AdmissionResults => Set<AdmissionResult>();

    internal DbSet<PublishJob> PublishJobs => Set<PublishJob>();

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
        modelBuilder.HasDefaultSchema("admission");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdmissionDbContext).Assembly);

        // requirement-spec.md §9: a single, static, permanently-unique application-number sequence -
        // created once by this initial migration, never dynamically (contrast Student's own
        // per-(admissionYear, facultyCode) dynamic sequence) - see IApplicationRepository's own
        // remarks.
        modelBuilder.HasSequence<long>("application_number_seq", "admission").StartsAt(1).IncrementsBy(1);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_applications_applicant_campaign":
                    translated = new DuplicateValueException("Application", "applicantId+campaignId", "unknown");
                    return true;
                case "ux_applications_application_number":
                    translated = new DuplicateValueException("Application", "applicationNumber", "unknown");
                    return true;
                case "ux_exam_attempts_applicant_test":
                    translated = new DuplicateValueException("ExamAttempt", "applicantId+admissionTestId", "unknown");
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
        public DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
