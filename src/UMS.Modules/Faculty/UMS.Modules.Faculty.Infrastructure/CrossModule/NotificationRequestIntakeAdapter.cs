using System.Text.Json;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Faculty.Infrastructure.CrossModule;

/// <summary>FAC-12: adapts <c>UMS.Shared.Notifications.INotificationRequestIntake</c> to Faculty's own local port - mirrors Documents' own <c>NotificationRequestIntakeAdapter</c> exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : INotificationRequestPublisher
{
    public async Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var command = new SubmitNotificationRequestCommand(
            SourceModule: "faculty",
            EventType: request.EventType,
            SourceEntityId: request.SourceEntityId,
            RecipientId: request.RecipientUserId,
            PayloadJson: JsonSerializer.Serialize(request.MergeFields));

        var result = await intake.SubmitAsync(command, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"NotificationRequest submission failed: {result.Error!.Message}");
        }
    }
}
