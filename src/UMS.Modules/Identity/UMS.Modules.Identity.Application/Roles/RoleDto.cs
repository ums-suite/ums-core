namespace UMS.Modules.Identity.Application.Roles;

public sealed record RoleDto(Guid Id, string Name, string? Description, IReadOnlyCollection<string> Permissions, DateTimeOffset CreatedAt);

public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyCollection<string> Permissions);

public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> Permissions);

public sealed record AssignRoleRequest(Guid RoleId, Guid? OrganizationNodeId);

public sealed record UserRoleAssignmentDto(Guid AssignmentId, Guid RoleId, string RoleName, Guid? OrganizationNodeId, DateTimeOffset AssignedAt);
