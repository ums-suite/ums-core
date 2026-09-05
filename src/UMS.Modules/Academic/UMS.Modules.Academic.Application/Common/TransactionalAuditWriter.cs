using UMS.Modules.Academic.Application.Abstractions;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Academic.Application.Common;

/// <summary>
/// design-decisions.md's synchronous-Audit-integration posture (ADR-0012) applied to every one of
/// Academic's own mutation call sites: record-then-save-then-commit, both-or-neither via one
/// shared transaction. Mirrors Faculty/Student's own <c>TransactionalAuditWriter</c> exactly.
/// </summary>
internal static class TransactionalAuditWriter
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
            return Result.Failure(Error.Conflict($"{ex.EntityType.ToLowerInvariant()}.version_conflict", ex.Message));
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
