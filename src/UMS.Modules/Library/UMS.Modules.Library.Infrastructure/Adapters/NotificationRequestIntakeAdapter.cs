using System.Text.Json;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Library.Infrastructure.Adapters;

/// <summary>Adapts this module's own <see cref="INotificationRequestPublisher"/> port onto Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - mirrors every other module's own adapter exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : INotificationRequestPublisher
{
    public async Task PublishAsync(LibraryNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand("library", request.EventType, request.SourceEntityId, request.RecipientUserId, JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
