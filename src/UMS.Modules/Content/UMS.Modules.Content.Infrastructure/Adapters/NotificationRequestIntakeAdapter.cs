using System.Text.Json;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Content.Infrastructure.Adapters;

/// <summary>Adapts Content's own <see cref="INoticeNotificationPublisher"/> port onto Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - mirrors every other module's own adapter exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : INoticeNotificationPublisher
{
    public async Task PublishAsync(NoticeNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand("content", request.EventType, request.SourceEntityId, request.RecipientUserId, JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
