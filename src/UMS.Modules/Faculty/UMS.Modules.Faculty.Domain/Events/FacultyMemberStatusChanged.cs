using UMS.Modules.Faculty.Domain.Common;
using UMS.Modules.Faculty.Domain.FacultyMembers;

namespace UMS.Modules.Faculty.Domain.Events;

public sealed record FacultyMemberStatusChanged(Guid FacultyMemberId, FacultyMemberStatus PreviousStatus, FacultyMemberStatus NewStatus, DateTimeOffset OccurredAt) : IDomainEvent;
