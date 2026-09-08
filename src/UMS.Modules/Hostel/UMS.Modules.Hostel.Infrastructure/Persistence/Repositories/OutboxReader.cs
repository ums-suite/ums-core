using Microsoft.EntityFrameworkCore;
using UMS.Modules.Hostel.Application.Abstractions;

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Repositories;

internal sealed class OutboxReader(HostelDbContext context) : IOutboxReader
{
    public async Task<IReadOnlyList<OutboxMessageDto>> GetUnprocessedAsync(string eventType, int batchSize, CancellationToken cancellationToken = default) =>
        await context.OutboxMessages
            .Where(m => m.EventType == eventType && m.ProcessedAt == null)
            .OrderBy(m => m.RecordedAt)
            .Take(batchSize)
            .Select(m => new OutboxMessageDto(m.Id, m.EventType, m.PayloadJson, m.AttemptCount))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task MarkProcessedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);
        message?.MarkProcessed(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailedAttemptAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var message = await context.OutboxMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken).ConfigureAwait(false);
        message?.RecordFailedAttempt(error);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
