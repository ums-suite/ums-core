using Microsoft.EntityFrameworkCore;
using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Templates;

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class TemplateRepository(NotificationsDbContext context) : ITemplateRepository
{
    public void Add(Template template) => context.Templates.Add(template);

    public Task<Template?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default) =>
        context.Templates.Include(t => t.Translations).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<Template?> GetByEventTypeAndChannelAsync(string eventType, NotificationChannel channel, CancellationToken cancellationToken = default) =>
        context.Templates
            .Include(t => t.Translations)
            .FirstOrDefaultAsync(t => t.EventType == eventType && t.Channel == channel && t.IsActive, cancellationToken);

    public async Task<IReadOnlyList<Template>> ListAsync(int skip, int take, CancellationToken cancellationToken = default) =>
        await context.Templates
            .Include(t => t.Translations)
            .OrderBy(t => t.EventType).ThenBy(t => t.Channel)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
