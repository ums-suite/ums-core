using UMS.Modules.Notifications.Domain.Common;

namespace UMS.Modules.Notifications.Domain.Requests;

/// <summary>
/// requirement-spec.md §3: "one channel-specific attempt, with its own retry state". design-
/// decisions.md, "Fan-Out Isolation": one independent row per channel per <see cref="NotificationRequest"/>,
/// its own retry count/backoff/dead-letter threshold, no shared transaction across channels
/// (ADR-0009's core boundary rationale - a down SMS provider never blocks the Email attempt for the
/// same request).
///
/// <para>
/// <b>State machine</b> (design-decisions.md, "Retry/Backoff Design Per Channel"; edge-cases.md, "A
/// retry racing a not-yet-failed original delivery attempt"):
/// <c>Pending</c> -&gt; <c>InFlight</c> (dispatch claimed) -&gt; <c>Delivered</c> (success, terminal)
/// or <c>Retrying</c> (transient failure, bounded exponential backoff) -&gt; back to <c>InFlight</c>
/// on the next claim, until either <c>Delivered</c> or <c>DeadLettered</c> (retries exhausted).
/// <c>NoContactInfo</c>/<c>TemplateMissing</c> skip straight to <c>DeadLettered</c> with the matching
/// <see cref="DeadLetterReason"/>, with zero retries spent - both are configuration/data problems,
/// not transient infrastructure failures (§8; design-decisions.md "Template Resolution Fallback").
/// An opted-out category sends the attempt straight to <c>Suppressed</c>, a successful no-op, never
/// <c>DeadLettered</c>.
/// </para>
///
/// <para>
/// <b>Idempotent by construction</b> (§4 invariant): every mutator below no-ops if the attempt is
/// already <see cref="DeliveryAttemptStatus.Delivered"/> - "a redelivered attempt for an already-
/// Delivered channel is a no-op, never a duplicate message to the recipient".
/// </para>
/// </summary>
public sealed class NotificationDeliveryAttempt
{
    /// <summary>Bounded exponential retry budget (ADR-0014) - the 5th consecutive transient failure dead-letters rather than retrying again.</summary>
    public const int MaxAttempts = 5;

    /// <summary>
    /// design-decisions.md: the retry scheduler only considers an attempt eligible for retry once
    /// it has left <c>InFlight</c> via "a documented per-provider timeout" - fixed here at 2 minutes,
    /// comfortably above every fake-provider adapter's own worst-case latency+Polly-retry budget
    /// (see channel adapters' own resilience configuration), long enough that a merely-slow original
    /// call is never mistaken for a dead one within this window.
    /// </summary>
    public static readonly TimeSpan InFlightTimeout = TimeSpan.FromMinutes(2);

    private NotificationDeliveryAttempt()
    {
    }

    public NotificationDeliveryAttemptId Id { get; private set; }

    public NotificationRequestId NotificationRequestId { get; private set; }

    public NotificationChannel Channel { get; private set; }

    public DeliveryAttemptStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset? NextAttemptAt { get; private set; }

    public DateTimeOffset? InFlightSince { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    /// <summary>The provider's own message/tracking id, correlated against webhook callbacks (NTF-15) - present only once at least one dispatch attempt has actually reached the provider.</summary>
    public string? ProviderMessageId { get; private set; }

    public string? LastError { get; private set; }

    public DeadLetterReason? DeadLetterReason { get; private set; }

    /// <summary>Denormalized snapshot of what was actually resolved/sent - lets the in-app notification center (NTF-12) and admin triage (NTF-14) render the message without re-resolving the template after the fact.</summary>
    public string? RenderedSubject { get; private set; }

    public string? RenderedBody { get; private set; }

    public string? RenderedDeepLink { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>True if a dispatch worker may claim this attempt right now (NTF-13's claim query uses this same condition, expressed in SQL, to stay consistent with this in-memory rule).</summary>
    public bool IsEligibleForDispatch(DateTimeOffset now) => Status switch
    {
        DeliveryAttemptStatus.Pending => true,
        DeliveryAttemptStatus.Retrying => NextAttemptAt is null || NextAttemptAt <= now,

        // A genuinely-stuck InFlight attempt (the original call hung past the documented timeout,
        // never resolved) - eligible again, per this type's own remarks on the InFlight/timeout
        // design. Still a residual, accepted race (edge-cases.md's own "Residual note").
        DeliveryAttemptStatus.InFlight => InFlightSince is not null && now - InFlightSince > InFlightTimeout,
        _ => false,
    };

    public void MarkInFlight(DateTimeOffset now)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            return;
        }

        Status = DeliveryAttemptStatus.InFlight;
        InFlightSince = now;
        UpdatedAt = now;
    }

    public void MarkDelivered(DateTimeOffset now, string? providerMessageId, string? renderedSubject, string? renderedBody, string? renderedDeepLink)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            // Idempotent no-op (§4 invariant) - most likely a retry racing a since-succeeded
            // original attempt (edge-cases.md).
            return;
        }

        Status = DeliveryAttemptStatus.Delivered;
        DeliveredAt = now;
        ProviderMessageId = providerMessageId ?? ProviderMessageId;
        RenderedSubject = renderedSubject;
        RenderedBody = renderedBody;
        RenderedDeepLink = renderedDeepLink;
        InFlightSince = null;
        UpdatedAt = now;
    }

    /// <summary>Records a transient failure and either schedules the next bounded-exponential-backoff retry or dead-letters on exhaustion. Returns true if this call dead-lettered the attempt.</summary>
    public bool MarkFailedTransient(string error, DateTimeOffset now)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            return false;
        }

        AttemptCount++;
        LastError = error;
        InFlightSince = null;

        if (AttemptCount >= MaxAttempts)
        {
            Status = DeliveryAttemptStatus.DeadLettered;
            DeadLetterReason = Requests.DeadLetterReason.RetriesExhausted;
            UpdatedAt = now;
            return true;
        }

        Status = DeliveryAttemptStatus.Retrying;
        NextAttemptAt = now + Backoff(AttemptCount);
        UpdatedAt = now;
        return false;
    }

    /// <summary>A provider rejected the send outright as non-retryable (e.g. invalid destination format) - immediate dead-letter, zero additional retries spent.</summary>
    public void MarkPermanentFailure(string error, DateTimeOffset now)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            return;
        }

        LastError = error;
        InFlightSince = null;
        Status = DeliveryAttemptStatus.DeadLettered;
        DeadLetterReason = Requests.DeadLetterReason.ProviderRejected;
        UpdatedAt = now;
    }

    /// <summary>§8 edge case: "fails fast with a distinguishable NoContactInfo status instead of retrying pointlessly" - zero attempts spent, immediate dead-letter.</summary>
    public void MarkNoContactInfo(DateTimeOffset now)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            return;
        }

        Status = DeliveryAttemptStatus.DeadLettered;
        DeadLetterReason = Requests.DeadLetterReason.NoContactInfo;
        UpdatedAt = now;
    }

    /// <summary>design-decisions.md, "Template Resolution Fallback": missing-English is a configuration bug, dead-lettered immediately, never retried.</summary>
    public void MarkTemplateMissing(DateTimeOffset now)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            return;
        }

        Status = DeliveryAttemptStatus.DeadLettered;
        DeadLetterReason = Requests.DeadLetterReason.TemplateMissing;
        UpdatedAt = now;
    }

    /// <summary>§9 Decision 3/edge-cases.md "opt-out change racing an in-flight send decision" - the send-time gate for a category the recipient has opted out of. A successful no-op, not a failure.</summary>
    public void MarkSuppressedByOptOut(DateTimeOffset now)
    {
        if (Status == DeliveryAttemptStatus.Delivered)
        {
            return;
        }

        Status = DeliveryAttemptStatus.Suppressed;
        UpdatedAt = now;
    }

    /// <summary>NTF-12: mark-read for the in-app notification center. Only meaningful for <see cref="NotificationChannel.InApp"/>, but not restricted here - the API layer only ever calls this for a caller's own InApp rows.</summary>
    public void MarkRead(DateTimeOffset now)
    {
        ReadAt ??= now;
    }

    internal static NotificationDeliveryAttempt CreatePending(NotificationRequestId requestId, NotificationChannel channel, DateTimeOffset now) => new()
    {
        Id = NotificationDeliveryAttemptId.New(),
        NotificationRequestId = requestId,
        Channel = channel,
        Status = DeliveryAttemptStatus.Pending,
        AttemptCount = 0,
        NextAttemptAt = now,
        CreatedAt = now,
        UpdatedAt = now,
    };

    /// <summary>Bounded exponential backoff with a fixed base (ADR-0014) - 10s, 20s, 40s, 80s for attempts 1-4 (the 5th failure dead-letters instead of scheduling a 5th wait).</summary>
    private static TimeSpan Backoff(int attemptNumber) => TimeSpan.FromSeconds(Math.Pow(2, attemptNumber) * 5);
}
