using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-7: requirement-spec.md §3 - consumers: Notifications ("hostel allocation," BRD §26), Reporting, Audit.</summary>
public sealed record BedAllocated(Guid AllocationId, Guid StudentId, Guid BedId, Guid RoomId, Guid HostelId, Guid HostelApplicationId, DateTimeOffset OccurredAt) : IDomainEvent;
