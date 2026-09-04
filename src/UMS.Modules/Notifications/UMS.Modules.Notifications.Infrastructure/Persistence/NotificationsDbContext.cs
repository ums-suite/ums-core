using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.Domain.Templates;
using UMS.Shared.Outbox;

namespace UMS.Modules.Notifications.Infrastructure.Persistence;

/// <summary>
/// Notifications' own schema (ADR-0004: <c>notifications</c>). Mirrors Identity's own
/// <c>IdentityDbContext</c> exactly: one <c>SaveChanges</c> call both commits the state change and
/// drains every pending domain event into the transactional outbox (ADR-0003), and translates the
/// dedupe unique-constraint violation (design-decisions.md "Dedup-Key Enforcement Mechanism") into
/// <see cref="DuplicateNotificationRequestException"/> the same way Identity translates its own
/// unique violations into <c>DuplicateUserException</c>.
/// </summary>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<NotificationRequest> NotificationRequests => Set<NotificationRequest>();

    internal DbSet<NotificationDeliveryAttempt> DeliveryAttempts => Set<NotificationDeliveryAttempt>();

    internal DbSet<Template> Templates => Set<Template>();

    internal DbSet<RecipientNotificationPreference> RecipientPreferences => Set<RecipientNotificationPreference>();

    internal DbSet<ChannelSuppression> ChannelSuppressions => Set<ChannelSuppression>();

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
        catch (DbUpdateException ex) when (IsDedupeViolation(ex))
        {
            throw new DuplicateNotificationRequestException();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("notifications");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }

    private static bool IsDedupeViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_notification_requests_dedupe" };

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

    /// <summary>Adapts EF Core's <see cref="IDbContextTransaction"/> to the Application layer's own, EF-agnostic <see cref="IUmsTransaction"/> port - mirrors Identity's own <c>EfUmsTransaction</c>.</summary>
    private sealed class EfUmsTransaction(IDbContextTransaction transaction) : IUmsTransaction
    {
        public DbTransaction DbTransaction => transaction.GetDbTransaction();

        public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
