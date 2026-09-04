using UMS.Modules.Documents.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Documents.Infrastructure.Authorization;

/// <summary>Documents' own contribution to the platform-wide Permission catalog (mirrors Audit's own <c>AuditPermissionManifest</c>).</summary>
internal sealed class DocumentPermissionManifest : IPermissionManifest
{
    public string OwningModule => "documents";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(DocumentPermissions.Generate, "Request synchronous, single-document generation (system-to-system, calling modules only)."),
        new(DocumentPermissions.GenerateBulk, "Request an asynchronous bulk-generation job."),
        new(DocumentPermissions.Read, "Read GeneratedDocument/UploadedArtifact metadata and presigned download URLs beyond one's own."),
        new(DocumentPermissions.TemplateManage, "Publish and read DocumentTemplate versions."),
        new(DocumentPermissions.Revoke, "Revoke a Ready GeneratedDocument."),
    ];
}
