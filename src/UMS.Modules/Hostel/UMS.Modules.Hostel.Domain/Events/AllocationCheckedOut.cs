using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-13: requirement-spec.md §3 - consumers: Reporting, Finance (refund-eligible flag), Audit; also the HOS-14 waitlist-re-ranking trigger (design-decisions.md "Waitlist Re-Ranking Consistency Mechanism") once this event's transaction has committed.</summary>
public sealed record AllocationCheckedOut(Guid AllocationId, Guid StudentId, Guid BedId, Guid RoomId, string CheckOutType, bool RefundEligible, DateTimeOffset OccurredAt) : IDomainEvent;
