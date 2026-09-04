using System.Data.Common;

namespace UMS.Modules.Identity.Application.Abstractions;

/// <summary>
/// Commits one transaction's worth of repository changes. Infrastructure's implementation also
/// drains every tracked aggregate's <c>DomainEvents</c> into the transactional outbox as part of
/// the same <c>SaveChanges</c> call (ADR-0003) - the Application layer never has to remember to do
/// that itself.
/// </summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit transaction on this module's own connection, so its
    /// <see cref="DbTransaction"/> can be handed to <c>UMS.Shared.Audit.IAuditRecorder</c>
    /// (AUD-1) - the mechanism that makes a business mutation and its audit entry commit
    /// together or not at all (ADR-0012). Calling <see cref="SaveChangesAsync"/> while a
    /// transaction opened this way is still active participates in it rather than opening (and
    /// auto-committing) a second one.
    /// </summary>
    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>A transaction begun via <see cref="IUnitOfWork.BeginTransactionAsync"/> - the caller commits or rolls back explicitly, then disposes.</summary>
public interface IUmsTransaction : IAsyncDisposable
{
    public DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
