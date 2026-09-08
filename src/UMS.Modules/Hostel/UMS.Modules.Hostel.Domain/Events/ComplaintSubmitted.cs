using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-15: requirement-spec.md §3 - consumers: Notifications, Audit.</summary>
public sealed record ComplaintSubmitted(Guid ComplaintId, Guid StudentId, Guid AllocationId, DateTimeOffset OccurredAt) : IDomainEvent;
