using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: `Draft -> Scheduled`. Consumers: Notifications, Reporting.</summary>
public sealed record CampusRecruitmentDriveScheduled(Guid DriveId, DateTimeOffset OccurredAt) : IDomainEvent;
