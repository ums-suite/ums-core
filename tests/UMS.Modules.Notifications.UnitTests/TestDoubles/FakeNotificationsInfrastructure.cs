using System.Data.Common;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Preferences;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Modules.Notifications.Domain.Templates;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Identity;

namespace UMS.Modules.Notifications.UnitTests.TestDoubles;

/// <summary>Hand-rolled in-memory fakes for the Application-layer ports - no mocking library dependency, matching Identity's own house style (see its <c>FakeIdentityInfrastructure.cs</c>).</summary>
internal sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}

internal sealed class FakeNotificationRequestRepository : INotificationRequestRepository
{
    public List<NotificationRequest> Requests { get; } = [];

    public void Add(NotificationRequest request) => Requests.Add(request);

    public Task<NotificationRequest?> GetByIdAsync(NotificationRequestId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<NotificationRequest>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotificationRequest>>(Requests.Where(r => ids.Contains(r.Id.Value)).ToList());
}

internal sealed class FakeNotificationDeliveryAttemptRepository : INotificationDeliveryAttemptRepository
{
    public List<NotificationDeliveryAttempt> Attempts { get; } = [];

    public void AddRange(IEnumerable<NotificationDeliveryAttempt> attempts) => Attempts.AddRange(attempts);

    public Task<NotificationDeliveryAttempt?> GetByIdAsync(NotificationDeliveryAttemptId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Attempts.FirstOrDefault(a => a.Id == id));

    public Task<NotificationDeliveryAttempt?> GetByProviderMessageIdAsync(NotificationChannel channel, string providerMessageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Attempts.FirstOrDefault(a => a.Channel == channel && a.ProviderMessageId == providerMessageId));

    public Task<IReadOnlyList<ClaimedAttempt>> ClaimBatchAsync(NotificationChannel channel, IReadOnlyList<NotificationPriority> priorities, int batchSize, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("ClaimBatchAsync's real row-locking behavior is exercised by the integration test suite against a real Postgres instance, not this in-memory fake.");

    public Task<NotificationDeliveryAttempt?> GetInAppForRecipientAsync(Guid recipientId, NotificationDeliveryAttemptId id, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Covered by the integration test suite.");

    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListInAppForRecipientAsync(Guid recipientId, int skip, int take, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Covered by the integration test suite.");

    public Task<int> CountUnreadInAppForRecipientAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Covered by the integration test suite.");

    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Covered by the integration test suite.");

    public Task<IReadOnlyList<NotificationDeliveryAttempt>> ListByRequestIdAsync(NotificationRequestId requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<NotificationDeliveryAttempt>>(Attempts.Where(a => a.NotificationRequestId == requestId).ToList());
}

internal sealed class FakeTemplateRepository : ITemplateRepository
{
    public List<Template> Templates { get; } = [];

    public void Add(Template template) => Templates.Add(template);

    public Task<Template?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.FirstOrDefault(t => t.Id == id));

    public Task<Template?> GetByEventTypeAndChannelAsync(string eventType, NotificationChannel channel, CancellationToken cancellationToken = default) =>
        Task.FromResult(Templates.FirstOrDefault(t => t.EventType == eventType && t.Channel == channel && t.IsActive));

    public Task<IReadOnlyList<Template>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Template>>(Templates.Skip(skip).Take(take).ToList());
}

internal sealed class FakeRecipientPreferenceRepository : IRecipientPreferenceRepository
{
    public List<RecipientNotificationPreference> Preferences { get; } = [];

    public void Add(RecipientNotificationPreference preference) => Preferences.Add(preference);

    public Task<RecipientNotificationPreference?> GetAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult(Preferences.FirstOrDefault(p => p.RecipientId == recipientId && p.Category == category));

    public Task<bool> IsOptedOutAsync(Guid recipientId, NotificationCategory category, CancellationToken cancellationToken = default) =>
        Task.FromResult(Preferences.Any(p => p.RecipientId == recipientId && p.Category == category && p.OptedOut));
}

internal sealed class FakeChannelSuppressionRepository : IChannelSuppressionRepository
{
    public List<ChannelSuppression> Suppressions { get; } = [];

    public void Add(ChannelSuppression suppression) => Suppressions.Add(suppression);

    public Task<bool> IsSuppressedAsync(Guid recipientId, NotificationChannel channel, CancellationToken cancellationToken = default) =>
        Task.FromResult(Suppressions.Any(s => s.RecipientId == recipientId && s.Channel == channel));
}

internal sealed class FakeRecipientDirectory(RecipientContactInfo? contactInfo) : IRecipientDirectory
{
    public Task<RecipientContactInfo?> GetContactInfoAsync(Guid recipientId, CancellationToken cancellationToken = default) =>
        Task.FromResult(contactInfo);
}

internal sealed class FakeChannelProvider(NotificationChannel channel, Func<ChannelSendRequest, ChannelSendResult> respond) : IChannelProvider
{
    public List<ChannelSendRequest> SentRequests { get; } = [];

    public NotificationChannel Channel => channel;

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);
        return Task.FromResult(respond(request));
    }
}

internal sealed class FakeAuditRecorder : IAuditRecorder
{
    public List<RecordAuditEntryRequest> RecordedEntries { get; } = [];

    public Task<Result> RecordEntryAsync(RecordAuditEntryRequest request, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        RecordedEntries.Add(request);
        return Task.FromResult(Result.Success());
    }

    public Task<Result> RecordEntriesAsync(IReadOnlyCollection<RecordAuditEntryRequest> requests, DbTransaction hostTransaction, CancellationToken cancellationToken = default)
    {
        RecordedEntries.AddRange(requests);
        return Task.FromResult(Result.Success());
    }
}

internal sealed class FakeDomainEventRecorder : IDomainEventRecorder
{
    public List<IDomainEvent> Recorded { get; } = [];

    public void Enqueue(IDomainEvent domainEvent) => Recorded.Add(domainEvent);
}

internal sealed class FakeOtpRateLimiter(bool allow = true) : IOtpRateLimiter
{
    public Task<bool> TryAcquireAsync(Guid recipientId, CancellationToken cancellationToken = default) => Task.FromResult(allow);
}

/// <summary>Never throws on <see cref="DuplicateNotificationRequestException"/> unless a test explicitly queues one - mirrors Identity's own <c>FakeUnitOfWork</c>.</summary>
internal sealed class FakeUnitOfWork : IUnitOfWork
{
    private readonly Queue<Exception> _queuedFailures = new();

    public int SaveChangesCallCount { get; private set; }

    public void QueueFailure(Exception exception) => _queuedFailures.Enqueue(exception);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        if (_queuedFailures.Count > 0)
        {
            throw _queuedFailures.Dequeue();
        }

        return Task.FromResult(1);
    }

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IUmsTransaction>(new FakeUmsTransaction());
}

/// <summary>
/// A no-op stand-in for a real ADO.NET transaction. <see cref="DbTransaction"/> returns <c>null!</c>
/// rather than throwing - unlike Identity's own equivalent fake, <see cref="FakeAuditRecorder"/>
/// never dereferences the transaction it is handed, so a null placeholder is sufficient here and
/// lets NTF-17's audit-integration branch be unit-tested without a real database.
/// </summary>
internal sealed class FakeUmsTransaction : IUmsTransaction
{
    public DbTransaction DbTransaction => null!;

    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
