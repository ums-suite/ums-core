namespace UMS.Modules.Organization.Application.Hierarchy;

/// <summary>ORG-9: one node of `GET /organization-tree`'s response - <paramref name="Status"/> is <c>null</c> for a level (Program has none below it in this tree, but every level down to Program does carry Status) that has no lifecycle at all.</summary>
public sealed record OrganizationTreeNodeDto(Guid Id, string NodeType, string Name, string? Status, IReadOnlyList<OrganizationTreeNodeDto> Children);

/// <summary>ORG-10: one entry of `GET /nodes/{id}/ancestors`'s resolved path, root-first (University ... down to the requested node itself).</summary>
public sealed record AncestorNodeDto(Guid Id, string NodeType, string Name);
