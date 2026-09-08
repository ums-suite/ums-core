namespace UMS.Modules.Alumni.Application.Abstractions;

public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>design-decisions.md "Job-Posting Moderation-Queue Concurrency Control": forces an xmin-checked UPDATE even when only an owned child collection changed (a known EF Core gotcha this codebase already documents in Research's own DbContext) - applied uniformly to every optimistic-concurrency write in this module.</summary>
    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class;
}

public interface IUmsTransaction : IAsyncDisposable
{
    public System.Data.Common.DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
