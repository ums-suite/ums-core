using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-11: requirement-spec.md §3 - consumers: Notifications, Reporting.</summary>
public sealed record AllocationActivated(Guid AllocationId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
