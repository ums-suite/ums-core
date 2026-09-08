using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Finance.Application.Abstractions;
using UMS.Modules.Finance.Application.Common;
using UMS.Modules.Finance.Domain.Payments;
using UMS.Modules.Finance.Domain.Reconciliation;
using UMS.Shared.Audit;

namespace UMS.Modules.Finance.Application.Reconciliation;

/// <summary>
/// FIN-14/ADR-0014: the daily reconciliation job - compares Finance's own <see cref="PaymentTransaction"/>
/// records against the (fake) gateway's settlement report for one calendar day.
/// <c>Successful -&gt; Reconciled</c> on a match; a mismatch produces a <see cref="ReconciliationException"/>
/// for manual Accountant review - never a silent automatic balance correction (requirement-spec.md
/// §8; ADR-0008).
///
/// <para>
/// Mirrors design-decisions.md's "Reconciliation Job Concurrency-Safety" mechanism exactly, one
/// aggregate-root generation after <see cref="StuckPaymentSweepService"/>'s own identical shape
/// (edge-cases.md "Daily Reconciliation Job Racing a Live In-Flight Payment" is the same race class
/// "Stuck-Pending Polling Job Racing a Webhook" already solved, applied to a scheduled batch job
/// instead of a polling one): an unlocked candidate scan already excluding anything updated within
/// the buffer window, followed by a fresh, row-locked re-read for whatever it actually decides to
/// mutate - never mutating the candidate row read outside a lock directly.
/// </para>
/// </summary>
public sealed class ReconciliationService(
    IPaymentRepository payments,
    IReconciliationExceptionRepository reconciliationExceptions,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock,
    ILogger<ReconciliationService> logger)
{
    /// <summary>design-decisions.md "Reconciliation Job Concurrency-Safety": "excluding anything updated within the last 30 minutes from that day's run" - an engineering default, not BRD-specified (edge-cases.md's own residual note).</summary>
    private static readonly TimeSpan BufferWindow = TimeSpan.FromMinutes(30);

    private enum ReconciliationOutcome
    {
        Skipped,
        Reconciled,
        Flagged,
    }

    public async Task<ReconciliationRunSummary> RunAsync(DateOnly settlementDate, int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var candidates = await payments.GetSuccessfulUnreconciledUpdatedBeforeAsync(now - BufferWindow, batchSize, cancellationToken).ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return new ReconciliationRunSummary(0, 0, 0);
        }

        IReadOnlyList<GatewaySettlementRecord> settlement;
        try
        {
            settlement = await gateway.GetSettlementReportAsync(settlementDate, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            logger.LogWarning(ex, "Finance reconciliation: settlement report fetch failed for {SettlementDate} - will retry next run.", settlementDate);
            return new ReconciliationRunSummary(candidates.Count, 0, 0);
        }

        var byMerchantTransactionId = settlement.ToDictionary(s => s.MerchantTransactionId, StringComparer.Ordinal);

        var reconciled = 0;
        var flagged = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                var outcome = await ProcessOneAsync(candidate.Id.Value, settlementDate, byMerchantTransactionId, cancellationToken).ConfigureAwait(false);
                switch (outcome)
                {
                    case ReconciliationOutcome.Reconciled:
                        reconciled++;
                        break;
                    case ReconciliationOutcome.Flagged:
                        flagged++;
                        break;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Finance reconciliation: unexpected failure processing Payment {PaymentId}.", candidate.Id.Value);
            }
        }

        return new ReconciliationRunSummary(candidates.Count, reconciled, flagged);
    }

    private static ReconciliationMismatchReason? DetermineMismatch(string? expectedGatewayTransactionId, GatewaySettlementRecord? record)
    {
        if (record is null)
        {
            return ReconciliationMismatchReason.NoSettlementRecordFound;
        }

        if (!string.Equals(record.GatewayTransactionId, expectedGatewayTransactionId, StringComparison.Ordinal))
        {
            return ReconciliationMismatchReason.GatewayTransactionIdMismatch;
        }

        return record.Status == PaymentStatus.Successful ? null : ReconciliationMismatchReason.GatewayStatusDisagreement;
    }

    private static string BuildMismatchDetails(ReconciliationMismatchReason reason, string? expected, GatewaySettlementRecord? record) => reason switch
    {
        ReconciliationMismatchReason.NoSettlementRecordFound => "The gateway's settlement report for this date has no record of this transaction.",
        ReconciliationMismatchReason.GatewayTransactionIdMismatch => $"Finance recorded gatewayTransactionId '{expected}' but the settlement report reports '{record?.GatewayTransactionId}' for this attempt.",
        ReconciliationMismatchReason.GatewayStatusDisagreement => $"Finance recorded this PaymentTransaction as Successful but the gateway's settlement report reports '{record?.Status}'.",
        _ => "Unrecognized reconciliation mismatch.",
    };

    private async Task<ReconciliationOutcome> ProcessOneAsync(Guid paymentId, DateOnly settlementDate, Dictionary<string, GatewaySettlementRecord> settlement, CancellationToken cancellationToken)
    {
        var correlationId = $"reconciliation-job:{Guid.NewGuid()}";

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var payment = await payments.GetByIdForUpdateAsync(new PaymentId(paymentId), cancellationToken).ConfigureAwait(false);
        if (payment is null || payment.Status != PaymentStatus.Successful)
        {
            // edge-cases.md "Daily Reconciliation Job Racing a Live In-Flight Payment": something
            // else already moved this Payment on (or it no longer exists) since the unlocked
            // candidate scan read it - re-checked under the lock, exactly the residual window
            // design-decisions.md's own mechanism accepts.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return ReconciliationOutcome.Skipped;
        }

        var expectedGatewayTransactionId = payment.CurrentTransaction.GatewayTransactionId;
        settlement.TryGetValue(payment.Id.Value.ToString(), out var record);

        var mismatchReason = DetermineMismatch(expectedGatewayTransactionId, record);
        var now = clock.UtcNow;

        if (mismatchReason is null)
        {
            var marked = payment.MarkReconciled(now);
            if (marked.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return ReconciliationOutcome.Skipped;
            }

            var auditRequest = AuditContext.ForSystemJob(
                "reconciliation-job",
                correlationId,
                "Payment",
                payment.Id.Value.ToString(),
                "reconcile",
                JsonSerializer.Serialize(new { status = "Successful" }),
                JsonSerializer.Serialize(new { status = "Reconciled", settlementDate = settlementDate.ToString("O", CultureInfo.InvariantCulture) }));

            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            return committed.IsSuccess ? ReconciliationOutcome.Reconciled : ReconciliationOutcome.Skipped;
        }

        // requirement-spec.md §8 / ADR-0008: a mismatch NEVER silently auto-corrects a balance - it
        // produces a ReconciliationException for manual Accountant review instead.
        var exception = ReconciliationException.Create(
            payment.Id,
            payment.CurrentTransaction.Id,
            settlementDate,
            mismatchReason.Value,
            expectedGatewayTransactionId,
            record?.GatewayTransactionId,
            record?.Status.ToString(),
            BuildMismatchDetails(mismatchReason.Value, expectedGatewayTransactionId, record),
            now);
        reconciliationExceptions.Add(exception);

        var flaggedAuditRequest = AuditContext.ForSystemJob(
            "reconciliation-job",
            correlationId,
            "Payment",
            payment.Id.Value.ToString(),
            "reconciliation_exception",
            JsonSerializer.Serialize(new { status = "Successful", expectedGatewayTransactionId }),
            JsonSerializer.Serialize(new { reason = mismatchReason.Value.ToString(), actualStatus = record?.Status.ToString(), actualGatewayTransactionId = record?.GatewayTransactionId }));

        var flaggedCommitted = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, flaggedAuditRequest, cancellationToken).ConfigureAwait(false);
        return flaggedCommitted.IsSuccess ? ReconciliationOutcome.Flagged : ReconciliationOutcome.Skipped;
    }
}

public sealed record ReconciliationRunSummary(int Candidates, int Reconciled, int Flagged);
