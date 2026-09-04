using System.Data.Common;

namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>Commits Documents' own DI-scoped <c>DocumentsDbContext</c> changes - mirrors Audit's own local <c>IUnitOfWork</c>.</summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens an explicit transaction so its <see cref="DbTransaction"/> can be handed to
    /// <c>UMS.Shared.Audit.IAuditRecorder</c> (DOC-14, ADR-0012) - mirrors Identity's own
    /// <c>IUnitOfWork.BeginTransactionAsync</c> (see <c>UserStatusService</c>'s remarks for why an
    /// explicit transaction, rather than <see cref="SaveChangesAsync"/>'s own implicit one, is
    /// required to hand a shareable <see cref="DbTransaction"/> to the audit recorder).
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
