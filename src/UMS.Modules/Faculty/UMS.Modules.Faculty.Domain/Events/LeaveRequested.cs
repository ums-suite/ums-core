using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.Events;

public sealed record LeaveRequested(Guid LeaveRequestId, Guid FacultyMemberId, bool RoutedDirectlyToAuthority, DateTimeOffset OccurredAt) : IDomainEvent;
