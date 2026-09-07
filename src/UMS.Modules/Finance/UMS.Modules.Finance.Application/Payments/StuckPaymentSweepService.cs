using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Common;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Finance.Application.Payments;

/// <summary>
/// FIN-9/FIN-10: the stuck-payment background sweep. Two edge cases share one candidate scan
/// (requirement-spec.md §8): a Payment stuck <see cref="PaymentStatus.Pending"/> with no webhook
/// ever arriving ("Webhook never arrives"), and a Payment stuck <see cref="PaymentStatus.Initiated"/>
/// with the gateway call itself never having landed ("Gateway outage during POST /payments").
///
/// <para>
/// Mirrors design-decisions.md's "Reconciliation Job Concurrency-Safety" mechanism exactly, one
/// aggregate root earlier in this build's own Master Sequence than the daily reconciliation job
/// itself (Flow #18): an unlocked candidate scan (<see cref="IPaymentRepository.GetNonTerminalUpdatedBeforeAsync"/>)
/// followed by a fresh, row-locked re-read for whatever it actually decides to mutate - never
/// mutating the candidate row read outside a lock directly.
/// </para>
/// </summary>
public sealed class StuckPaymentSweepService(
    IPaymentRepository payments,
    IPaymentGateway gateway,
    PaymentWebhookService webhookService,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<StuckPaymentSweepService> logger)
{
    // FIN-9: how long a Pending Payment (or an Initiated one WITH a gateway record) may go without
    // a webhook before this sweep proactively queries the gateway itself.
    private static readonly TimeSpan PollThreshold = TimeSpan.FromMinutes(10);

    // FIN-10: how long an Initiated Payment with NO gateway record at all (the gateway call itself
    // never landed) may persist before it is given up on outright - deliberately longer than
    // PollThreshold since "the gateway has genuinely never heard of this attempt" is a stronger,
    // rarer signal than "no webhook yet", and a longer grace window costs nothing since an
    // Initiated Payment with no session reference can never be double-charged.
    private static readonly TimeSpan InitiatedStaleThreshold = TimeSpan.FromMinutes(30);

    public async Task<int> SweepAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var candidates = await payments.GetNonTerminalUpdatedBeforeAsync(now - PollThreshold, batchSize, cancellationToken).ConfigureAwait(false);

        var processed = 0;
        foreach (var payment in candidates)
        {
            try
            {
                if (await ProcessOneAsync(payment.Id.Value, payment.Status, payment.UpdatedAt, now, cancellationToken).ConfigureAwait(false))
                {
                    processed++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Finance stuck-payment sweep: unexpected failure processing Payment {PaymentId}.", payment.Id.Value);
            }
        }

        return processed;
    }

    private async Task<bool> ProcessOneAsync(Guid paymentId, PaymentStatus candidateStatus, DateTimeOffset candidateUpdatedAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var correlationId = $"stuck-payment-sweep:{Guid.NewGuid()}";

        GatewayStatusQueryResult? queried;
        try
        {
            queried = await gateway.QueryStatusAsync(paymentId.ToString(), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            logger.LogWarning(ex, "Finance stuck-payment sweep: gateway status query failed for Payment {PaymentId} - will retry next pass.", paymentId);
            return false;
        }

        if (queried is not null)
        {
            // Routes through the IDENTICAL idempotent, ordering-guarded transition function the
            // live webhook handler uses (design-decisions.md; edge-cases.md "Stuck-Pending Polling
            // Job Racing a Webhook That Arrives Mid-Poll") - whichever of the two commits first
            // wins, the second is a safe no-op, with no separate locking scheme needed here.
            var result = await webhookService.ApplyAsync(paymentId, queried.GatewayTransactionId, queried.Status, correlationId, "stuck-payment-sweep", cancellationToken).ConfigureAwait(false);
            return result.IsSuccess;
        }

        if (candidateStatus != PaymentStatus.Initiated || now - candidateUpdatedAt < InitiatedStaleThreshold)
        {
            return false;
        }

        return await MarkStaleUnderLockAsync(paymentId, correlationId, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> MarkStaleUnderLockAsync(Guid paymentId, string correlationId, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var payment = await payments.GetByIdForUpdateAsync(new PaymentId(paymentId), cancellationToken).ConfigureAwait(false);
        if (payment is null || payment.Status != PaymentStatus.Initiated)
        {
            // A webhook (or a concurrent sweep pass) already moved this Payment forward since the
            // unlocked candidate scan read it - re-checked under the lock, exactly the residual
            // window design-decisions.md's reconciliation-job analog accepts.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var now = clock.UtcNow;
        var marked = payment.MarkStale("stale_initiated_timeout", now);
        if (marked.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return false;
        }

        var auditRequest = AuditContext.ForSystemJob(
            "stuck-payment-sweep",
            correlationId,
            "Payment",
            payment.Id.Value.ToString(),
            "fail",
            JsonSerializer.Serialize(new { status = "Initiated" }),
            JsonSerializer.Serialize(new { status = "Failed", reason = "stale_initiated_timeout" }));

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsSuccess;
    }
}
