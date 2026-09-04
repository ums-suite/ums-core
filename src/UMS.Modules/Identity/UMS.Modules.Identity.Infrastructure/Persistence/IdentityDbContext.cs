using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.Common;
using UMS.Modules.Identity.Domain.Permissions;
using UMS.Modules.Identity.Domain.Roles;
using UMS.Modules.Identity.Domain.Sessions;
using UMS.Modules.Identity.Domain.Users;
using UMS.Shared.Outbox;

namespace UMS.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Identity's own schema (ADR-0004: <c>identity</c>), and Identity's implementation of both
/// <see cref="IUnitOfWork"/> and <see cref="IDomainEventRecorder"/> - a single <c>SaveChanges</c>
/// call both commits the state change and drains every pending domain event (from tracked
/// aggregates and from <see cref="Enqueue"/>) into the transactional outbox, in the same
/// transaction (ADR-0003).
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<User> Users => Set<User>();

    internal DbSet<Role> Roles => Set<Role>();

    internal DbSet<Session> Sessions => Set<Session>();

    internal DbSet<PermissionCatalogEntry> Permissions => Set<PermissionCatalogEntry>();

    internal DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public void Enqueue(IDomainEvent domainEvent) => _recordedEvents.Add(domainEvent);

    public async Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        return new EfUmsTransaction(transaction);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AppendPendingDomainEventsToOutbox();

        try
        {
            return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (TryTranslateUniqueViolation(ex, out var duplicateUserException))
        {
            throw duplicateUserException;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // citext gives username/email/mobile/university-id case-insensitive equality/uniqueness
        // natively (UserConfiguration's own remarks) instead of hand-rolled lower() comparisons.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasDefaultSchema("identity");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
    }

    /// <summary>
    /// Translates a Postgres unique-violation on one of Users' identifier constraints into
    /// <see cref="DuplicateUserException"/> (edge-cases.md, "Concurrent provisioning creates a
    /// duplicate User"). Any other constraint violation is left as-is - a genuine defect, not a
    /// documented race to recover from.
    /// </summary>
    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateUserException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            var identifierType = postgresException.ConstraintName switch
            {
                "ux_users_email" => "email",
                "ux_users_username" => "username",
                "ux_users_mobile" => "mobile",
                "ux_users_university_id" => "university id",
                _ => null,
            };

            if (identifierType is not null)
            {
                translated = new DuplicateUserException(identifierType, ExtractConflictingValue(exception, identifierType) ?? "unknown");
                return true;
            }
        }

        translated = null!;
        return false;
    }

    private static string? ExtractConflictingValue(DbUpdateException exception, string identifierType)
    {
        var user = exception.Entries.OfType<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<User>>().FirstOrDefault()?.Entity;
        return identifierType switch
        {
            "email" => user?.Email.Value,
            "username" => user?.Username,
            "mobile" => user?.Mobile?.Value,
            "university id" => user?.UniversityId,
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

    /// <summary>Adapts EF Core's <see cref="IDbContextTransaction"/> to the Application layer's own, EF-agnostic <see cref="IUmsTransaction"/> port.</summary>
    private sealed class EfUmsTransaction(IDbContextTransaction transaction) : IUmsTransaction
    {
        public DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
