namespace UMS.Modules.Identity.Application.Roles;

public sealed record RoleDto(Guid Id, string Name, string? Description, IReadOnlyCollection<string> Permissions, bool RequiresMfa, DateTimeOffset CreatedAt);

/// <summary>IDN-16: <paramref name="RequiresMfa"/> defaults false - most Roles are ordinary; only the platform's configurable privileged set (Super Admin, Registrar, Admission Officer, Accountant, HR Officer per requirement-spec.md identity §2) opts in.</summary>
public sealed record CreateRoleRequest(string Name, string? Description, IReadOnlyCollection<string> Permissions, bool RequiresMfa = false);

/// <summary><paramref name="RequiresMfa"/> is nullable - omitted means "leave the Role's current MFA requirement unchanged," so a caller updating only Permissions never has to know or restate it.</summary>
public sealed record UpdateRolePermissionsRequest(IReadOnlyCollection<string> Permissions, bool? RequiresMfa = null);

public sealed record AssignRoleRequest(Guid RoleId, Guid? OrganizationNodeId);

public sealed record UserRoleAssignmentDto(Guid AssignmentId, Guid RoleId, string RoleName, Guid? OrganizationNodeId, DateTimeOffset AssignedAt);
