using System.Collections.Concurrent;

namespace UMS.Modules.Finance.Infrastructure.Gateway;

/// <summary>
/// <b>Not a real provider integration.</b> This environment has no real SSLCommerz sandbox
/// credentials available, so <see cref="FakeSslCommerzPrimaryHandler"/> talks to this in-process,
/// per-merchantTransactionId state store instead of a real socket - mirroring Notifications' own
/// <c>FakeProviderPrimaryHandler</c> posture (a genuine resilience pipeline wrapped around a
/// simulated terminal), extended here with real per-transaction state since FIN-9's stuck-payment
/// poll needs somewhere stateful to actually query.
///
/// <para>
/// Registered as a singleton and resolvable directly from the DI container by integration tests
/// (<c>services.GetRequiredService&lt;FakeSslCommerzGatewayState&gt;()</c>) - the one deliberately
/// public seam that lets a test simulate "the gateway itself now reports Successful" for FIN-9's
/// polling path, the same role a test-only endpoint plays for Notifications' own webhook
/// verification (its own remarks: "synthetic payloads posted directly", not triggered by the fake
/// gateway automatically).
/// </para>
/// </summary>
public sealed class FakeSslCommerzGatewayState
{
    private readonly ConcurrentDictionary<string, (string GatewayTransactionId, string Status)> _byMerchantTransactionId = new();

    public (string GatewayTransactionId, string Status) RecordInitiation(string merchantTransactionId)
    {
        var entry = ($"fake-sslcommerz-{Guid.NewGuid():N}", "Pending");
        _byMerchantTransactionId[merchantTransactionId] = entry;
        return entry;
    }

    public (string GatewayTransactionId, string Status)? Query(string merchantTransactionId) =>
        _byMerchantTransactionId.TryGetValue(merchantTransactionId, out var entry) ? entry : null;

    /// <summary>Test-only hook simulating the gateway later confirming a status - e.g. the payer completed payment at the gateway, or a bank later reports failure - for FIN-9's polling path.</summary>
    public void SetStatus(string merchantTransactionId, string status) =>
        _byMerchantTransactionId.AddOrUpdate(
            merchantTransactionId,
            _ => ($"fake-sslcommerz-{Guid.NewGuid():N}", status),
            (_, existing) => (existing.GatewayTransactionId, status));

    public void Forget(string merchantTransactionId) => _byMerchantTransactionId.TryRemove(merchantTransactionId, out _);
}
