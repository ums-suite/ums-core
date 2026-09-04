using Microsoft.EntityFrameworkCore;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Modules.Audit.Infrastructure.Persistence.Repositories;

internal sealed class AuditExportRequestRepository(AuditDbContext context) : IAuditExportRequestRepository
{
    public void Add(AuditExportRequest request) => context.ExportRequests.Add(request);

    public Task<AuditExportRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ExportRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
}
