namespace UMS.Modules.Notifications.Infrastructure.Providers;

/// <summary>
/// Tunable behavior for the in-process fake gateway every channel provider talks to (see
/// <see cref="FakeProviderPrimaryHandler"/>'s own remarks for why this build fakes the provider
/// rather than integrating a real Twilio/SendGrid/Meta WhatsApp Business API/FCM account). Bound
/// per channel from configuration (<c>Notifications:FakeGateway:Email</c>,
/// <c>:Sms</c>, <c>:Push</c>, <c>:WhatsApp</c>) so a manual test can flip
/// <see cref="ForceOutage"/> to <c>true</c> for one channel (edge-cases.md's "SMS provider outage"
/// case) without touching the others.
/// </summary>
public sealed class FakeProviderGatewayOptions
{
    /// <summary>0.0-1.0 - the fraction of otherwise-successful sends that instead return a simulated transient (503) failure, to exercise NTF-13's retry/backoff path for real.</summary>
    public double FailureRate { get; set; } = 0.15;

    /// <summary>When true, every send simulated by this channel fails - used to manually exercise edge-cases.md's "SMS provider outage" -&gt; dead-letter-after-bounded-retries case.</summary>
    public bool ForceOutage { get; set; }

    public int MinLatencyMs { get; set; } = 20;

    public int MaxLatencyMs { get; set; } = 180;
}
