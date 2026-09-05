using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

internal sealed class OutboxReader(AcademicDbContext context) : IOutboxReader
{
    public Task<int> CountUnprocessedAsync(CancellationToken cancellationToken = default) =>
        context.OutboxMessages.CountAsync(m => m.ProcessedAt == null, cancellationToken);
}
