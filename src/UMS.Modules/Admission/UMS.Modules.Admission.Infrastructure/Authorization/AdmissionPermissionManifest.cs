using UMS.Modules.Admission.Application.Permissions;
using UMS.Shared.Authorization;

namespace UMS.Modules.Admission.Infrastructure.Authorization;

/// <summary>Admission's own contribution to the platform-wide Permission catalog (requirement-spec.md §6). Mirrors every other module's own PermissionManifest exactly.</summary>
internal sealed class AdmissionPermissionManifest : IPermissionManifest
{
    public string OwningModule => "admission";

    public IReadOnlyCollection<PermissionDefinition> Permissions { get; } =
    [
        new(AdmissionPermissions.CampaignManage, "Create and configure an AdmissionCampaign (Admission Officer/Registrar)."),
        new(AdmissionPermissions.MeritListGenerate, "Generate a MeritList for a Campaign."),
        new(AdmissionPermissions.MeritListApprove, "Review and approve a MeritList (Registrar or delegated authority)."),
        new(AdmissionPermissions.ResultPublish, "Publish an AdmissionResult, triggering the ADR-0007 write-through cache."),
        new(AdmissionPermissions.ApplicationReview, "Review/verify Application documents and admit-card details (Admission Officer)."),
    ];
}
