namespace UMS.Modules.Content.Application.Abstractions;

/// <summary>The read/ack side of Content's own outbox (ADR-0014) - mirrors every other module's own <c>IOutboxReader</c>. Drives the urgent-notice Notifications fan-out relay worker (CNT-13).</summary>
public interface IOutboxReader
{
    public Task<IReadOnlyList<OutboxMessageDto>> GetUnprocessedAsync(string eventType, int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    public Task RecordFailedAttemptAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessageDto(Guid Id, string EventType, string PayloadJson, int AttemptCount);
