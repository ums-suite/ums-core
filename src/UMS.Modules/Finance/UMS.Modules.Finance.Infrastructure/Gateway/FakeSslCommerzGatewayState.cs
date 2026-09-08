using System.Collections.Concurrent;

namespace UMS.Modules.Finance.Infrastructure.Gateway;

/// <summary>
/// <b>Not a real provider integration.</b> This environment has no real SSLCommerz sandbox
/// credentials available, so <see cref="FakeSslCommerzPrimaryHandler"/> talks to this in-process,
/// per-merchantTransactionId state store instead of a real socket - mirroring Notifications' own
/// <c>FakeProviderPrimaryHandler</c> posture (a genuine resilience pipeline wrapped around a
/// simulated terminal), extended here with real per-transaction state since FIN-9's stuck-payment
/// poll needs somewhere stateful to actually query, and FIN-14's daily reconciliation job needs the
/// same state shaped as a batch settlement report (<see cref="GetSettlementReport"/>).
///
/// <para>
/// Registered as a singleton and resolvable directly from the DI container by integration tests
/// (<c>services.GetRequiredService&lt;FakeSslCommerzGatewayState&gt;()</c>) - the one deliberately
/// public seam that lets a test simulate "the gateway itself now reports X" for FIN-9's polling path
/// and FIN-14's reconciliation path alike, the same role a test-only endpoint plays for Notifications'
/// own webhook verification (its own remarks: "synthetic payloads posted directly", not triggered by
/// the fake gateway automatically).
/// </para>
/// </summary>
public sealed class FakeSslCommerzGatewayState
{
    private readonly ConcurrentDictionary<string, TransactionRecord> _byMerchantTransactionId = new();
    private readonly ConcurrentDictionary<string, byte> _refundUnsupported = new();

    public (string GatewayTransactionId, string Status) RecordInitiation(string merchantTransactionId)
    {
        var record = new TransactionRecord($"fake-sslcommerz-{Guid.NewGuid():N}", "Pending", DateTimeOffset.UtcNow);
        _byMerchantTransactionId[merchantTransactionId] = record;
        return (record.GatewayTransactionId, record.Status);
    }

    public (string GatewayTransactionId, string Status)? Query(string merchantTransactionId) =>
        _byMerchantTransactionId.TryGetValue(merchantTransactionId, out var record) ? (record.GatewayTransactionId, record.Status) : null;

    /// <summary>Test-only hook simulating the gateway later confirming a status - e.g. the payer completed payment at the gateway, or a bank later reports failure - for FIN-9's polling path and FIN-14's reconciliation "match"/"mismatch" scenarios alike.</summary>
    public void SetStatus(string merchantTransactionId, string status) =>
        _byMerchantTransactionId.AddOrUpdate(
            merchantTransactionId,
            _ => new TransactionRecord($"fake-sslcommerz-{Guid.NewGuid():N}", status, DateTimeOffset.UtcNow),
            (_, existing) => existing with { Status = status, RecordedAt = DateTimeOffset.UtcNow });

    public void Forget(string merchantTransactionId)
    {
        _byMerchantTransactionId.TryRemove(merchantTransactionId, out _);
        _refundUnsupported.TryRemove(merchantTransactionId, out _);
    }

    /// <summary>FIN-11 test-only hook: simulates a payment method the gateway does NOT support a programmatic refund for (requirement-spec.md §9's "otherwise ... manually settled" branch).</summary>
    public void MarkRefundUnsupported(string merchantTransactionId) => _refundUnsupported[merchantTransactionId] = 1;

    public bool IsRefundUnsupported(string merchantTransactionId) => _refundUnsupported.ContainsKey(merchantTransactionId);

    /// <summary>FIN-14: the batch-shaped read the daily reconciliation job needs - every transaction this fake gateway last recorded/updated on <paramref name="settlementDate"/> (UTC calendar day), consistent with how <see cref="Query"/> is already a per-transaction fake lookup.</summary>
    public IReadOnlyList<(string MerchantTransactionId, string GatewayTransactionId, string Status)> GetSettlementReport(DateOnly settlementDate) =>
        _byMerchantTransactionId
            .Where(kvp => DateOnly.FromDateTime(kvp.Value.RecordedAt.UtcDateTime) == settlementDate)
            .Select(kvp => (kvp.Key, kvp.Value.GatewayTransactionId, kvp.Value.Status))
            .ToList();

    private sealed record TransactionRecord(string GatewayTransactionId, string Status, DateTimeOffset RecordedAt);
}
