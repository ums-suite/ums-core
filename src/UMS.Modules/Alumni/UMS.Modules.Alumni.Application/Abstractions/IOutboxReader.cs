namespace UMS.Modules.Alumni.Application.Abstractions;

/// <summary>The read/ack side of Alumni's own outbox (ADR-0014) - mirrors every other module's own <c>IOutboxReader</c>. Drives the ALM-15 Notifications fan-out relay worker.</summary>
public interface IOutboxReader
{
    public Task<IReadOnlyList<OutboxMessageDto>> GetUnprocessedAsync(string eventType, int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    public Task RecordFailedAttemptAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessageDto(Guid Id, string EventType, string PayloadJson, int AttemptCount);
