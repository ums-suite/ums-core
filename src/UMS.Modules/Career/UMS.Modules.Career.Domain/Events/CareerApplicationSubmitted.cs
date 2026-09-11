using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: raised only once the write-time guarded insert actually succeeds. Consumer: Notifications (confirmation to Student), Reporting.</summary>
public sealed record CareerApplicationSubmitted(Guid CareerApplicationId, Guid StudentId, Guid? InternshipId, Guid? DriveId, DateTimeOffset OccurredAt) : IDomainEvent;
