using UMS.Shared.ErrorHandling.Results;

namespace UMS.Shared.Integrations;

/// <summary>
/// ADR-0018: the shared exam-integrity abstraction "mirroring <c>IPaymentGateway</c>'s ADR-0008
/// shape" - covering identity verification (photo/ID match at attempt start), browser lockdown
/// (disabling copy/paste, tab-switch detection, full-screen enforcement), and continuous flagging
/// (webcam/audio anomaly detection surfaced as a reviewable flag, never an automatic fail).
///
/// <para>
/// Living in <c>UMS.Shared.Integrations</c> - not a module-owned namespace - is what lets both
/// <c>Admission</c>'s <c>ExamAttempt</c> (this build, release/DEVELOPMENT_PLAN.md Flow #15, the
/// first real caller) and, once online semester exams exist, <c>Academic</c>'s <c>Exam</c> depend
/// on the SAME provider integration without either module depending on the other (ADR-0018's own
/// Consequences: "a shared-library dependency, not a new module-to-module edge" -
/// <c>module-boundaries.md</c>'s acyclic graph is unaffected). Each module owns its own
/// <c>IntegrityFlag</c> records against this shared interface - the provider integration is
/// shared, the domain data is not (ADR-0004).
/// </para>
///
/// <para>
/// <b>Provider selection, stated per ADR-0018:</b> a specific third-party proctoring vendor is
/// "a defensible engineering default to make at Admission's own build time, not decided here". No
/// real vendor credentials exist in this environment, so this build's only implementation is a
/// fake, in-process provider (<c>UMS.Modules.Admission.Infrastructure.Proctoring.FakeProctoringProvider</c>)
/// - the same "real abstraction, fake terminal handler" posture every other external integration in
/// this codebase already takes (Finance's <c>IPaymentGateway</c>, Notifications' channel adapters).
/// </para>
///
/// <para>
/// <b>Human-always-decides posture (ADR-0018):</b> nothing on this interface can disqualify an
/// attempt by itself - <see cref="ReportAnomalyAsync"/> only returns a flag for the CALLING
/// module's own aggregate to record and surface to a human reviewer (Admission Officer / Department
/// Head respectively); there is no "FailAttempt" or "Disqualify" operation here at all.
/// </para>
/// </summary>
public interface IProctoringProvider
{
    /// <summary>
    /// Starts a proctored session for one attempt - performs the photo/ID identity check and
    /// returns a lockdown configuration the client enforces (full-screen, copy/paste disabled, tab-
    /// switch detection). Failure here is a genuine precondition failure (identity could not be
    /// verified), not an anomaly flag - the calling module decides whether that blocks the attempt
    /// from starting at all.
    /// </summary>
    public Task<Result<ProctoringSessionHandle>> StartSessionAsync(StartProctoringSessionCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records one continuous-monitoring anomaly (a webcam/audio signal, a tab-switch event) against
    /// an already-started session. Always succeeds if the session exists - "flags evidence, it does
    /// not auto-disqualify" (ADR-0018) is enforced by this call never returning anything stronger
    /// than "flag recorded"; disqualification is never a possible outcome of this call.
    /// </summary>
    public Task<Result<ProctoringAnomalyReport>> ReportAnomalyAsync(ReportProctoringAnomalyCommand command, CancellationToken cancellationToken = default);
}

/// <param name="AttemptId">The calling module's own attempt id (Admission's <c>ExamAttemptId</c>, Academic's future <c>Exam</c> attempt id) - opaque to the provider.</param>
/// <param name="ApplicantUserId">The Identity <c>UserId</c> of the person being proctored, for photo/ID match.</param>
/// <param name="ReferencePhotoUrl">The applicant's on-file photo to match against at session start - <c>null</c> skips identity verification (a documented, non-blocking degradation, never a silent auto-pass presented as a verified match).</param>
public sealed record StartProctoringSessionCommand(Guid AttemptId, Guid ApplicantUserId, string? ReferencePhotoUrl);

/// <param name="IdentityVerified">True if the photo/ID match succeeded; <c>false</c> when a reference photo could not be checked (see <see cref="StartProctoringSessionCommand.ReferencePhotoUrl"/>) - the calling module's own edge-cases.md decides whether that blocks the attempt.</param>
public sealed record ProctoringSessionHandle(string ProviderSessionId, bool IdentityVerified, bool LockdownEnforced);

/// <param name="AnomalyType">A provider-defined category (e.g. <c>"TabSwitch"</c>, <c>"MultipleFaces"</c>, <c>"NoFaceDetected"</c>) - a caller-supplied/provider-reported string, not a shared enum, the same reason <c>SubmitNotificationRequestCommand.EventType</c> is a string.</param>
public sealed record ReportProctoringAnomalyCommand(string ProviderSessionId, string AnomalyType, string Details, DateTimeOffset OccurredAt);

/// <param name="ConfidenceScore">0.0-1.0, provider-reported - the calling module's own <c>IntegrityFlag</c> stores this for the human reviewer's context; it is never itself a pass/fail threshold this shared contract enforces.</param>
public sealed record ProctoringAnomalyReport(string AnomalyType, decimal ConfidenceScore, DateTimeOffset OccurredAt);
