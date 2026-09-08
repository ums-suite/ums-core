using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

/// <summary>
/// edge-cases.md "Principal Investigator Leaves the University Mid-Grant"; design-decisions.md
/// "Grant Lifecycle State Machine and PI-Vacancy Handling" - consumed by Notifications, raising a
/// <c>NotificationRequest</c> alert to Admin/Research-Office (requirement-spec.md §7).
/// </summary>
public sealed record GrantPiReassignmentRequired(Guid GrantId, Guid VacatedFacultyMemberId, DateTimeOffset OccurredAt) : IDomainEvent;
