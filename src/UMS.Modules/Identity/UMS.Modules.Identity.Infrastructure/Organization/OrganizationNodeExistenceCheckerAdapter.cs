using UMS.Modules.Identity.Application.Abstractions;
using UMS.Modules.Identity.Domain.ScopeGrants;

namespace UMS.Modules.Identity.Infrastructure.Organization;

/// <summary>
/// The REAL implementation, replacing the former <c>StubOrganizationNodeExistenceChecker</c> now
/// that Organization (release/DEVELOPMENT_PLAN.md Flow #6) exists. Delegates to
/// <see cref="UMS.Shared.Organization.IOrganizationNodeExistenceChecker"/> - the same in-process,
/// shared-interface pattern <c>UMS.Shared.Audit.IAuditRecorder</c> already established (ADR-0003:
/// "direct interface calls for commands") - so Identity never takes a forbidden dependency on
/// <c>UMS.Modules.Organization.*</c> internals (module-boundaries.md, ADR-0002). Only the id
/// itself crosses the boundary, unwrapped to a plain <see cref="Guid"/> - Identity's own
/// <see cref="OrganizationNodeId"/> value type stays entirely internal to Identity, matching how
/// <c>RecordAuditEntryRequest.EntityId</c> is passed as an opaque string rather than a typed id.
/// </summary>
internal sealed class OrganizationNodeExistenceCheckerAdapter(UMS.Shared.Organization.IOrganizationNodeExistenceChecker organizationChecker)
    : IOrganizationNodeExistenceChecker
{
    public Task<bool> ExistsAsync(OrganizationNodeId organizationNodeId, CancellationToken cancellationToken = default) =>
        organizationChecker.ExistsAsync(organizationNodeId.Value, cancellationToken);
}
