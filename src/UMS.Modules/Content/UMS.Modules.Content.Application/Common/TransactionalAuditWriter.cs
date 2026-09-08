using UMS.Modules.Content.Application.Abstractions;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Application.Common;

/// <summary>ADR-0012's write-path transaction-coupling mechanism - mirrors every other module's own <c>TransactionalAuditWriter</c> exactly.</summary>
public static class TransactionalAuditWriter
{
    public static async Task<Result> CommitWithAuditAsync(
        IUnitOfWork unitOfWork,
        IAuditRecorder auditRecorder,
        IUmsTransaction transaction,
        RecordAuditEntryRequest auditRequest,
        CancellationToken cancellationToken)
    {
        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return auditResult;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateValueException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.Conflict($"{ex.EntityType.ToLowerInvariant()}.duplicate_value", ex.Message));
        }
        catch (ConcurrencyConflictException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.Conflict($"{ex.EntityType.ToLowerInvariant()}.concurrency_conflict", ex.Message));
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    /// <summary>
    /// A commit with NO audit write - used by the scheduled scheduling-sweep services (CNT-4/CNT-9)
    /// for the ordinary (non-publish/archive-worthy... actually every transition IS audited) case is
    /// intentionally NOT provided: design-decisions.md's Audit-Write Synchronicity decision applies
    /// to publish/archive/edit-after-publish unconditionally, including when the actor is the
    /// scheduled job itself (<see cref="AuditContext.ForSystemJob"/>) - so every scheduling-service
    /// commit goes through <see cref="CommitWithAuditAsync"/> too, never this shortcut.
    /// </summary>
    public static async Task<Result> CommitWithoutAuditAsync(
        IUnitOfWork unitOfWork,
        IUmsTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            // design-decisions.md "Scheduled-Publish Job Exactly-Once Execution Mechanism": a
            // concurrent writer won the race first - this tick simply has nothing left to do.
            // Never retried, never escalated - harmless by construction.
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
