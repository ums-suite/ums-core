using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: a Student releases a booked slot (or the withdrawal/cancellation cascade releases it on their behalf). Consumers: Notifications, Reporting.</summary>
public sealed record InterviewSlotCancelled(Guid SlotId, Guid DriveId, Guid CareerApplicationId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
