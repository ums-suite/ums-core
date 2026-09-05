using System.Data.Common;

namespace UMS.Modules.Academic.Application.Abstractions;

/// <summary>Commits one transaction's worth of repository changes. Mirrors <c>UMS.Modules.Faculty/Student.Application.Abstractions.IUnitOfWork</c> exactly.</summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class;
}

public interface IUmsTransaction : IAsyncDisposable
{
    public DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
