namespace UMS.Modules.Career.Infrastructure.Persistence.Inbox;

/// <summary>Defense-in-depth ack-tracking for a cross-module outbox poll (Student's StudentGraduated/StudentStatusChanged) - never the sole correctness mechanism for anything, since CAR-16's consumer performs no mutation at all (design-decisions.md's graduation-boundary posture).</summary>
public sealed class ProcessedInboundEvent(Guid eventId, string source, DateTimeOffset processedAt)
{
    public Guid EventId { get; private set; } = eventId;

    public string Source { get; private set; } = source;

    public DateTimeOffset ProcessedAt { get; private set; } = processedAt;
}
