namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>The read/ack side of Student's own outbox (ADR-0014) - mirrors Faculty's/Documents' own <c>IOutboxReader</c>. No worker consumes it yet in this Core pass (no async operation is in scope - STU-9..16/STU-15 bulk import is deliberately deferred), kept for parity with every other module's DbContext-level outbox-append-on-SaveChanges mechanism and for future consumers (Academic, Reporting) once they exist.</summary>
public interface IOutboxReader
{
    public Task<IReadOnlyList<OutboxMessageDto>> GetUnprocessedAsync(string eventType, int batchSize, CancellationToken cancellationToken = default);

    public Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default);

    public Task RecordFailedAttemptAsync(Guid id, string error, CancellationToken cancellationToken = default);
}

public sealed record OutboxMessageDto(Guid Id, string EventType, string PayloadJson, int AttemptCount);
