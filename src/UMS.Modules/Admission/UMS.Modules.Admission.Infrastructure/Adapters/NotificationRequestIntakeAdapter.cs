using System.Text.Json;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Admission.Infrastructure.Adapters;

/// <summary>Adapts this module's own <see cref="INotificationRequestPublisher"/> port onto Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - mirrors Finance's own <c>NotificationRequestIntakeAdapter</c> exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : INotificationRequestPublisher
{
    public async Task PublishAsync(AdmissionNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand("admission", request.EventType, request.SourceEntityId, request.RecipientUserId, JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
