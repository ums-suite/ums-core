using UMS.Modules.Research.Domain.Grants;

namespace UMS.Modules.Research.Application.Abstractions;

public interface IGrantRepository
{
    public Task<Grant?> GetByIdAsync(GrantId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// requirement-spec.md §9/§4 fifth bullet: a lightweight, status-only projection - used by
    /// Publication's <c>fundedByGrantIds</c> eligibility check (RES-8) so that check never loads the
    /// full Grant aggregate (investigators included) into memory, matching "cross-aggregate
    /// references by id only, never an object graph load" (§3).
    /// </summary>
    public Task<GrantStatus?> GetStatusAsync(Guid grantId, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Grant>> ListAsync(Guid? principalInvestigatorFacultyMemberId, Guid? facultyMemberId, GrantStatus? status, int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>requirement-spec.md §2 Public Research Showcase - server-side <c>isPubliclyVisible = true</c> filtering baked into the query itself (design-decisions.md "Public Showcase Visibility Filtering").</summary>
    public Task<IReadOnlyList<Grant>> ListPubliclyVisibleAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>RES-5: every <c>Funded</c>/<c>Active</c> Grant where the given FacultyMember holds the <see cref="Grants.GrantInvestigatorRole.PrincipalInvestigator"/> role - what the PI-departure relay evaluates <c>FacultyMemberStatusChanged</c> against.</summary>
    public Task<IReadOnlyList<Grant>> ListActiveWherePrincipalInvestigatorAsync(Guid facultyMemberId, CancellationToken cancellationToken = default);

    public void Add(Grant grant);
}
