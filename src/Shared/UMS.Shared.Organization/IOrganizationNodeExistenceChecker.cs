namespace UMS.Shared.Organization;

/// <summary>
/// Resolves whether a hierarchy node (`University`/`Campus`/`Faculty`/`Department`/`Program`)
/// exists, keyed by its opaque id (release/DEVELOPMENT_PLAN.md Flow #6; requirement-spec.md
/// organization §2 "A resolved 'ancestor path' query ... needed by Identity's ScopeGrant display").
///
/// <para>
/// Living in <c>UMS.Shared.Organization</c> - not <c>UMS.Modules.Organization.*</c> - is what
/// lets Identity call it without taking a forbidden dependency on Organization's Domain/
/// Application/Infrastructure internals (module-boundaries.md, ADR-0002), mirroring the exact
/// pattern <c>UMS.Shared.Audit.IAuditRecorder</c> already established for Audit's cross-module
/// write path and <c>UMS.Shared.Authorization.IPermissionManifest</c> for the catalog fan-in.
/// Organization's own Infrastructure layer (<c>UMS.Modules.Organization.Infrastructure</c>)
/// registers the one real implementation against this interface at composition-root time; every
/// other module (today, only Identity, via its own
/// <c>Application.Abstractions.IOrganizationNodeExistenceChecker</c> adapter) resolves this
/// shared interface instead.
/// </para>
///
/// <para>
/// This closes the gap release/DEVELOPMENT_PLAN.md's Flow #4/#5 notes both flagged explicitly:
/// Identity's own existence check was a permissive stub
/// (<c>StubOrganizationNodeExistenceChecker</c>, "accepted as existing until Organization lands")
/// until this module existed to call into for real.
/// </para>
/// </summary>
public interface IOrganizationNodeExistenceChecker
{
    /// <summary>
    /// True if <paramref name="organizationNodeId"/> is the id of an existing `University`,
    /// `Campus`, `Faculty`, `Department`, or `Program` row - the levels Identity's own
    /// `ScopeGrant` may bound a Role to (glossary: "ScopeGrant ... bounds a Role's effect to a
    /// Faculty/Department/Program"). Existence alone is checked here, not active status -
    /// Identity's own requirement-spec never asks "is the scoped node still active", only "does
    /// it exist at all" (a since-deactivated node is still a legitimate historical scope target).
    /// </summary>
    public Task<bool> ExistsAsync(Guid organizationNodeId, CancellationToken cancellationToken = default);
}
