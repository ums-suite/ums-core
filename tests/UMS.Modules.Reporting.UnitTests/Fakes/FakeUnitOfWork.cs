using UMS.Modules.Reporting.Application.Abstractions;

namespace UMS.Modules.Reporting.UnitTests.Fakes;

/// <summary>A no-op unit of work - sufficient for every unit-tested service that never opens a transaction (<c>MetricRefreshJobBase</c>, <c>RegulatoryReportRunExecutionService</c>). Anything exercising <see cref="BeginTransactionAsync"/> belongs in the integration suite against a real Postgres transaction.</summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("FakeUnitOfWork does not support transactions - use the integration suite for audit-coupled services.");

    public void SetExpectedVersion<TEntity>(TEntity entity, uint expectedVersion)
        where TEntity : class
    {
    }
}
