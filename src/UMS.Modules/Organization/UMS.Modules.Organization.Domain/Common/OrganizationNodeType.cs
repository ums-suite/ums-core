namespace UMS.Modules.Organization.Domain.Common;

/// <summary>
/// Discriminates which hierarchy level an id/event/tree node refers to - used by
/// <see cref="Events.OrganizationNodeRenamed"/> (one event shape shared across every renamable
/// level, per requirement-spec.md organization §3) and by the tree/ancestor read models (ORG-9/
/// ORG-10) instead of five parallel, near-identical DTOs.
/// </summary>
public enum OrganizationNodeType
{
    University = 0,
    Campus = 1,
    Faculty = 2,
    Department = 3,
    Program = 4,
}
