using UMS.Modules.Identity.Domain.ScopeGrants;

namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Resolves whether an <see cref="OrganizationNodeId"/> exists, via Organization's own read
/// interface (requirement-spec.md identity §7: "Identity calls into Organization only indirectly
/// ... resolved by Organization's own read interface when a scope needs to be ... validated for
/// existence"). Organization (release/DEVELOPMENT_PLAN.md Flow #6) does not exist yet - the
/// Infrastructure implementation registered today is an explicit stub seam, not a real check; see
/// its own remarks.
/// </summary>
public interface IOrganizationNodeExistenceChecker
{
    public Task<bool> ExistsAsync(OrganizationNodeId organizationNodeId, CancellationToken cancellationToken = default);
}
