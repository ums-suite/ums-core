namespace UMS.Modules.Identity.Domain.ScopeGrants;

/// <summary>
/// An opaque reference to an <c>OrganizationNode</c> (University/Campus/Faculty/Department/Program)
/// owned by the `Organization` module (requirement-spec.md identity §7: "Identity stores the
/// OrganizationNode id as an opaque reference, never a joined copy of Organization's data",
/// ADR-0002). Identity never models Organization's own hierarchy - this id is only ever compared
/// for equality or handed back to a caller that resolves it against Organization's own read
/// interface once that module exists (release/DEVELOPMENT_PLAN.md Flow #6).
/// </summary>
public readonly record struct OrganizationNodeId(Guid Value)
{
    public override string ToString() => Value.ToString();
}
