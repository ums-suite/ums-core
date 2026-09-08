using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Reporting.Infrastructure.CrossModule;

/// <summary>Adapts Reporting's own local <see cref="INotificationRequestPublisher"/> port onto <see cref="INotificationRequestIntake"/> - mirrors every other module's own equivalent adapter exactly.</summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake) : INotificationRequestPublisher
{
    private const string SourceModule = "reporting";

    public async Task PublishAsync(ReportingNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await intake.SubmitAsync(
            new SubmitNotificationRequestCommand(
                SourceModule,
                request.EventType,
                request.SourceEntityId,
                request.RecipientUserId,
                System.Text.Json.JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);
    }
}
