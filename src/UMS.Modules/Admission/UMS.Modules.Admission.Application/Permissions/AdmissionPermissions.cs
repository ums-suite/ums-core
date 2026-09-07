namespace UMS.Modules.Admission.Application.Permissions;

/// <summary>
/// requirement-spec.md §6's API-surface table describes these as "flat capability strings per
/// ADR-0006 (not module-prefixed)" - ADR-0006's own examples (<c>result.publish</c>,
/// <c>payment.refund</c>) are indeed 2-segment, but Identity's already-shipped catalog validator
/// (<c>PermissionCatalogEntry.Register</c>) requires at least 3 dot-separated segments
/// (<c>&lt;module&gt;.&lt;resource&gt;.&lt;action&gt;</c>) regardless. This is the identical
/// documented deviation Documents' own permission strings already went through at Flow #9
/// ("adapted from the spec's literal 2-segment form to Identity's already-built 3-segment
/// validator") - every permission here is prefixed with <c>admission.</c> for the same reason,
/// not a spec change.
/// </summary>
public static class AdmissionPermissions
{
    public const string CampaignManage = "admission.campaign.manage";

    public const string MeritListGenerate = "admission.meritlist.generate";

    public const string MeritListApprove = "admission.meritlist.approve";

    public const string ResultPublish = "admission.result.publish";

    /// <summary>Admission Officer's document-review/admit-card-review surface (requirement-spec.md §6's <c>application.review</c> alternative to Applicant-owned).</summary>
    public const string ApplicationReview = "admission.application.review";
}
