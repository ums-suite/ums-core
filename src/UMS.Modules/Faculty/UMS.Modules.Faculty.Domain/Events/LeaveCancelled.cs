using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.Events;

public sealed record LeaveCancelled(Guid LeaveRequestId, Guid FacultyMemberId, DateTimeOffset OccurredAt) : IDomainEvent;
