namespace UMS.Modules.Learning.Infrastructure.Plagiarism;

/// <summary>
/// Tunable behavior for the in-process fake similarity-check gateway (see
/// <see cref="FakePlagiarismProviderHandler"/>'s own remarks for why this build fakes the provider
/// rather than integrating a real Turnitin/iThenticate/Copyleaks account). Bound from
/// <c>Learning:PlagiarismProvider</c> so a manual test can flip <see cref="ForceOutage"/> to
/// exercise edge-cases.md's "The plagiarism-check provider is slow or down" case for real, without
/// touching code. Mirrors Notifications' own <c>FakeProviderGatewayOptions</c> exactly.
/// </summary>
public sealed class FakePlagiarismGatewayOptions
{
    /// <summary>The name recorded on every <c>PlagiarismScore</c> this provider produces.</summary>
    public string ProviderName { get; set; } = "ums-fake-similarity-gateway";

    /// <summary>0.0-1.0 - the fraction of otherwise-successful checks that instead return a simulated transient (503) failure, so the Polly retry path is exercised for real rather than only in theory.</summary>
    public double FailureRate { get; set; }

    /// <summary>When true, every check fails - the switch for manually exercising the sustained-outage case (a Failed terminal status that never blocks evaluation).</summary>
    public bool ForceOutage { get; set; }

    public int MinLatencyMs { get; set; } = 20;

    public int MaxLatencyMs { get; set; } = 180;
}
