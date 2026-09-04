using System.Data.Common;

namespace UMS.Modules.Notifications.Application.Abstractions;

/// <summary>Mirrors Identity's/Audit's own <c>IUnitOfWork</c> exactly (ums-conventions.md pattern reuse).</summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    public Task<IUmsTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>EF-agnostic transaction port - see Identity's <c>IdentityDbContext.EfUmsTransaction</c> for the concrete adapter this mirrors, needed so <see cref="UMS.Shared.Audit.IAuditRecorder"/> can share the exact same physical transaction (ADR-0012) for NTF-17's audit call sites.</summary>
public interface IUmsTransaction : IAsyncDisposable
{
    public DbTransaction DbTransaction { get; }

    public Task CommitAsync(CancellationToken cancellationToken = default);

    public Task RollbackAsync(CancellationToken cancellationToken = default);
}
