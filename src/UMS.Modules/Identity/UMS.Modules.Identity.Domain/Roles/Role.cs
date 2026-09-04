using UMS.Modules.Identity.Domain.Common;

namespace UMS.Modules.Identity.Domain.Roles;

/// <summary>
/// A named, reusable bundle of Permission strings (glossary; requirement-spec.md identity §2,
/// §3). Permission membership is a wholesale replace (`PATCH /roles/{id}/permissions`, §6) rather
/// than individual add/remove calls, matching "bundle" semantics - a Role either has a coherent
/// permission set or it doesn't, never a half-applied incremental edit.
/// </summary>
public sealed class Role : AggregateRoot<RoleId>
{
    private Role()
    {
    }

    private Role(RoleId id, string name, string? description, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Description = description;
        CreatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<string> Permissions { get; private set; } = [];

    /// <summary>IDN-16: requirement-spec.md identity §2 "MFA is mandatory for a configurable set of privileged roles" - this Role's Permissions never resolve as granted for a holder without a verified MFA enrollment (§4 "MFA cannot be silently downgraded").</summary>
    public bool RequiresMfa { get; private set; }

    public static Role Create(string name, string? description, IEnumerable<string> permissionKeys, DateTimeOffset now, bool requiresMfa = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name is required.", nameof(name));
        }

        var role = new Role(RoleId.New(), name.Trim(), description?.Trim(), now) { RequiresMfa = requiresMfa };
        role.SetPermissions(permissionKeys);
        return role;
    }

    /// <summary>Replaces the entire Permission bundle - never a partial add, so the Role's effective grant is always exactly what the last write said (requirement-spec.md identity §6).</summary>
    public void SetPermissions(IEnumerable<string> permissionKeys) =>
        Permissions = permissionKeys.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct(StringComparer.Ordinal).ToList();

    public void SetRequiresMfa(bool requiresMfa) => RequiresMfa = requiresMfa;
}
