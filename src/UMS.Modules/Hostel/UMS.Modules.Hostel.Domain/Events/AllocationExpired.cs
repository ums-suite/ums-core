using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>
/// HOS-10: edge-cases.md "Fee-payment grace period expiring while payment is in flight" - an addition
/// beyond requirement-spec.md's own §3 event catalog, needed as the second (alongside
/// <see cref="AllocationCheckedOut"/>) event-driven trigger design-decisions.md's "Waitlist
/// Re-Ranking Consistency Mechanism" names explicitly ("triggered ... off AllocationCheckedOut/
/// grace-period-expiry events").
/// </summary>
public sealed record AllocationExpired(Guid AllocationId, Guid StudentId, Guid BedId, Guid RoomId, DateTimeOffset OccurredAt) : IDomainEvent;
