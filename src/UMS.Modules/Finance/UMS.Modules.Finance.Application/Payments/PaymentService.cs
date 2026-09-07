using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Domain.Invoices;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.Payments;

/// <summary>
/// FIN-6/FIN-7: <c>POST /payments</c> and its ownership-scoped read.
///
/// <para>
/// <b>Idempotency-key replay, including an in-place retry.</b> design-decisions.md
/// "Idempotency-Key + Gateway-Transaction-ID Enforcement Mechanism": a key is checked, permanently,
/// before the gateway call. This build refines the "replay the stored Payment's state" behavior one
/// step further for the still-<see cref="PaymentStatus.Initiated"/> case specifically (no gateway
/// session ever recorded, so nothing has committed at the gateway yet): rather than only replaying
/// stale state, the SAME Payment/PaymentTransaction retries the gateway call in place - directly
/// serving edge-cases.md's "Gateway outage during POST /payments" ("the call fails fast with a
/// retryable error") without abandoning the caller's own Idempotency-Key on the very first transient
/// failure. Once a gateway session IS recorded (Pending or later), replay is purely informational -
/// exactly design-decisions.md's original "replay the stored state" behavior.
/// </para>
/// </summary>
public sealed class PaymentService(
    IInvoiceRepository invoices,
    IPaymentRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<InitiatePaymentResult>> InitiateAsync(Guid callerUserId, InitiatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Error.Validation("payment.idempotency_key_required", "The Idempotency-Key header is required.");
        }

        var existingByKey = await payments.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
        if (existingByKey is not null)
        {
            if (existingByKey.OwnerId != callerUserId)
            {
                return Error.Forbidden("payment.not_owner", "You do not own the Payment for this Idempotency-Key.");
            }

            return existingByKey.Status == PaymentStatus.Initiated
                ? await RetryGatewayCallAsync(existingByKey, cancellationToken).ConfigureAwait(false)
                : new InitiatePaymentResult(ToDto(existingByKey), RedirectUrl: null);
        }

        // design-decisions.md "Invoice-Level Concurrency Control for Payment Initiation": the
        // pessimistic row lock GetByIdForUpdateAsync takes is only real for as long as it is held
        // inside an explicit transaction spanning the whole read-decide-insert sequence - a bare
        // FromSqlInterpolated(...FOR UPDATE) with no ambient transaction releases the lock the
        // instant that one SELECT completes, which would make the lock a no-op against exactly the
        // race (edge-cases.md "Two Concurrent Payment Attempts Against the Same Invoice") it exists
        // to close.
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var invoice = await invoices.GetByIdForUpdateAsync(new InvoiceId(request.InvoiceId), cancellationToken).ConfigureAwait(false);
        if (invoice is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("payment.invoice_not_found", $"No Invoice exists with id '{request.InvoiceId}'.");
        }

        if (invoice.OwnerId != callerUserId)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Forbidden("payment.not_owner", "You do not own this Invoice.");
        }

        if (!invoice.HasOutstandingBalance)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("payment.invoice_already_satisfied", $"Invoice '{invoice.Id}' has no outstanding balance.");
        }

        // Rejected outright, under the same row lock, not merely deduplicated after the fact.
        var nonTerminal = await payments.GetNonTerminalByInvoiceAsync(invoice.Id.Value, cancellationToken).ConfigureAwait(false);
        if (nonTerminal is not null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.Conflict("payment.attempt_already_in_progress", $"A Payment attempt is already in progress for Invoice '{invoice.Id}'.");
        }

        var now = clock.UtcNow;
        var created = Payment.Initiate(invoice, callerUserId, request.IdempotencyKey, gateway.Name, now);
        if (created.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return created.Error!;
        }

        var payment = created.Value;
        payments.Add(payment);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException)
        {
            // edge-cases.md's "Webhook Arrives After a Browser-Side Timeout" family: a genuinely
            // concurrent request with the SAME key raced this one to the unique constraint. Replay
            // the winner's state rather than surfacing a raw constraint error.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            var winner = await payments.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken).ConfigureAwait(false);
            return winner is null
                ? Error.Failure("payment.creation_race_unresolved", "A concurrent Payment creation conflict could not be resolved.")
                : new InitiatePaymentResult(ToDto(winner), RedirectUrl: null);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return await RetryGatewayCallAsync(payment, cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result<PaymentDto>> GetByIdAsync(Guid id, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var payment = await payments.GetByIdAsync(new PaymentId(id), cancellationToken).ConfigureAwait(false);
        if (payment is null)
        {
            return Error.NotFound("payment.not_found", $"No Payment exists with id '{id}'.");
        }

        return payment.OwnerId != callerUserId
            ? Error.Forbidden("payment.not_owner", "You do not own this Payment.")
            : ToDto(payment);
    }

    internal static PaymentDto ToDto(Payment payment) => new(
        payment.Id.Value,
        payment.InvoiceId.Value,
        payment.OwnerId,
        payment.Amount.Amount,
        payment.Amount.Currency,
        payment.Status.ToString(),
        payment.CurrentTransaction.GatewayName,
        payment.CreatedAt,
        payment.UpdatedAt);

    /// <summary>requirement-spec.md §5: "the initiation call itself must still return promptly" - a gateway failure here fails fast with a retryable error, leaving the Payment in Initiated rather than blocking on retries of its own (edge-cases.md "Gateway outage during POST /payments").</summary>
    private async Task<Result<InitiatePaymentResult>> RetryGatewayCallAsync(Payment payment, CancellationToken cancellationToken)
    {
        GatewayInitiationResult gatewayResult;
        try
        {
            gatewayResult = await gateway.InitiateAsync(
                new GatewayInitiationRequest(payment.Id.Value.ToString(), payment.Amount.Amount, payment.Amount.Currency, payment.OwnerId, payment.SourceModule),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            return Error.Failure("payment.gateway_unavailable", $"The payment gateway is currently unavailable ({ex.Message}) - retry with the same Idempotency-Key.");
        }

        payment.RecordGatewaySessionReference(gatewayResult.SessionReference, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new InitiatePaymentResult(ToDto(payment), gatewayResult.RedirectUrl);
    }
}
