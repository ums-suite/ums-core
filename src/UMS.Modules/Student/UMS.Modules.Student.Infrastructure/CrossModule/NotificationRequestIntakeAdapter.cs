using System.Text.Json;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Student.Infrastructure.CrossModule;

/// <summary>STU-4: adapts <c>UMS.Shared.Notifications.INotificationRequestIntake</c> to Student's own local port - mirrors Documents'/Faculty's own <c>NotificationRequestIntakeAdapter</c> exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : INotificationRequestPublisher
{
    public async Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var command = new SubmitNotificationRequestCommand(
            SourceModule: "student",
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
