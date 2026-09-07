using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Learning.Infrastructure.Notifications;

/// <summary>
/// Adapts this module's own <see cref="INotificationRequestPublisher"/> port onto Notifications'
/// real, shared <see cref="INotificationRequestIntake"/> contract (ADR-0009) - the identical
/// adapter Documents and Faculty both already have. Best-effort by construction: a Notifications
/// outage is logged and returned, never propagated back into a Learning mutation that already
/// committed.
/// </summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake, ILogger<NotificationRequestIntakeAdapter> logger) : INotificationRequestPublisher
{
    private const string SourceModule = "learning";

    public async Task PublishAsync(LearningNotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await intake.SubmitAsync(
            new SubmitNotificationRequestCommand(
                SourceModule,
                request.EventType,
                request.SourceEntityId,
                request.RecipientUserId,
                JsonSerializer.Serialize(request.MergeFields)),
            cancellationToken).ConfigureAwait(false);

        if (result.IsFailure && logger.IsEnabled(LogLevel.Warning))
        {
            logger.LogWarning(
                "Notifications rejected the {EventType} request for {RecipientUserId}: {Error} [dedupeKey={DedupeKey}].",
                request.EventType,
                request.RecipientUserId,
                result.Error,
                request.DedupeKey);
        }
    }
}
