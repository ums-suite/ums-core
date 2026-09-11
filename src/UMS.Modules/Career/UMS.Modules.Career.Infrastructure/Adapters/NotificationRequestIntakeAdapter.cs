using System.Text.Json;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Career.Infrastructure.Adapters;

/// <summary>Adapts Career's own <see cref="ICareerNotificationPublisher"/> port onto Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - mirrors every other module's own adapter exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : ICareerNotificationPublisher
{
    public async Task PublishAsync(CareerNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand("career", request.EventType, request.SourceEntityId, request.RecipientId, JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
