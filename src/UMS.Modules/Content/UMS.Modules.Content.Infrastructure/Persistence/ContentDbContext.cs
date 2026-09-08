using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Banners;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Downloads;
using UMS.Modules.Content.Domain.Events;
using UMS.Modules.Content.Domain.HomepageSections;
using UMS.Modules.Content.Domain.Notices;
using UMS.Shared.Outbox;

namespace UMS.Modules.Content.Infrastructure.Persistence;

/// <summary>Content's own schema (ADR-0004: <c>content</c>) - mirrors every other module's own DbContext shape exactly, including the outbox-drain-on-SaveChanges mechanism and the Postgres-exception translation pattern.</summary>
public sealed class ContentDbContext(DbContextOptions<ContentDbContext> options)
    : DbContext(options), IUnitOfWork, IDomainEventRecorder
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private readonly List<IDomainEvent> _recordedEvents = [];

    internal DbSet<Notice> Notices => Set<Notice>();

    internal DbSet<Event> Events => Set<Event>();

    internal DbSet<Banner> Banners => Set<Banner>();

    internal DbSet<HomepageSection> HomepageSections => Set<HomepageSection>();

    internal DbSet<DownloadResource> DownloadResources => Set<DownloadResource>();

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
        modelBuilder.HasDefaultSchema("content");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ContentDbContext).Assembly);
    }

    private static bool TryTranslateUniqueViolation(DbUpdateException exception, out DuplicateValueException translated)
    {
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            switch (postgresException.ConstraintName)
            {
                case "ux_notice_translations_notice_language":
                    translated = new DuplicateValueException("Notice", "LanguageCode", "unknown");
                    return true;
                case "ux_event_translations_event_language":
                    translated = new DuplicateValueException("Event", "LanguageCode", "unknown");
                    return true;
                case "ux_homepage_sections_section_key":
                    translated = new DuplicateValueException("HomepageSection", "SectionKey", "unknown");
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
