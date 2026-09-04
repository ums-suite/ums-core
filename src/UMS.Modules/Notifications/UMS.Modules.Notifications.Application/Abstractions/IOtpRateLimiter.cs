namespace UMS.Modules.Notifications.Application.Abstractions;

/// <summary>
/// requirement-spec.md §5 NFR Rate Limiting: "OTP-triggering sends respect BRD §11's OTP rate-
/// limiting requirement to prevent abuse of the SMS channel" (NTF-10). Reuses the platform's shared
/// Redis-backed limiter (ums-conventions.md, Resilience &amp; Reliability: "ASP.NET Core's built-in
/// rate-limiting middleware, backed by the same Redis counters") - applied programmatically here
/// rather than via the ASP.NET Core middleware pipeline, since OTP requests arrive through the
/// in-process <c>INotificationRequestIntake</c> path (§9 Decision 1), not an inbound HTTP request.
/// </summary>
public interface IOtpRateLimiter
{
    /// <summary>True if this recipient may receive another OTP-category notification right now; false if they have exceeded the configured window/limit.</summary>
    public Task<bool> TryAcquireAsync(Guid recipientId, CancellationToken cancellationToken = default);
}
