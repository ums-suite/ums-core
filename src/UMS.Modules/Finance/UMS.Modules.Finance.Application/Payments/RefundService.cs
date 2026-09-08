using System.Text.Json;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Common;
using UMS.Modules.Finance.Domain.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.Ledger;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.Payments;

/// <summary>
/// FIN-11: <c>POST /api/v1/finance/payments/{id}/refund</c> - Accountant/Admin only
/// (<c>finance.payment.refund</c>), not ownership-scoped (mirrors <c>FeeStructureManage</c>/
/// <c>InvoiceCreate</c>'s own permission-gated-not-ownership-scoped shape, not
/// <c>PaymentInitiate</c>/<c>PaymentRead</c>'s owner-scoped one).
///
/// <para>
/// Runs the whole validate -&gt; gateway-call -&gt; insert sequence inside ONE pessimistic-lock
/// transaction on the <see cref="Payment"/> row (design-decisions.md "Refund Concurrency Control":
/// "DB-enforced transactional check ... in the same transaction as the refund insert"). This
/// deliberately differs from <see cref="PaymentService"/>'s own Payment-initiation flow, which splits
/// the gateway call OUT of its lock/transaction specifically to protect requirement-spec.md §5's
/// "the initiation call itself must still return promptly" NFR for a high-volume, latency-sensitive
/// endpoint - no equivalent NFR exists anywhere in the spec for the low-volume, Accountant-only
/// refund path, so holding the lock across the (fake) gateway's own refund call here is the simpler,
/// more literal reading of the design decision's "same transaction" text, not a shortcut.
/// </para>
/// </summary>
public sealed class RefundService(
    IPaymentRepository payments,
    ILedgerEntryRepository ledgerEntries,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<RefundDto>> RefundAsync(Guid paymentId, AuditContext audit, RefundPaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(request);

        var amountResult = Money.Create(request.Amount);
        if (amountResult.IsFailure)
        {
            return amountResult.Error!;
        }

        var amount = amountResult.Value;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var payment = await payments.GetByIdForUpdateAsync(new PaymentId(paymentId), cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("refund.payment_not_found", $"No Payment exists with id '{paymentId}'.");
        }

        // design-decisions.md "Refund Concurrency Control": the amount-ceiling/status invariant is
        // checked HERE, under the pessimistic row lock GetByIdForUpdateAsync just took - the same
        // lock the refund insert itself commits under a few lines below, closing the race window
        // between two concurrent refund requests against the same Payment.
        var validation = payment.ValidateRefundRequest(amount);
        if (validation.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return validation.Error!;
        }

        var remainingBefore = payment.RemainingRefundableAmount;
        var statusBefore = payment.Status;

        var gatewayTransactionId = payment.CurrentTransaction.GatewayTransactionId;
        GatewayRefundResult? gatewayResult = null;
        if (gatewayTransactionId is not null)
        {
            try
            {
                gatewayResult = await gateway.RefundAsync(
                    new GatewayRefundRequest(payment.Id.Value.ToString(), gatewayTransactionId, amount.Amount, amount.Currency),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return Error.Failure("refund.gateway_unavailable", $"The payment gateway is currently unavailable ({ex.Message}) - retry the refund request.");
            }
        }

        // requirement-spec.md §9's refund-execution decision: gateway-routed where the provider
        // supports it (a non-null result); otherwise an Accountant marks it manually settled right
        // here, at the point of THIS call - there is no separate confirmation step in this pass (see
        // RefundStatus's own remarks for why).
        var method = gatewayResult is null ? RefundMethod.ManuallySettled : RefundMethod.GatewayRouted;
        var succeeded = gatewayResult?.Succeeded ?? true;
        var gatewayReference = gatewayResult?.GatewayRefundReference;
        var failureReason = gatewayResult is { Succeeded: false } ? gatewayResult.FailureReason : null;

        var now = clock.UtcNow;
        var recorded = payment.RecordRefund(amount, audit.ActorUserId, method, gatewayReference, succeeded, failureReason, now);
        if (recorded.IsFailure)
        {
            // Defensive only - ValidateRefundRequest already passed above under this SAME lock, with
            // no writer able to intervene between that check and this one.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return recorded.Error!;
        }

        var refund = recorded.Value;

        if (succeeded)
        {
            // FIN-12/design-decisions.md "Ledger-Entry Atomicity Mechanism": direct, same-transaction
            // write, never routed through the outbox itself - identical posture to how PaymentPosted
            // is already written in PaymentWebhookService.
            var ledgerEntry = LedgerEntry.Create(
                LedgerEntryType.RefundPosted,
                "Payment",
                payment.Id.Value,
                amount.Amount,
                amount.Currency,
                $"Refund posted against Payment '{payment.Id}' ({method}).",
                audit.CorrelationId,
                now);
            ledgerEntries.Add(ledgerEntry);
            domainEvents.Enqueue(new LedgerEntryPosted(ledgerEntry.Id.Value, ledgerEntry.EntryType.ToString(), ledgerEntry.ReferenceType, ledgerEntry.ReferenceId, ledgerEntry.Amount, now));
        }

        // requirement-spec.md §4's last bullet / ADR-0012: every Refund write calls Audit
        // synchronously, in the same transaction as the mutation itself.
        var auditRequest = audit.ToRequest(
            "Payment",
            payment.Id.Value.ToString(),
            "refund",
            JsonSerializer.Serialize(new { status = statusBefore.ToString(), remainingRefundable = remainingBefore }),
            JsonSerializer.Serialize(new { refundId = refund.Id.Value, amount = amount.Amount, method = method.ToString(), succeeded, remainingRefundable = payment.RemainingRefundableAmount }),
            request.Reason);

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        return ToDto(refund, payment.Id.Value);
    }

    internal static RefundDto ToDto(Refund refund, Guid paymentId) => new(
        refund.Id.Value,
        paymentId,
        refund.Amount,
        refund.Currency,
        refund.Status.ToString(),
        refund.Method.ToString(),
        refund.GatewayRefundReference,
        refund.CreatedAt);
}
