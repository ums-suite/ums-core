namespace UMS.Modules.Career.Application.Abstractions;

public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Forces an xmin-checked UPDATE even when only an owned/child collection changed - mirrors every other module's own uniform optimistic-concurrency mechanism exactly.</summary>
    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class;
}

public interface IUmsTransaction : IAsyncDisposable
{
    public System.Data.Common.DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
