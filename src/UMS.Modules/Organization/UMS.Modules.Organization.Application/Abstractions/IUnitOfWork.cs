using System.Data.Common;

namespace UMS.Modules.Organization.Application.Abstractions;

/// <summary>
/// Commits one transaction's worth of repository changes. Mirrors
/// <c>UMS.Modules.Identity.Application.Abstractions.IUnitOfWork</c> exactly - see
/// <see cref="IUmsTransaction"/>'s own remarks for why an explicit, shareable transaction (rather
/// than <see cref="SaveChangesAsync"/>'s own implicit one) is required to hand a
/// <see cref="DbTransaction"/> to <c>UMS.Shared.Audit.IAuditRecorder</c>.
/// </summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// design-decisions.md, "Optimistic Concurrency (Version Column) for Hierarchy Edits": tells
    /// the change tracker that <paramref name="entity"/>'s concurrency token, at the moment the
    /// client read it, was <paramref name="expectedVersion"/> - not whatever value a fresh load
    /// just populated. <see cref="SaveChangesAsync"/>'s generated `UPDATE ... WHERE xmin = ...`
    /// then only matches if the row is still at that version, throwing
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/> (translated to a
    /// `409 Conflict` by the calling Application service) the instant someone else's write landed
    /// first - this is what makes a stale `PATCH` fail loudly instead of silently overwriting.
    /// </summary>
    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class;
}

/// <summary>A transaction begun via <see cref="IUnitOfWork.BeginTransactionAsync"/> - the caller commits or rolls back explicitly, then disposes.</summary>
public interface IUmsTransaction : IAsyncDisposable
{
    public DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
