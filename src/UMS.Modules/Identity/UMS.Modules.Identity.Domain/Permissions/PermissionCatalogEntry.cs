using System.Text.RegularExpressions;

namespace UMS.Modules.Identity.Domain.Permissions;

/// <summary>
/// One registered entry in the platform-wide Permission catalog (requirement-spec.md identity
/// §2: "&lt;owning-module&gt;.&lt;resource&gt;.&lt;action&gt;, all lowercase, dot-separated";
/// §6 `GET /permissions`). Identity stores and resolves the catalog without needing to understand
/// any entry's business meaning (§2's manifest-registration decision) - <see cref="Description"/>
/// is opaque, owner-supplied text.
/// </summary>
public sealed partial class PermissionCatalogEntry
{
    private PermissionCatalogEntry()
    {
    }

    private PermissionCatalogEntry(string key, string owningModule, string description, DateTimeOffset registeredAt)
    {
        Key = key;
        OwningModule = owningModule;
        Description = description;
        RegisteredAt = registeredAt;
    }

    /// <summary>The permission string itself, e.g. <c>identity.role.assign</c> - the catalog's primary key.</summary>
    public string Key { get; private set; } = string.Empty;

    public string OwningModule { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; private set; }

    public static PermissionCatalogEntry Register(string key, string owningModule, string description, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(key) || !KeyPattern().IsMatch(key))
        {
            throw new ArgumentException(
                "Permission key must be lowercase, dot-separated, and follow '<module>.<resource>.<action>'.",
                nameof(key));
        }

        if (string.IsNullOrWhiteSpace(owningModule))
        {
            throw new ArgumentException("Permission must record its owning module.", nameof(owningModule));
        }

        return new PermissionCatalogEntry(key, owningModule.Trim(), description.Trim(), now);
    }

    /// <summary>Re-registering an already-known key updates its metadata in place (identity §9.2) rather than duplicating.</summary>
    public void UpdateMetadata(string owningModule, string description, DateTimeOffset registeredAt)
    {
        OwningModule = owningModule;
        Description = description;
        RegisteredAt = registeredAt;
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*){2,}$", RegexOptions.Compiled)]
    private static partial Regex KeyPattern();
}
