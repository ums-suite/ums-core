namespace UMS.Shared.Identity;

/// <summary>
/// STU-11: resolves ADR-0006's `ScopeGrant` mechanism for a calling module that needs to route or
/// gate work by `Organization`-node scope, without reimplementing Identity's own Role/ScopeGrant
/// storage. "At scope" means the User holds an active Role assignment whose Role grants
/// <c>permission</c>, and that assignment's own `ScopeGrant` either covers the whole University
/// (a <see langword="null"/> `ScopeNode` - e.g. a Registrar) or names
/// <paramref name="organizationNodeId"/> exactly (glossary: "ScopeGrant ... bounds a Role's effect
/// to a Faculty/Department/Program").
///
/// <para>
/// Living in <c>UMS.Shared.Identity</c> - not <c>UMS.Modules.Identity.*</c> - is what lets a calling
/// module (Student is the first) resolve scoped permission holders without a forbidden dependency
/// on Identity's Domain/Application/Infrastructure internals (module-boundaries.md, ADR-0002),
/// mirroring <see cref="IRecipientDirectory"/>'s/<see cref="IUserProvisioner"/>'s own exact pattern.
/// Identity's own Infrastructure layer registers the one real implementation.
/// </para>
///
/// <para>
/// <b>Deliberately exact-scope, not a hierarchy walk.</b> This directory does not itself walk up
/// the Organization tree (a Faculty-scoped ScopeGrant does not automatically "cover" that Faculty's
/// child Departments here) - a caller that needs escalation (Student's own grievance routing,
/// edge-cases.md's "escalates to the next scope level") resolves the target ancestor node itself
/// first (<see cref="UMS.Shared.Organization.IOrganizationHierarchyQuery"/>) and then asks this
/// directory about that specific node. Keeping the two concerns separate avoids Identity needing an
/// Organization dependency of its own for what is, from Identity's side, a pure permission/scope
/// lookup.
/// </para>
/// </summary>
public interface IScopeGrantDirectory
{
    /// <summary>Used to gate a reviewer's access to one already-routed <c>StudentRequest</c> (STU-12/STU-13/STU-14) - does this specific caller's ScopeGrant cover the request's own routed scope?</summary>
    public Task<bool> HasPermissionAtScopeAsync(Guid userId, string permission, Guid organizationNodeId, CancellationToken cancellationToken = default);

    /// <summary>Used at submission time (STU-11) to resolve who to notify - every User currently holding <paramref name="permission"/> at a ScopeGrant covering <paramref name="organizationNodeId"/> (university-wide or exact-node).</summary>
    public Task<IReadOnlyCollection<Guid>> GetUserIdsWithPermissionAtScopeAsync(string permission, Guid organizationNodeId, CancellationToken cancellationToken = default);
}
