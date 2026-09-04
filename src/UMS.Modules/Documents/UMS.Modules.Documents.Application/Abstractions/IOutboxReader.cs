namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>The read/ack side of Documents' own outbox (ADR-0014), consumed by <c>UMS.Workers</c>' relays - mirrors Audit's own <c>IOutboxReader</c>.</summary>
public interface IOutboxReader
{
    public Task<IReadOnlyList<OutboxMessageDto>> GetUnprocessedAsync(string eventType, int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    public Task RecordFailedAttemptAsync(Guid id, string error, CancellationToken cancellationToken = default);
}
