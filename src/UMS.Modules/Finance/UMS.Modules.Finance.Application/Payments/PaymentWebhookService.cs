using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Common;
using UMS.Modules.Finance.Domain.Events;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Ledger;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.Payments;

/// <summary>
/// FIN-8: gateway webhook ingestion - the single idempotent, ordering-guarded state-transition path
/// (design-decisions.md "Webhook Signature Verification and State-Transition Ordering") also reused
/// by <see cref="StuckPaymentSweepService"/> for its own polling path
/// (edge-cases.md "Stuck-Pending Polling Job Racing a Webhook That Arrives Mid-Poll").
///
/// <para>
/// Always returns a success <see cref="Result"/> to the caller (the gateway) - a signature failure
/// is the one exception (rejected outright, ADR-0008: "signature-verified before any state
/// transition"); an unknown Payment id, a duplicate delivery, or an out-of-order report are all
/// silent, safe no-ops, never surfaced as an error the gateway might reasonably retry-forever on
/// (edge-cases.md "Duplicate Webhook Delivery for the Same PaymentTransaction").
/// </para>
/// </summary>
public sealed class PaymentWebhookService(
    IPaymentRepository payments,
    IInvoiceRepository invoices,
    ILedgerEntryRepository ledgerEntries,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IDomainEventRecorder domainEvents,
    IAuditRecorder auditRecorder,
    IReceiptRequester receiptRequester,
    IClock clock,
    ILogger<PaymentWebhookService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Result> HandleAsync(Guid paymentId, string rawBody, string? signatureHeader, string correlationId, CancellationToken cancellationToken = default)
    {
        if (!gateway.VerifyWebhookSignature(rawBody, signatureHeader))
        {
            logger.LogWarning("Finance gateway webhook: signature verification failed for Payment {PaymentId}.", paymentId);
            return Result.Failure(Error.Unauthorized("payment.webhook_signature_invalid", "The webhook signature could not be verified."));
        }

        GatewayWebhookRequest? request;
        try
        {
            // The wire payload is camelCase (ums-conventions.md's platform-wide JSON convention);
            // this handler deserializes the raw body directly rather than through ASP.NET's own
            // [FromBody] binding (which defaults to case-insensitive matching already) - explicit
            // here for the identical, correct result.
            request = JsonSerializer.Deserialize<GatewayWebhookRequest>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            request = null;
        }

        if (request is null || !Enum.TryParse<PaymentStatus>(request.Status, ignoreCase: true, out var reportedStatus)
            || reportedStatus is not (PaymentStatus.Pending or PaymentStatus.Successful or PaymentStatus.Failed))
        {
            logger.LogWarning("Finance gateway webhook: malformed or unrecognized payload for Payment {PaymentId}.", paymentId);
            return Result.Success();
        }

        return await ApplyAsync(paymentId, request.GatewayTransactionId, reportedStatus, correlationId, "gateway-webhook", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Shared with <see cref="StuckPaymentSweepService"/>'s own polling path - see this class's own remarks.</summary>
    internal async Task<Result> ApplyAsync(Guid paymentId, string gatewayTransactionId, PaymentStatus reportedStatus, string correlationId, string sourceLabel, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var payment = await payments.GetByIdForUpdateAsync(new PaymentId(paymentId), cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            logger.LogWarning("Finance gateway webhook: no Payment exists with id {PaymentId} ({Source}).", paymentId, sourceLabel);
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        var previousStatus = payment.Status;
        var now = clock.UtcNow;
        var applied = payment.ApplyGatewayWebhook(gatewayTransactionId, reportedStatus, now);
        if (applied.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return applied;
        }

        if (payment.Status == previousStatus)
        {
            // Duplicate or out-of-order - a genuine no-op (edge-cases.md, both named cases).
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        var auditAction = payment.Status switch
        {
            PaymentStatus.Successful => "succeed",
            PaymentStatus.Failed => "fail",
            _ => "transition",
        };

        if (payment.Status == PaymentStatus.Successful)
        {
            var invoice = await invoices.GetByIdAsync(payment.InvoiceId, cancellationToken).ConfigureAwait(false);
            if (invoice is not null)
            {
                invoice.MarkPaid(now);
            }

            var ledgerEntry = LedgerEntry.Create(
                LedgerEntryType.PaymentPosted,
                "Payment",
                payment.Id.Value,
                payment.Amount.Amount,
                payment.Amount.Currency,
                $"Payment posted for Invoice '{payment.InvoiceId}'.",
                correlationId,
                now);
            ledgerEntries.Add(ledgerEntry);
            domainEvents.Enqueue(new LedgerEntryPosted(ledgerEntry.Id.Value, ledgerEntry.EntryType.ToString(), ledgerEntry.ReferenceType, ledgerEntry.ReferenceId, ledgerEntry.Amount, now));
        }

        var auditRequest = AuditContext.ForSystemJob(
            sourceLabel,
            correlationId,
            "Payment",
            payment.Id.Value.ToString(),
            auditAction,
            JsonSerializer.Serialize(new { status = previousStatus.ToString() }),
            JsonSerializer.Serialize(new { status = payment.Status.ToString(), gatewayTransactionId }));

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed;
        }

        if (payment.Status == PaymentStatus.Successful)
        {
            // ADR-0010's "receipt right after payment" - synchronous, but AFTER commit and
            // best-effort, the same posture Student's own Documents call established: a Documents
            // outage never rolls back or fails the Payment that already committed.
            try
            {
                var receipt = await receiptRequester.RequestReceiptAsync(new RequestReceiptCommand(payment.OwnerId, payment.Id.Value, payment.Amount.Amount, payment.Amount.Currency, now), cancellationToken).ConfigureAwait(false);

                // A caught exception isn't the only failure shape here - Documents' own Result
                // pattern returns a plain failed Result (e.g. no DocumentTemplate registered yet
                // for "Receipt") without ever throwing, so that outcome needs its own visibility,
                // not just the exception path below (a real gap this manual verification pass
                // caught: no template existed and the failure was silently swallowed).
                if (receipt.IsFailure && logger.IsEnabled(LogLevel.Warning))
                {
                    logger.LogWarning(
                        "Finance: receipt generation request for Payment {PaymentId} was rejected: {Error} - the Payment itself already committed successfully.",
                        payment.Id.Value,
                        receipt.Error);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Finance: receipt generation request failed for Payment {PaymentId} - the Payment itself already committed successfully.", payment.Id.Value);
            }
        }

        return Result.Success();
    }
}
