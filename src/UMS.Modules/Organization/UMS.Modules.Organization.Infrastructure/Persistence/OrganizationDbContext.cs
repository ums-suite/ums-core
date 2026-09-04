using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Organization.Application.Abstractions;
using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Departments;
using UMS.Modules.Organization.Domain.Designations;
using UMS.Modules.Organization.Domain.Facilities;
using UMS.Modules.Organization.Domain.Faculties;
using UMS.Modules.Organization.Domain.Universities;
using UMS.Shared.Outbox;
using OrgProgram = UMS.Modules.Organization.Domain.Programs.Program;

namespace UMS.Modules.Organization.Infrastructure.Persistence;

/// <summary>
/// Organization's own schema (ADR-0004: <c>organization</c>), and Organization's implementation
/// of both <see cref="IUnitOfWork"/> and <see cref="IDomainEventRecorder"/> - mirrors
/// <c>UMS.Modules.Identity.Infrastructure.Persistence.IdentityDbContext</c>'s shape exactly,
/// including the same "drain pending domain events into the outbox inside SaveChanges" mechanism
/// and the same Postgres-exception-to-domain-exception translation pattern (here: a `(parent_id,
/// name)` unique-violation becomes <see cref="DuplicateNameException"/>, and an EF Core
/// concurrency conflict becomes <see cref="ConcurrencyConflictException"/> - see
/// <see cref="IUnitOfWork.SetExpectedVersion{TEntity}"/>'s own remarks for how the check is
/// primed).
/// </summary>
public sealed class OrganizationDbContext(DbContextOptions<OrganizationDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<University> Universities => Set<University>();

    internal DbSet<Campus> Campuses => Set<Campus>();

    internal DbSet<Faculty> Faculties => Set<Faculty>();

    internal DbSet<Department> Departments => Set<Department>();

    internal DbSet<OrgProgram> Programs => Set<OrgProgram>();

    internal DbSet<Designation> Designations => Set<Designation>();

    internal DbSet<Building> Buildings => Set<Building>();

    internal DbSet<Room> Rooms => Set<Room>();

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
        catch (DbUpdateException ex) when (TryTranslateUniqueViolation(ex, out var duplicateNameException))
        {
            throw duplicateNameException;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("organization");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrganizationDbContext).Assembly);
    }

    /// <summary>
    /// edge-cases.md "Two Departments with the identical name created simultaneously under the
    /// same Faculty": translates a Postgres unique-violation on one of the `(parent_id, name)`
    /// constraints into <see cref="DuplicateNameException"/>, mirroring
    /// <c>IdentityDbContext</c>'s own <c>TryTranslateUniqueViolation</c> exactly. Any other
    /// constraint violation is left as-is - a genuine defect, not a documented race to recover
    /// from.
    /// </summary>
    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateNameException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            var entityType = postgresException.ConstraintName switch
            {
                "ux_campuses_university_id_name" => "Campus",
                "ux_faculties_campus_id_name" => "Faculty",
                "ux_departments_faculty_id_name" => "Department",
                "ux_programs_department_id_name" => "Program",
                _ => null,
            };

            if (entityType is not null)
            {
                translated = new DuplicateNameException(entityType, ExtractConflictingName(exception) ?? "unknown");
                return true;
            }
        }

        translated = null!;
        return false;
    }

    private static string? ExtractConflictingName(DbUpdateException exception)
    {
        var entry = exception.Entries.Count > 0 ? exception.Entries[0] : null;
        return entry?.Entity switch
        {
            Campus c => c.Name,
            Faculty f => f.Name,
            Department d => d.Name,
            OrgProgram p => p.Name,
            _ => null,
        };
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

    /// <summary>Adapts EF Core's <c>IDbContextTransaction</c> to the Application layer's own, EF-agnostic <see cref="IUmsTransaction"/> port - mirrors <c>IdentityDbContext.EfUmsTransaction</c> exactly.</summary>
    private sealed class EfUmsTransaction(IDbContextTransaction transaction) : IUmsTransaction
    {
        public DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
