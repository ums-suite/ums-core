using UMS.Modules.Audit.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Audit.Infrastructure.Authorization;

/// <summary>Audit's own contribution to the platform-wide Permission catalog (mirrors Identity's own <c>IdentityPermissionManifest</c>).</summary>
internal sealed class AuditPermissionManifest : IPermissionManifest
{
    public string OwningModule => "audit";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(AuditPermissions.EntryRead, "Read audit log entries and entity history."),
        new(AuditPermissions.ExportGenerate, "Generate an async export of a filtered audit view."),
    ];
}
