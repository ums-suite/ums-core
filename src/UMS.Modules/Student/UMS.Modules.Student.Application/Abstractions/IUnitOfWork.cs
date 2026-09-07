using System.Data.Common;

namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>Commits one transaction's worth of repository changes. Mirrors <c>UMS.Modules.Faculty.Application.Abstractions.IUnitOfWork</c> exactly.</summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class;

    /// <summary>
    /// STU-15: forces an already-tracked entity back to <c>Unchanged</c>, discarding any pending
    /// in-memory mutation - used ONLY after catching a <see cref="ConcurrencyConflictException"/> on
    /// a bulk-import UPDATE row's own <see cref="SaveChangesAsync"/> call, so the failed row's
    /// rejected edit (and its now-stale <see cref="SetExpectedVersion{TEntity}"/> baseline) can never
    /// be silently re-attempted by a LATER <see cref="SaveChangesAsync"/> call in the same shared
    /// worker scope (<c>StudentBulkImportProcessingService</c>'s own remarks) - without this, that
    /// later, unrelated bookkeeping save would itself throw the same concurrency exception and abort
    /// the rest of the batch.
    /// </summary>
    public void DiscardChanges<TEntity>(TEntity entity)
        where TEntity : class;
}

public interface IUmsTransaction : IAsyncDisposable
{
    public DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
