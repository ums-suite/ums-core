using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

public sealed record GrantPrincipalInvestigatorChanged(Guid GrantId, Guid? PreviousPrincipalInvestigatorFacultyMemberId, Guid NewPrincipalInvestigatorFacultyMemberId, DateTimeOffset OccurredAt) : IDomainEvent;
