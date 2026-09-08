namespace UMS.Modules.Alumni.Infrastructure.Persistence.Inbox;

/// <summary>Defense-in-depth ack-tracking for a cross-module outbox poll (Student's StudentGraduated, Finance's PaymentSucceeded/PaymentFailed) - never the sole correctness mechanism for StudentGraduated (that's the DB unique constraint on student_id_ref).</summary>
public sealed class ProcessedInboundEvent(Guid eventId, string source, DateTimeOffset processedAt)
{
    public Guid EventId { get; private set; } = eventId;

    public string Source { get; private set; } = source;

    public DateTimeOffset ProcessedAt { get; private set; } = processedAt;
}
