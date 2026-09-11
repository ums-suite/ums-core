using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: raised only after the atomic conditional-write booking actually affects a row. Consumers: Notifications (confirmation), Reporting.</summary>
public sealed record InterviewSlotBooked(Guid SlotId, Guid DriveId, Guid CareerApplicationId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
