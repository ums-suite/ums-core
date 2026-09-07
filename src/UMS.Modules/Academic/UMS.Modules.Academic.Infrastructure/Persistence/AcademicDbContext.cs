using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.AcademicSessions;
using UMS.Modules.Academic.Domain.Attendance;
using UMS.Modules.Academic.Domain.Common;
using UMS.Modules.Academic.Domain.CourseOfferings;
using UMS.Modules.Academic.Domain.Courses;
using UMS.Modules.Academic.Domain.Curricula;
using UMS.Modules.Academic.Domain.Enrollments;
using UMS.Modules.Academic.Domain.Programs;
using UMS.Modules.Academic.Domain.ResultPublications;
using UMS.Shared.Outbox;

namespace UMS.Modules.Academic.Infrastructure.Persistence;

/// <summary>Academic's own schema (ADR-0004: <c>academic</c>) - mirrors Faculty/Student's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class AcademicDbContext(DbContextOptions<AcademicDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<Program> Programs => Set<Program>();

    internal DbSet<Course> Courses => Set<Course>();

    internal DbSet<Curriculum> Curricula => Set<Curriculum>();

    internal DbSet<AcademicSession> AcademicSessions => Set<AcademicSession>();

    internal DbSet<CourseOffering> CourseOfferings => Set<CourseOffering>();

    internal DbSet<Enrollment> Enrollments => Set<Enrollment>();

    internal DbSet<ResultPublication> ResultPublications => Set<ResultPublication>();

    internal DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
        modelBuilder.HasDefaultSchema("academic");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AcademicDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_courses_code":
                    translated = new DuplicateValueException("Course", "code", "unknown");
                    return true;
                case "ux_academic_sessions_code":
                    translated = new DuplicateValueException("AcademicSession", "code", "unknown");
                    return true;
                case "ux_curricula_program_version":
                    translated = new DuplicateValueException("Curriculum", "program+version", "unknown");
                    return true;
                case "ux_enrollments_student_courseoffering_semester":
                    translated = new DuplicateValueException("Enrollment", "student_courseoffering_semester", "unknown");
                    return true;
                case "ux_result_publications_course_offering_id":
                    translated = new DuplicateValueException("ResultPublication", "course_offering_id", "unknown");
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
