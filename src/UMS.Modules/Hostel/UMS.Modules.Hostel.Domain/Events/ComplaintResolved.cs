using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-16: requirement-spec.md §3 - consumers: Notifications, Audit.</summary>
public sealed record ComplaintResolved(Guid ComplaintId, Guid StudentId, Guid AllocationId, string Resolution, DateTimeOffset OccurredAt) : IDomainEvent;
