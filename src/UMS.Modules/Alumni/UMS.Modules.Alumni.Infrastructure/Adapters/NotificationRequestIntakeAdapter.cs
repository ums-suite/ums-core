using System.Text.Json;
using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Alumni.Infrastructure.Adapters;

/// <summary>Adapts Alumni's own <see cref="IAlumniNotificationPublisher"/> port onto Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - mirrors every other module's own adapter exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : IAlumniNotificationPublisher
{
    public async Task PublishAsync(AlumniNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand("alumni", request.EventType, request.SourceEntityId, request.RecipientId, JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
