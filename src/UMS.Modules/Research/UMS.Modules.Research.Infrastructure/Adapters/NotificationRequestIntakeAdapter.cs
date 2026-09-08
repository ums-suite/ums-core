using System.Text.Json;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Research.Infrastructure.Adapters;

/// <summary>Adapts Research's own <see cref="IResearchNotificationPublisher"/> port onto Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - mirrors every other module's own adapter exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : IResearchNotificationPublisher
{
    public async Task PublishAsync(ResearchNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand("research", request.EventType, request.SourceEntityId, request.RecipientId, JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
