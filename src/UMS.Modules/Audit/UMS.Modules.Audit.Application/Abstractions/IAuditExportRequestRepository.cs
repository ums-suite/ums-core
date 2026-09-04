using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Modules.Audit.Application.Abstractions;

public interface IAuditExportRequestRepository
{
    public void Add(AuditExportRequest request);

    public Task<AuditExportRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
}
