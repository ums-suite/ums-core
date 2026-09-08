using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Modules.Alumni.Domain.AlumniEvents;
using UMS.Modules.Alumni.Domain.Chapters;
using UMS.Modules.Alumni.Domain.Common;
using UMS.Modules.Alumni.Domain.Donations;
using UMS.Modules.Alumni.Domain.Jobs;
using UMS.Modules.Alumni.Domain.Mentorship;
using UMS.Modules.Alumni.Infrastructure.Persistence.Inbox;
using UMS.Shared.Outbox;

namespace UMS.Modules.Alumni.Infrastructure.Persistence;

/// <summary>Alumni's own schema (ADR-0004: <c>alumni</c>) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class AlumniDbContext(DbContextOptions<AlumniDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<Alumnus> Alumni => Set<Alumnus>();

    internal DbSet<AlumniChapter> Chapters => Set<AlumniChapter>();

    internal DbSet<JobPosting> JobPostings => Set<JobPosting>();

    internal DbSet<JobApplication> JobApplications => Set<JobApplication>();

    internal DbSet<DonationCampaign> DonationCampaigns => Set<DonationCampaign>();

    internal DbSet<Donation> Donations => Set<Donation>();

    internal DbSet<MentorshipOptIn> MentorshipOptIns => Set<MentorshipOptIn>();

    internal DbSet<MentorshipMatch> MentorshipMatches => Set<MentorshipMatch>();

    internal DbSet<AlumniEvent> AlumniEvents => Set<AlumniEvent>();

    internal DbSet<AlumniEventRsvp> AlumniEventRsvps => Set<AlumniEventRsvp>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    internal DbSet<ProcessedInboundEvent> ProcessedInboundEvents => Set<ProcessedInboundEvent>();

    public void Enqueue(IDomainEvent domainEvent) => _recordedEvents.Add(domainEvent);

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    /// <summary>See Research's own <c>ResearchDbContext.SetExpectedVersion</c> for why forcing the whole entry to <see cref="EntityState.Modified"/> is required (an owned-collection-only mutation otherwise never emits an UPDATE at all, silently skipping the xmin check).</summary>
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
        modelBuilder.HasDefaultSchema("alumni");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AlumniDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_alumni_student_id_ref":
                    translated = new DuplicateValueException("Alumnus", "StudentIdRef", "unknown");
                    return true;
                case "ux_chapter_memberships_chapter_alumnus":
                    translated = new DuplicateValueException("AlumniChapter", "AlumnusId", "unknown");
                    return true;
                case "ux_mentorship_opt_ins_person_role":
                    translated = new DuplicateValueException("MentorshipOptIn", "PersonId", "unknown");
                    return true;
                case "ux_alumni_event_rsvps_event_alumnus":
                    translated = new DuplicateValueException("AlumniEventRsvp", "AlumnusId", "unknown");
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
