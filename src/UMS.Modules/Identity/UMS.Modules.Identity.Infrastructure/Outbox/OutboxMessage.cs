namespace UMS.Modules.Identity.Infrastructure.Outbox;

/// <summary>
/// The durable, transactional-outbox record of one domain event (ADR-0003). Nothing consumes this
/// table yet - Audit (release/DEVELOPMENT_PLAN.md Flow #5) and Notifications (Flow #8) don't exist
/// as modules yet, so "in-process, synchronous to Audit" and "outbox to Notifications"
/// (requirement-spec.md identity §3) currently mean "durably recorded, in the same transaction as
/// the state change, ready for either flow's relay/handler to pick up once it exists" - not yet
/// "dispatched somewhere". This is the seam those flows hook into.
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

    public static OutboxMessage Create(string eventType, string payloadJson, DateTimeOffset occurredAt, DateTimeOffset recordedAt) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        PayloadJson = payloadJson,
        OccurredAt = occurredAt,
        RecordedAt = recordedAt,
    };
}
