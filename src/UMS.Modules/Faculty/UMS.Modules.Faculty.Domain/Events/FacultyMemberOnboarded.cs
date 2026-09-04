using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.Events;

public sealed record FacultyMemberOnboarded(Guid FacultyMemberId, Guid UserId, Guid DepartmentId, string EmployeeId, DateTimeOffset OccurredAt) : IDomainEvent;
