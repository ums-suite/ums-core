using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.FeeStructures;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Payments;

namespace UMS.Modules.Finance.UnitTests.Payments;

/// <summary>
/// FIN-5/FIN-6/FIN-8: requirement-spec.md §2 Payment State Machine, §4's forward-only invariant.
/// <see cref="Payment.ApplyGatewayWebhook"/> is the single idempotent, ordering-guarded transition
/// path design-decisions.md's "Webhook Signature Verification and State-Transition Ordering"
/// describes - these tests exercise every edge case that decision and edge-cases.md's "Duplicate
/// Webhook Delivery"/"Out-of-Order Webhook Delivery" name.
/// </summary>
public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid OwnerId = Guid.NewGuid();

    private static Invoice OpenInvoice() =>
        Invoice.Create(
            "admission",
            "app-1",
            "ApplicationFee",
            OwnerId,
            FeeStructure.CreateInitialVersion("ApplicationFee", FeeApplicability.ForService("Admission").Value, Money.Create(500m).Value, Now, Now).Value,
            Now).Value;

    [Fact]
    public void An_empty_idempotency_key_is_rejected()
    {
        var result = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), string.Empty, "SSLCommerz", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("payment.idempotency_key_required", result.Error!.Code);
    }

    [Fact]
    public void Initiating_against_an_Invoice_with_no_outstanding_balance_is_rejected()
    {
        var invoice = OpenInvoice();
        invoice.MarkPaid(Now);

        var result = Payment.Initiate(invoice, Guid.NewGuid(), "key-1", "SSLCommerz", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("payment.invoice_already_satisfied", result.Error!.Code);
    }

    [Fact]
    public void A_successful_Initiate_starts_in_Initiated_status_and_raises_PaymentInitiated()
    {
        var invoice = OpenInvoice();

        var payment = Payment.Initiate(invoice, Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;

        Assert.Equal(PaymentStatus.Initiated, payment.Status);
        Assert.Equal(invoice.TotalAmount.Amount, payment.Amount.Amount);
        Assert.Contains(payment.DomainEvents, e => e is PaymentInitiated);
    }

    [Fact]
    public void RecordGatewaySessionReference_moves_the_Payment_to_Pending()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;

        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));

        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public void A_webhook_reporting_Successful_from_Pending_transitions_and_raises_PaymentSucceeded()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;
        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));

        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Successful, Now.AddSeconds(2));

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentSucceeded);
    }

    [Fact]
    public void A_webhook_reporting_Failed_from_Pending_transitions_and_raises_PaymentFailed()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;
        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));

        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Failed, Now.AddSeconds(2));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentFailed);
    }

    /// <summary>edge-cases.md "Duplicate Webhook Delivery for the Same PaymentTransaction" - the second delivery is a silent no-op, never re-raising the event.</summary>
    [Fact]
    public void A_duplicate_webhook_reporting_the_same_status_twice_is_a_no_op_the_second_time()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;
        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));
        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Successful, Now.AddSeconds(2));
        payment.ClearDomainEvents();

        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Successful, Now.AddSeconds(3));

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.DoesNotContain(payment.DomainEvents, e => e is PaymentSucceeded);
    }

    /// <summary>edge-cases.md "Out-of-Order Webhook Delivery Causing a Backward State Transition" - a Failed report arriving after an already-committed Successful never flips it backward.</summary>
    [Fact]
    public void A_Failed_report_arriving_after_an_already_committed_Successful_never_flips_it_backward()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;
        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));
        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Successful, Now.AddSeconds(2));
        payment.ClearDomainEvents();

        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Failed, Now.AddSeconds(3));

        Assert.Equal(PaymentStatus.Successful, payment.Status);
        Assert.DoesNotContain(payment.DomainEvents, e => e is PaymentFailed);
    }

    [Fact]
    public void MarkStale_transitions_a_non_terminal_Payment_to_Failed()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;

        var result = payment.MarkStale("stale_initiated_timeout", Now.AddMinutes(30));

        Assert.True(result.IsSuccess);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Contains(payment.DomainEvents, e => e is PaymentFailed);
    }

    [Fact]
    public void MarkStale_against_an_already_terminal_Payment_is_rejected()
    {
        var payment = Payment.Initiate(OpenInvoice(), Guid.NewGuid(), "key-1", "SSLCommerz", Now).Value;
        payment.RecordGatewaySessionReference("session-abc", Now.AddSeconds(1));
        payment.ApplyGatewayWebhook("gw-tx-1", PaymentStatus.Successful, Now.AddSeconds(2));

        var result = payment.MarkStale("stale_initiated_timeout", Now.AddMinutes(30));

        Assert.True(result.IsFailure);
        Assert.Equal("payment.already_terminal", result.Error!.Code);
    }
}
