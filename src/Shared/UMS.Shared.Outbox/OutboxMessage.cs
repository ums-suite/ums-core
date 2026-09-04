namespace UMS.Shared.Outbox;

/// <summary>
/// The durable, transactional-outbox record of one domain event or async job trigger (ADR-0003,
/// ADR-0014: "one shared Outbox pattern ... feeding one shared background worker infrastructure").
/// Promoted here from what was originally <c>UMS.Modules.Identity.Infrastructure.Outbox</c>
/// (Flow #4) the moment a second module (Audit, Flow #5 - AUD-9's async export) needed the exact
/// same shape - per <c>module-boundaries.md</c>'s "shared vs. per-module infrastructure" guidance,
/// a pattern used identically by two or more modules belongs in a shared library, not duplicated
/// per module. Each module still owns its OWN outbox <em>table</em>, in its own schema
/// (ADR-0004) - this class only shares the row shape/serialization convention, never the data
/// itself; there is no cross-module `outbox` table or schema.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    /// <summary>
    /// Set once a worker has successfully processed this message (ADR-0014: "idempotent by
    /// construction, safe to retry" - a relay re-scans unprocessed rows, so this must be
    /// persisted, not just an in-memory dedupe). <c>null</c> means still pending.
    /// </summary>
    public DateTimeOffset? ProcessedAt { get; private set; }

    /// <summary>Bounded retry count for dead-letter handling (ADR-0014).</summary>
    public int AttemptCount { get; private set; }

    public string? LastError { get; private set; }

    public static OutboxMessage Create(string eventType, string payloadJson, DateTimeOffset occurredAt, DateTimeOffset recordedAt) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        PayloadJson = payloadJson,
        OccurredAt = occurredAt,
        RecordedAt = recordedAt,
    };

    public void MarkProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
    }

    public void RecordFailedAttempt(string error)
    {
        AttemptCount++;
        LastError = error;
    }
}
