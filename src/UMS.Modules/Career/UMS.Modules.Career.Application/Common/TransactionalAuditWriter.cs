using UMS.Modules.Career.Application.Abstractions;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Career.Application.Common;

/// <summary>ADR-0012's write-path transaction-coupling mechanism - mirrors every other module's own `TransactionalAuditWriter` exactly.</summary>
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

    /// <summary>A commit with NO audit write - reserved for non-sensitive mutations/scheduled-sweep ticks. Every AUDITED mutation still goes through <see cref="CommitWithAuditAsync"/>.</summary>
    public static async Task<Result> CommitWithoutAuditAsync(IUnitOfWork unitOfWork, IUmsTransaction transaction, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
