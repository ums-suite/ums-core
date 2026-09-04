namespace UMS.Modules.Audit.Application.Abstractions;

/// <summary>Commits Audit's own DI-scoped <c>AuditDbContext</c> changes - used by the export job-tracking write path (AUD-9), never by <c>RecordEntry</c>'s cross-module path, which binds its own short-lived context directly to the caller's transaction (see <c>UMS.Shared.Audit.IAuditRecorder</c>'s remarks).</summary>
public interface IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
