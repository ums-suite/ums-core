using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.Events;

/// <summary>Published (via Faculty's own outbox) - consumed by Notifications (fan-out, ADR-0009) and Reporting (requirement-spec.md faculty §6).</summary>
public sealed record LeaveApproved(Guid LeaveRequestId, Guid FacultyMemberId, Guid RequesterUserId, Guid ApproverUserId, DateTimeOffset OccurredAt) : IDomainEvent;
