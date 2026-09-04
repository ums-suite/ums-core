using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Templates;

namespace UMS.Modules.Notifications.Application.Abstractions;

public interface ITemplateRepository
{
    public void Add(Template template);

    public Task<Template?> GetByIdAsync(TemplateId id, CancellationToken cancellationToken = default);

    public Task<Template?> GetByEventTypeAndChannelAsync(string eventType, NotificationChannel channel, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<Template>> ListAsync(int skip, int take, CancellationToken cancellationToken = default);
}
