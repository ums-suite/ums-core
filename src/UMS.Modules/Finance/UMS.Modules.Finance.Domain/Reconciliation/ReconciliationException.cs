using System.Diagnostics.CodeAnalysis;
using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.Domain.Reconciliation;

/// <summary>
/// FIN-14: requirement-spec.md finance §3's module-local term "ReconciliationException" - "A daily-
/// reconciliation mismatch between Finance's internal PaymentTransaction state and the gateway
/// settlement report, held for manual Accountant review - never auto-corrected." A own table, own
/// entity, not folded into <c>LedgerEntry</c> - a mismatch here has NOT (yet) moved any money, so it
/// is not itself a financial event (design-decisions.md "Reconciliation Job Concurrency-Safety";
/// edge-cases.md "Daily Reconciliation Job Racing a Live In-Flight Payment").
///
/// <para>
/// Immutable by construction, the same append-only posture <see cref="Ledger.LedgerEntry"/> takes -
/// this build's own scope (FIN-14) is detection only; the Accountant review/resolution workflow
/// implied by <c>finance.reconciliation.review</c> is a further ticket, not decomposed here.
/// </para>
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "\"ReconciliationException\" is requirement-spec.md finance §3's own literal module-local term - a domain noun, not a .NET exception type (it never derives from System.Exception).")]
public sealed class ReconciliationException
{
    private ReconciliationException()
    {
    }

    private ReconciliationException(ReconciliationExceptionId id, PaymentId paymentId, PaymentTransactionId paymentTransactionId, DateOnly settlementDate, ReconciliationMismatchReason reason, string? expectedGatewayTransactionId, string? actualGatewayTransactionId, string? actualGatewayStatus, string details, DateTimeOffset detectedAt)
    {
        Id = id;
        PaymentId = paymentId;
        PaymentTransactionId = paymentTransactionId;
        SettlementDate = settlementDate;
        Reason = reason;
        ExpectedGatewayTransactionId = expectedGatewayTransactionId;
        ActualGatewayTransactionId = actualGatewayTransactionId;
        ActualGatewayStatus = actualGatewayStatus;
        Details = details;
        DetectedAt = detectedAt;
    }

    public ReconciliationExceptionId Id { get; private init; }

    public PaymentId PaymentId { get; private init; }

    public PaymentTransactionId PaymentTransactionId { get; private init; }

    public DateOnly SettlementDate { get; private init; }

    public ReconciliationMismatchReason Reason { get; private init; }

    public string? ExpectedGatewayTransactionId { get; private init; }

    public string? ActualGatewayTransactionId { get; private init; }

    public string? ActualGatewayStatus { get; private init; }

    public string Details { get; private init; } = string.Empty;

    public DateTimeOffset DetectedAt { get; private init; }

    public static ReconciliationException Create(PaymentId paymentId, PaymentTransactionId paymentTransactionId, DateOnly settlementDate, ReconciliationMismatchReason reason, string? expectedGatewayTransactionId, string? actualGatewayTransactionId, string? actualGatewayStatus, string details, DateTimeOffset detectedAt) =>
        new(ReconciliationExceptionId.New(), paymentId, paymentTransactionId, settlementDate, reason, expectedGatewayTransactionId, actualGatewayTransactionId, actualGatewayStatus, details, detectedAt);
}
