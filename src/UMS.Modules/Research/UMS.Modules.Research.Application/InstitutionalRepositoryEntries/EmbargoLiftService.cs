using System.Text.Json;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Common;
using UMS.Shared.Audit;

namespace UMS.Modules.Research.Application.InstitutionalRepositoryEntries;

/// <summary>
/// RES-12: design-decisions.md "InstitutionalRepositoryEntry Embargo-Lift Mechanism" - the daily
/// ADR-0014 scheduled worker's own automatic sweep target. Called by
/// <c>ResearchEmbargoLiftSweepWorker</c>; deliberately no distributed lock/lease (mirrors Content's
/// own scheduled-publish sweep) - safe to run from multiple concurrent <c>UMS.Workers</c> replicas
/// because <see cref="Domain.InstitutionalRepositoryEntries.InstitutionalRepositoryEntry.LiftEmbargo"/>
/// is a no-op once already lifted, so a redundant concurrent tick is harmless.
/// </summary>
public sealed class EmbargoLiftService(IInstitutionalRepositoryEntryRepository entries, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, IClock clock)
{
    public async Task<int> LiftLapsedEmbargoesAsync(int batchSize, string correlationId, CancellationToken cancellationToken = default)
    {
        var lapsed = await entries.ListLapsedEmbargoesAsync(DateOnly.FromDateTime(clock.UtcNow.UtcDateTime), batchSize, cancellationToken).ConfigureAwait(false);
        var liftedCount = 0;

        foreach (var entry in lapsed)
        {
            entry.LiftEmbargo(clock.UtcNow);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(
                "research-embargo-lift-sweep",
                correlationId,
                "InstitutionalRepositoryEntry",
                entry.Id.Value.ToString(),
                "lift_embargo",
                JsonSerializer.Serialize(new { isEmbargoed = true, embargoEndDate = entry.Embargo.EmbargoEndDate }),
                JsonSerializer.Serialize(new { isEmbargoed = false }));

            var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (commitResult.IsSuccess)
            {
                liftedCount++;
            }
        }

        return liftedCount;
    }
}
