using UMS.Modules.Organization.Application.Abstractions;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Organization.Application.Common;

/// <summary>
/// requirement-spec.md organization §5 Auditability: "every structural mutation (create/
/// deactivate/rename at every level) is audit-logged" - every one of Organization's ~15 mutation
/// call sites needs the exact same four-step sequence Identity's own `UserStatusService`
/// established (record the audit entry, save the business change, commit, both-or-neither via one
/// shared transaction). Centralized here once, rather than copy-pasted at every call site, so the
/// sequence (and its two failure-translation paths) can never drift between entities.
/// </summary>
internal static class TransactionalAuditWriter
{
    /// <summary>
    /// Records <paramref name="auditRequest"/> in <paramref name="transaction"/>, then saves the
    /// already-applied in-memory business mutation through <paramref name="unitOfWork"/>, then
    /// commits - rolling back on any failure so a rejected audit write or a rejected business
    /// write (a `(parent_id, name)` uniqueness violation, or an optimistic-concurrency version
    /// mismatch) never leaves the other half committed alone (ADR-0012).
    /// </summary>
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
        catch (DuplicateNameException ex)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.Conflict($"{ex.EntityType.ToLowerInvariant()}.duplicate_name", ex.Message));
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
