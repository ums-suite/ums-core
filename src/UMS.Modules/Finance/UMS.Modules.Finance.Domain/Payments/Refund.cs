using UMS.Modules.Finance.Domain.Common;

namespace UMS.Modules.Finance.Domain.Payments;

/// <summary>
/// FIN-11: requirement-spec.md finance §3 "Refund | Entity | Child of Payment; references the
/// specific PaymentTransaction it reverses." Created and resolved synchronously, in one call,
/// against either the gateway's own refund capability or an Accountant's manual settlement
/// (requirement-spec.md §9's refund-execution decision) - see <see cref="RefundStatus"/>'s own
/// remarks for why there is no separate pending state.
///
/// <para>
/// <b>EF mapping note (a real, previously-hit limitation in this exact aggregate - see
/// <c>PaymentConfiguration</c>'s own remarks):</b> <see cref="Id"/> is this entity's own durable
/// business identity (used for external references - audit, notifications, the API response) but is
/// deliberately NOT the EF Core primary key for this <c>OwnsMany</c> collection. EF Core 10.0.11
/// cannot reliably tell a brand-new child row from an existing one when the owned collection's key is
/// a fully caller-supplied value AND the parent <see cref="Payment"/> is already tracked/persisted
/// (unlike <see cref="PaymentTransaction"/>, which is always added to a still-<c>Added</c>, not-yet-
/// persisted <see cref="Payment"/> at <see cref="Payment.Initiate"/> time) - it can misfire an
/// <c>UPDATE</c> instead of an <c>INSERT</c> and raise a spurious concurrency conflict. The actual EF
/// key is a shadow <c>Ordinal</c> position column (mirroring <c>InvoiceItem</c>'s own already-working
/// pattern), configured entirely in <c>PaymentConfiguration</c> - nothing on this type itself.
/// </para>
///
/// <para>
/// <see cref="Amount"/>/<see cref="Currency"/> are plain scalars rather than a nested <see cref="Money"/>
/// for the identical reason <c>InvoiceItem</c>'s own mapping documents: EF Core's <c>ComplexProperty</c>
/// builder has no support one level inside an already-owned collection in this EF Core version, and
/// <c>Money</c> being a struct rules out the <c>OwnsOne</c> alternative too. <see cref="AsMoney"/>
/// reconstructs the value object for any caller that wants it.
/// </para>
/// </summary>
public sealed class Refund
{
    private Refund()
    {
    }

    private Refund(RefundId id, PaymentId paymentId, PaymentTransactionId paymentTransactionId, decimal amount, string currency, RefundStatus status, RefundMethod method, Guid requestedByUserId, string? gatewayRefundReference, string? failureReason, DateTimeOffset now)
    {
        Id = id;
        PaymentId = paymentId;
        PaymentTransactionId = paymentTransactionId;
        Amount = amount;
        Currency = currency;
        Status = status;
        Method = method;
        RequestedByUserId = requestedByUserId;
        GatewayRefundReference = gatewayRefundReference;
        FailureReason = failureReason;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public RefundId Id { get; private init; }

    public PaymentId PaymentId { get; private init; }

    public PaymentTransactionId PaymentTransactionId { get; private init; }

    public decimal Amount { get; private init; }

    public string Currency { get; private init; } = string.Empty;

    public Money AsMoney => Money.FromStoredValue(Amount, Currency);

    public RefundStatus Status { get; private init; }

    public RefundMethod Method { get; private init; }

    public Guid RequestedByUserId { get; private init; }

    public string? GatewayRefundReference { get; private init; }

    public string? FailureReason { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset UpdatedAt { get; private init; }

    internal static Refund Create(PaymentTransactionId paymentTransactionId, PaymentId paymentId, Money amount, RefundStatus status, RefundMethod method, Guid requestedByUserId, string? gatewayRefundReference, string? failureReason, DateTimeOffset now) =>
        new(RefundId.New(), paymentId, paymentTransactionId, amount.Amount, amount.Currency, status, method, requestedByUserId, gatewayRefundReference, failureReason, now);
}
