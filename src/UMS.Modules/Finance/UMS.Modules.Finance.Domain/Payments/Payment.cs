using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Domain.Payments;

/// <summary>
/// FIN-5/FIN-6/FIN-8: one payment attempt cycle against exactly one <see cref="Invoice"/>
/// (requirement-spec.md finance §3/§4: "Invoice is satisfied by exactly one Successful/Reconciled
/// Payment").
///
/// <para>
/// <see cref="OwnerId"/>/<see cref="SourceModule"/>/<see cref="SourceReferenceId"/> are denormalized
/// from the owning <see cref="Invoice"/> at <see cref="Initiate"/> time - the same "capture once so
/// a fan-out event needs no second lookup" pattern Learning's own <c>Assignment.CreatedByUserId</c>
/// uses - so <see cref="ApplyGatewayWebhook"/> can raise a fully-populated
/// <see cref="PaymentSucceeded"/>/<see cref="PaymentFailed"/> event without loading the Invoice a
/// second time inside the webhook's own transaction.
/// </para>
/// </summary>
public sealed class Payment : AggregateRoot<PaymentId>
{
    private readonly List<PaymentTransaction> _transactions = [];
    private readonly List<Refund> _refunds = [];

    private Payment()
    {
    }

    private Payment(PaymentId id, InvoiceId invoiceId, Guid ownerId, string sourceModule, string sourceReferenceId, Guid initiatedByUserId, string idempotencyKey, Money amount, DateTimeOffset now)
    {
        Id = id;
        InvoiceId = invoiceId;
        OwnerId = ownerId;
        SourceModule = sourceModule;
        SourceReferenceId = sourceReferenceId;
        InitiatedByUserId = initiatedByUserId;
        IdempotencyKey = idempotencyKey;
        Amount = amount;
        Status = PaymentStatus.Initiated;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public InvoiceId InvoiceId { get; private set; }

    public Guid OwnerId { get; private set; }

    public string SourceModule { get; private set; } = string.Empty;

    public string SourceReferenceId { get; private set; } = string.Empty;

    public Guid InitiatedByUserId { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public Money Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<PaymentTransaction> Transactions => _transactions.AsReadOnly();

    public IReadOnlyCollection<Refund> Refunds => _refunds.AsReadOnly();

    /// <summary>This build's Payment Core slice only ever creates one attempt per Payment - see <see cref="PaymentTransaction"/>'s own remarks.</summary>
    public PaymentTransaction CurrentTransaction => _transactions[0];

    public bool IsNonTerminal => Status is PaymentStatus.Initiated or PaymentStatus.Pending;

    /// <summary>design-decisions.md "Refund Concurrency Control": the amount-ceiling invariant's own running total, computed from already-committed Succeeded Refunds only - a Failed refund attempt never consumes any of the ceiling.</summary>
    public decimal SucceededRefundTotal => _refunds.Where(r => r.Status == RefundStatus.Succeeded).Sum(r => r.Amount);

    public decimal RemainingRefundableAmount => Amount.Amount - SucceededRefundTotal;

    public static Result<Payment> Initiate(Invoice invoice, Guid initiatedByUserId, string idempotencyKey, string gatewayName, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Error.Validation("payment.idempotency_key_required", "A Payment initiation requires a non-empty Idempotency-Key.");
        }

        if (!invoice.HasOutstandingBalance)
        {
            return Error.Conflict("payment.invoice_already_satisfied", $"Invoice '{invoice.Id}' has no outstanding balance and cannot accept a new Payment.");
        }

        var payment = new Payment(PaymentId.New(), invoice.Id, invoice.OwnerId, invoice.SourceModule, invoice.SourceReferenceId, initiatedByUserId, idempotencyKey.Trim(), invoice.TotalAmount, now);
        var transaction = PaymentTransaction.StartFirstAttempt(payment.Id, gatewayName, now);
        payment._transactions.Add(transaction);
        payment.Raise(new PaymentInitiated(payment.Id.Value, invoice.Id.Value, payment.Amount.Amount, now));
        return payment;
    }

    /// <summary>Called right after <c>IPaymentGateway.InitiateAsync</c> returns.</summary>
    public void RecordGatewaySessionReference(string sessionReference, DateTimeOffset now)
    {
        CurrentTransaction.RecordGatewaySessionReference(sessionReference, now);
        Status = PaymentStatus.Pending;
        UpdatedAt = now;
    }

    /// <summary>
    /// The sole entry point for a gateway-reported status, called identically by the live webhook
    /// handler and the stuck-payment polling job (design-decisions.md's "Webhook Signature
    /// Verification and State-Transition Ordering"). Always returns success - a no-op (duplicate or
    /// out-of-order report) is not an error, it is the correct, silent outcome
    /// (edge-cases.md "Duplicate Webhook Delivery", "Out-of-Order Webhook Delivery").
    /// </summary>
    public Result ApplyGatewayWebhook(string gatewayTransactionId, PaymentStatus reportedStatus, DateTimeOffset now)
    {
        var transaction = CurrentTransaction;

        if (transaction.GatewayTransactionId is not null && !string.Equals(transaction.GatewayTransactionId, gatewayTransactionId, StringComparison.Ordinal))
        {
            // Defensive only: this Payment's single attempt already recorded a DIFFERENT gateway
            // transaction id than the one now reported - cannot happen with a real gateway
            // (its own tran_id/val_id pairing is stable per attempt), never mutates.
            return Result.Success();
        }

        if (!transaction.TryApplyGatewayStatus(gatewayTransactionId, reportedStatus, now))
        {
            return Result.Success();
        }

        Status = transaction.Status;
        UpdatedAt = now;

        switch (reportedStatus)
        {
            case PaymentStatus.Successful:
                Raise(new PaymentSucceeded(Id.Value, InvoiceId.Value, OwnerId, SourceModule, SourceReferenceId, Amount.Amount, now));
                break;
            case PaymentStatus.Failed:
                Raise(new PaymentFailed(Id.Value, InvoiceId.Value, OwnerId, transaction.FailureReason ?? "gateway_reported_failure", now));
                break;
        }

        return Result.Success();
    }

    /// <summary>FIN-9/FIN-10: the stuck-payment sweep's own path to the identical state-transition guard (edge-cases.md "Webhook never arrives" / "Gateway outage during POST /payments").</summary>
    public Result MarkStale(string reason, DateTimeOffset now)
    {
        if (!IsNonTerminal)
        {
            return Result.Failure(Error.Conflict("payment.already_terminal", $"Payment '{Id}' is already '{Status}' and cannot be marked stale."));
        }

        CurrentTransaction.MarkStale(reason, now);
        Status = PaymentStatus.Failed;
        UpdatedAt = now;
        Raise(new PaymentFailed(Id.Value, InvoiceId.Value, OwnerId, reason, now));
        return Result.Success();
    }

    /// <summary>
    /// FIN-11: requirement-spec.md §2/§4 - a Refund reverses a previously Successful/Reconciled
    /// Payment, capped at <see cref="Amount"/> minus the sum of prior Succeeded Refunds on this same
    /// Payment; a Refund against an already-fully-refunded Payment is rejected outright. Pure
    /// validation only (no mutation) - the caller (<c>RefundService</c>) runs this under the same
    /// pessimistic row lock design-decisions.md's "Refund Concurrency Control" specifies, BEFORE
    /// calling the gateway, and again defensively right before <see cref="RecordRefund"/> actually
    /// commits the outcome.
    /// </summary>
    public Result ValidateRefundRequest(Money amount)
    {
        if (Status is not (PaymentStatus.Successful or PaymentStatus.Reconciled))
        {
            return Result.Failure(Error.Conflict("refund.payment_not_refundable", $"Payment '{Id}' is '{Status}' - only a Successful or Reconciled Payment can be refunded."));
        }

        if (RemainingRefundableAmount <= 0)
        {
            return Result.Failure(Error.Conflict("refund.payment_already_fully_refunded", $"Payment '{Id}' has already been fully refunded."));
        }

        if (amount.Amount <= 0)
        {
            return Result.Failure(Error.Validation("refund.amount_must_be_positive", "A Refund amount must be greater than zero."));
        }

        if (amount.Amount > RemainingRefundableAmount)
        {
            return Result.Failure(Error.Conflict("refund.amount_exceeds_remaining", $"Refund amount {amount.Amount} exceeds the {RemainingRefundableAmount} still refundable on Payment '{Id}'."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Commits the Refund outcome (gateway-routed or manually-settled) once known - re-validates the
    /// amount-ceiling/status invariant defensively (the authoritative check runs under the same
    /// pessimistic row lock this call itself executes inside; see <c>RefundService</c>) before
    /// appending the Refund child row, raising <see cref="RefundRequested"/> for parity with
    /// requirement-spec.md §3's own event catalog, and - only once the outcome is a genuine success -
    /// <see cref="RefundCompleted"/>.
    /// </summary>
    public Result<Refund> RecordRefund(Money amount, Guid requestedByUserId, RefundMethod method, string? gatewayRefundReference, bool succeeded, string? failureReason, DateTimeOffset now)
    {
        var validation = ValidateRefundRequest(amount);
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        var refund = Refund.Create(CurrentTransaction.Id, Id, amount, succeeded ? RefundStatus.Succeeded : RefundStatus.Failed, method, requestedByUserId, gatewayRefundReference, failureReason, now);
        _refunds.Add(refund);
        UpdatedAt = now;

        Raise(new RefundRequested(refund.Id.Value, Id.Value, OwnerId, amount.Amount, now));

        if (succeeded)
        {
            Raise(new RefundCompleted(refund.Id.Value, Id.Value, InvoiceId.Value, OwnerId, amount.Amount, now));
        }

        return refund;
    }

    /// <summary>
    /// FIN-14: the daily reconciliation job's sole mutation path (design-decisions.md "Reconciliation
    /// Job Concurrency-Safety") - Reconciled is reachable only from Successful (requirement-spec.md
    /// §4), forward-only like every other transition on this aggregate.
    /// </summary>
    public Result MarkReconciled(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Successful)
        {
            return Result.Failure(Error.Conflict("payment.not_reconcilable", $"Payment '{Id}' is '{Status}' - only a Successful Payment can be marked Reconciled."));
        }

        Status = PaymentStatus.Reconciled;
        UpdatedAt = now;
        Raise(new PaymentReconciled(Id.Value, InvoiceId.Value, now));
        return Result.Success();
    }
}
