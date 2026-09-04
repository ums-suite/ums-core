using UMS.Shared.Domain;

namespace UMS.Shared.Identity;

/// <summary>
/// Resolves a recipient's contact info/language preference by their Identity <c>UserId</c>
/// (release/DEVELOPMENT_PLAN.md Flow #8, NTF-2; requirement-spec.md notifications §3 Consumed
/// "Identity's recipient contact-info/language-preference query"; §7 Cross-Module Dependencies).
///
/// <para>
/// Living in <c>UMS.Shared.Identity</c> - not <c>UMS.Modules.Identity.*</c> - is what lets
/// Notifications call it without taking a forbidden dependency on Identity's Domain/Application/
/// Infrastructure internals (module-boundaries.md, ADR-0002), mirroring the exact pattern
/// <c>UMS.Shared.Audit.IAuditRecorder</c> established for Audit's cross-module write path and
/// <c>UMS.Shared.Organization.IOrganizationNodeExistenceChecker</c> for Organization's cross-module
/// read path. Identity's own Infrastructure layer registers the one real implementation against
/// this interface at composition-root time.
/// </para>
/// </summary>
public interface IRecipientDirectory
{
    /// <summary>
    /// Returns <c>null</c> if no User exists with <paramref name="recipientId"/> - Notifications
    /// treats that the same as "no verified contact info on any channel" (edge-cases.md,
    /// "Recipient has no verified email/phone on file").
    /// </summary>
    public Task<RecipientContactInfo?> GetContactInfoAsync(Guid recipientId, CancellationToken cancellationToken = default);
}

/// <summary>
/// A snapshot of one recipient's deliverable contact points, read fresh at send time (design-
/// decisions.md, "Opt-Out-Check Timing" - the same send-time-freshness reasoning applies to contact
/// info, not only opt-out state: a bounced/updated email should stop being used as soon as Identity's
/// own profile flow updates it, not only at request-enqueue time).
/// </summary>
/// <param name="Email">Absent if the User has never set a verified email - Identity's own <c>User.Email</c> is actually always required today, but this stays nullable since a future Identity change (e.g. an unverified-email state) should not force a breaking change here.</param>
/// <param name="Mobile">Absent for a User with no phone on file (Identity's own <c>User.Mobile</c> is optional).</param>
/// <param name="PushToken">
/// Always <c>null</c> in this build - Identity has no device/push-token registry yet
/// (requirement-spec.md notifications §9 Open Questions: "the specific push-notification provider
/// ... left to ums-infra/mobile-scope decisions, outside this spec's authority"). Kept as an explicit
/// field (not omitted) so the Push channel adapter's "no contact info" gate has a real, typed signal
/// to check rather than an implicit always-fail special case.
/// </param>
/// <param name="PreferredLanguageCode">
/// Always <c>"en"</c> in this build - Identity's <c>User</c> aggregate has no profile-language field
/// yet (out of this flow's scope to add to another module's already-built, PR-open domain model).
/// A publishing module may still override the resolved language per-request via
/// <c>NotificationRequest</c>'s own <c>languageOverride</c> (see notifications
/// <c>SubmitNotificationRequestCommand</c>); English is what every request without an explicit
/// override resolves to until Identity grows a real preference field.
/// </param>
public sealed record RecipientContactInfo(Email? Email, PhoneNumber? Mobile, string? PushToken, string PreferredLanguageCode);
