using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.ScopeGrants;

namespace UMS.Modules.Identity.Infrastructure.Organization;

/// <summary>
/// EXPLICIT SEAM, not a real check: Organization (release/DEVELOPMENT_PLAN.md Flow #6) does not
/// exist yet, so there is no read interface to call. Every <see cref="OrganizationNodeId"/> is
/// accepted as existing until Organization lands and this registration is replaced with one that
/// actually calls Organization's public query interface (requirement-spec.md identity §7).
/// Deliberately kept as its own named type (rather than an inline lambda) so it is easy to find
/// and delete when that day comes.
/// </summary>
internal sealed class StubOrganizationNodeExistenceChecker : IOrganizationNodeExistenceChecker
{
    public Task<bool> ExistsAsync(OrganizationNodeId organizationNodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
