namespace UMS.Modules.Audit.Application.Abstractions;

/// <summary>The read/ack side of Audit's own outbox (ADR-0014), consumed by <c>UMS.Workers</c>' export relay - the write side is <see cref="IOutboxEnqueuer"/>.</summary>
public interface IOutboxReader
{
    public Task<IReadOnlyList<OutboxMessageDto>> GetUnprocessedAsync(string eventType, int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    public Task RecordFailedAttemptAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessageDto(Guid Id, string EventType, string PayloadJson, int AttemptCount);
