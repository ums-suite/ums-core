using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.Events;

public sealed record LeaveApprovedByDepartmentHead(Guid LeaveRequestId, Guid FacultyMemberId, Guid ApproverUserId, DateTimeOffset OccurredAt) : IDomainEvent;
