using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.Common;
using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Outbox;

namespace UMS.Modules.Student.Infrastructure.Persistence;

/// <summary>Student's own schema (ADR-0004: <c>student</c>) - mirrors <c>FacultyDbContext</c>'s shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class StudentDbContext(DbContextOptions<StudentDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<Domain.Students.Student> Students => Set<Domain.Students.Student>();

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
            var entityType = ex.Entries.Count > 0 ? ex.Entries[0].Entity.GetType().Name : "Entity";
            throw new ConcurrencyConflictException(entityType);
        }
        catch (DbUpdateException ex) when (TryTranslateUniqueViolation(ex, out var duplicateValueException))
        {
            throw duplicateValueException;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("student");

        // Mirrors Identity's own UserConfiguration: case-insensitive email/mobile columns.
        // Declared here too (not only relied upon via Identity's own migration) so Student's
        // schema is self-contained regardless of module registration order.
        modelBuilder.HasPostgresExtension("citext");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StudentDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            var entry = exception.Entries.Count > 0 ? exception.Entries[0].Entity as Domain.Students.Student : null;

            switch (postgresException.ConstraintName)
            {
                case "ux_students_originating_application_id":
                    translated = new DuplicateValueException("Student", "originatingApplicationId", entry?.OriginatingApplicationId.ToString() ?? "unknown");
                    return true;
                case "ux_students_student_number":
                    translated = new DuplicateValueException("Student", "studentNumber", entry?.StudentNumber.Value ?? "unknown");
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
                domainEvent.GetType().FullName ?? domainEvent.GetType().Name,
                JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _serializerOptions),
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
