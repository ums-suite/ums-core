namespace UMS.Modules.Research.Domain.Grants;

/// <summary>requirement-spec.md §3: <c>(FacultyMemberId, role: PrincipalInvestigator | CoInvestigator, addedAt)</c>. A real, meaningful distinction, not decoration - only a PI may submit the funding-body report that closes out reporting, and exactly one active PI must exist while <c>Funded</c>/<c>Active</c> (§4).</summary>
public sealed record GrantInvestigator(Guid FacultyMemberId, GrantInvestigatorRole Role, DateTimeOffset AddedAt);
