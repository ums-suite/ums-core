namespace UMS.Shared.Authorization;

/// <summary>
/// One Permission string this module contributes to the platform-wide catalog
/// (requirement-spec.md identity §2/§9.2: "a module-registered manifest, not a hand-maintained
/// master list ... each module publishes its own permission strings at startup"). <see cref="Key"/>
/// must follow the catalog convention: <c>&lt;owning-module&gt;.&lt;resource&gt;.&lt;action&gt;</c>,
/// all lowercase, dot-separated.
/// </summary>
public sealed record PermissionDefinition(string Key, string Description);
