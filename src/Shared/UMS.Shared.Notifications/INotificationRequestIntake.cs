using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Notifications;

/// <summary>
/// The ONLY way another module raises a notification (release/DEVELOPMENT_PLAN.md Flow #8, NTF-3;
/// requirement-spec.md notifications §9 Decision 1: "No public inbound HTTP endpoint for other
/// modules to submit a notification - strictly the in-process domain-event/outbox path (ADR-0003,
/// ADR-0009) ... keeps the sole-integrator boundary enforced by construction"). Every publishing
/// module calls this shared interface from its OWN outbox relay worker (ADR-0014) - never a direct
/// reference to <c>UMS.Modules.Notifications.*</c> internals (module-boundaries.md), mirroring the
/// exact pattern <see cref="UMS.Shared.Audit.IAuditRecorder"/> already established for Audit's own
/// cross-module write path.
///
/// <para>
/// <b>Known gap, documented rather than glossed over:</b> no other module in this repo raises real
/// BRD-catalog events yet (Admission/Finance/Academic/Hostel/Faculty - release/DEVELOPMENT_PLAN.md's
/// Master Sequence builds every one of them after Notifications). This contract, its dedup/opt-out/
/// fan-out mechanics, and every channel adapter are fully real and exercised end-to-end by
/// Notifications' own tests and manual verification, calling <see cref="SubmitAsync"/> directly with
/// event payloads shaped exactly like the BRD §12/§26 catalog - the same "one minimal real call
/// site now, every other module wires its own later" posture Audit's own <c>UserStatusService</c>
/// call site and Organization's cross-module checker both took for their first consumer.
/// </para>
/// </summary>
public interface INotificationRequestIntake
{
    /// <summary>
    /// Idempotent: a duplicate submission for the same natural key
    /// (<paramref name="request"/>'s <c>SourceModule</c>/<c>EventType</c>/<c>SourceEntityId</c>/
    /// <c>RecipientId</c> - requirement-spec.md §4 invariant "Deduplication", §9 Decision 2) returns
    /// success as a no-op, never an error the calling outbox relay would misinterpret as "retry me"
    /// (edge-cases.md, "Two modules ... raising the same logical NotificationRequest concurrently").
    /// </summary>
    public Task<Result<Guid>> SubmitAsync(SubmitNotificationRequestCommand request, CancellationToken cancellationToken = default);
}

/// <summary>One publishing module's fan-out request, exactly as it calls <see cref="INotificationRequestIntake.SubmitAsync"/>.</summary>
/// <param name="SourceModule">The publishing module's own name, e.g. <c>"admission"</c> (ums-conventions.md Naming's lowercase module-schema convention).</param>
/// <param name="EventType">The publishing module's own event name, e.g. <c>"ApplicationSubmitted"</c> (ums-conventions.md Naming: "&lt;Entity&gt;&lt;PastTenseVerb&gt;").</param>
/// <param name="SourceEntityId">The publishing module's own entity id this event concerns - see <see cref="Requests.NotificationRequest"/>'s own remarks (residual note) on why the publishing module owns getting this granularity right.</param>
/// <param name="RecipientId">The Identity <c>UserId</c> to notify.</param>
/// <param name="PayloadJson">Merge-field data (JSON object) the resolved <c>Template</c> is rendered against, e.g. <c>{"amount":"1200"}</c>.</param>
/// <param name="LanguageOverride">Optional - overrides the recipient's resolved language for this one request (see <c>UMS.Shared.Identity.RecipientContactInfo</c>'s remarks for why this exists).</param>
public sealed record SubmitNotificationRequestCommand(
    string SourceModule,
    string EventType,
    string SourceEntityId,
    Guid RecipientId,
    string PayloadJson,
    string? LanguageOverride = null);
