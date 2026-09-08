using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.UnitTests.Payments;

/// <summary>
/// FIN-11: requirement-spec.md §2/§4's refund invariants - "Refund.amount &lt;= Payment.amount -
/// sum(prior Refund.amount)"; "a Refund against an already-fully-refunded Payment is rejected."
/// design-decisions.md "Refund Concurrency Control" names the DB-enforced transactional check as the
/// mechanism; these tests exercise the pure domain invariant <see cref="Payment.ValidateRefundRequest"/>
/// / <see cref="Payment.RecordRefund"/> enforce regardless of which caller (RefundService, under a
/// row lock) invokes them.
/// </summary>
public sealed class RefundTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();
    private static readonly Guid AccountantId = Guid.NewGuid();

    private static Payment SuccessfulPayment(decimal amount = 500m)
    {
        var invoice = Invoice.Create(
            "admission",
            "app-1",
            "ApplicationFee",
            OwnerId,
            FeeStructure.CreateInitialVersion("ApplicationFee", FeeApplicability.ForService("Admission").Value, Money.Create(amount).Value, Now, Now).Value,
            Now).Value;

        var payment = Payment.Initiate(invoice, Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;
        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));
        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Successful, Now.AddSeconds(2));
        payment.ClearDomainEvents();
        return payment;
    }

    [Fact]
    public void A_refund_against_a_non_Successful_non_Reconciled_Payment_is_rejected()
    {
        var invoice = Invoice.Create(
            "admission",
            "app-2",
            "ApplicationFee",
            OwnerId,
            FeeStructure.CreateInitialVersion("ApplicationFee", FeeApplicability.ForService("Admission").Value, Money.Create(500m).Value, Now, Now).Value,
            Now).Value;
        var payment = Payment.Initiate(invoice, Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;

        var result = payment.ValidateRefundRequest(Money.Create(100m).Value);

        Assert.True(result.IsFailure);
        Assert.Equal("refund.payment_not_refundable", result.Error!.Code);
    }

    [Fact]
    public void A_partial_refund_within_the_remaining_amount_succeeds_and_raises_RefundRequested_and_RefundCompleted()
    {
        var payment = SuccessfulPayment(500m);

        var result = payment.RecordRefund(Money.Create(200m).Value, AccountantId, RefundMethod.GatewayRouted, "fake-ref-1", succeeded: true, failureReason: null, Now.AddMinutes(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Succeeded, result.Value.Status);
        Assert.Equal(200m, payment.SucceededRefundTotal);
        Assert.Equal(300m, payment.RemainingRefundableAmount);
        Assert.Contains(payment.DomainEvents, e => e is RefundRequested);
        Assert.Contains(payment.DomainEvents, e => e is RefundCompleted);
    }

    [Fact]
    public void A_refund_amount_exceeding_the_remaining_refundable_amount_is_rejected()
    {
        var payment = SuccessfulPayment(500m);

        var result = payment.ValidateRefundRequest(Money.Create(500.01m).Value);

        Assert.True(result.IsFailure);
        Assert.Equal("refund.amount_exceeds_remaining", result.Error!.Code);
    }

    [Fact]
    public void The_full_amount_can_be_refunded_in_one_call()
    {
        var payment = SuccessfulPayment(500m);

        var result = payment.RecordRefund(Money.Create(500m).Value, AccountantId, RefundMethod.ManuallySettled, gatewayRefundReference: null, succeeded: true, failureReason: null, Now.AddMinutes(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, payment.RemainingRefundableAmount);
    }

    [Fact]
    public void A_second_refund_exceeding_what_remains_after_a_first_partial_refund_is_rejected()
    {
        var payment = SuccessfulPayment(500m);
        payment.RecordRefund(Money.Create(300m).Value, AccountantId, RefundMethod.GatewayRouted, "fake-ref-1", succeeded: true, failureReason: null, Now.AddMinutes(5));

        var second = payment.ValidateRefundRequest(Money.Create(300m).Value);

        Assert.True(second.IsFailure);
        Assert.Equal("refund.amount_exceeds_remaining", second.Error!.Code);
    }

    [Fact]
    public void A_refund_against_an_already_fully_refunded_Payment_is_rejected_outright()
    {
        var payment = SuccessfulPayment(500m);
        payment.RecordRefund(Money.Create(500m).Value, AccountantId, RefundMethod.GatewayRouted, "fake-ref-1", succeeded: true, failureReason: null, Now.AddMinutes(5));

        var result = payment.ValidateRefundRequest(Money.Create(1m).Value);

        Assert.True(result.IsFailure);
        Assert.Equal("refund.payment_already_fully_refunded", result.Error!.Code);
    }

    [Fact]
    public void A_zero_or_negative_refund_amount_is_rejected()
    {
        var payment = SuccessfulPayment(500m);

        var result = payment.ValidateRefundRequest(Money.Zero);

        Assert.True(result.IsFailure);
        Assert.Equal("refund.amount_must_be_positive", result.Error!.Code);
    }

    /// <summary>A Failed refund attempt (e.g. the gateway itself rejected it) never consumes any of the amount ceiling - only a Succeeded Refund counts.</summary>
    [Fact]
    public void A_Failed_refund_outcome_does_not_consume_the_amount_ceiling()
    {
        var payment = SuccessfulPayment(500m);

        var result = payment.RecordRefund(Money.Create(200m).Value, AccountantId, RefundMethod.GatewayRouted, gatewayRefundReference: null, succeeded: false, failureReason: "simulated_gateway_refund_rejected", Now.AddMinutes(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(RefundStatus.Failed, result.Value.Status);
        Assert.Equal(0m, payment.SucceededRefundTotal);
        Assert.Equal(500m, payment.RemainingRefundableAmount);
        Assert.Contains(payment.DomainEvents, e => e is RefundRequested);
        Assert.DoesNotContain(payment.DomainEvents, e => e is RefundCompleted);
    }

    [Fact]
    public void A_Reconciled_Payment_can_still_be_refunded()
    {
        var payment = SuccessfulPayment(500m);
        payment.MarkReconciled(Now.AddMinutes(1));

        var result = payment.ValidateRefundRequest(Money.Create(100m).Value);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void MarkReconciled_transitions_a_Successful_Payment_and_raises_PaymentReconciled()
    {
        var payment = SuccessfulPayment(500m);

        var result = payment.MarkReconciled(Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Reconciled, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentReconciled);
    }

    [Fact]
    public void MarkReconciled_against_a_non_Successful_Payment_is_rejected()
    {
        var invoice = Invoice.Create(
            "admission",
            "app-3",
            "ApplicationFee",
            OwnerId,
            FeeStructure.CreateInitialVersion("ApplicationFee", FeeApplicability.ForService("Admission").Value, Money.Create(500m).Value, Now, Now).Value,
            Now).Value;
        var payment = Payment.Initiate(invoice, Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;

        var result = payment.MarkReconciled(Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("payment.not_reconcilable", result.Error!.Code);
    }

    [Fact]
    public void MarkReconciled_is_not_reachable_twice()
    {
        var payment = SuccessfulPayment(500m);
        payment.MarkReconciled(Now.AddMinutes(1));

        var result = payment.MarkReconciled(Now.AddMinutes(2));

        Assert.True(result.IsFailure);
        Assert.Equal("payment.not_reconcilable", result.Error!.Code);
    }
}
