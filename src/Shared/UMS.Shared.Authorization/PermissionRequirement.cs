using Microsoft.AspNetCore.Authorization;

namespace UMS.Shared.Authorization;

/// <summary>
/// "Does the current caller hold this exact Permission string" - the only shape an authorization
/// check may take platform-wide (requirement-spec.md identity §4: "No code path may authorize on
/// a raw role-name string comparison"). There is deliberately no requirement type constructible
/// from a role name.
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
