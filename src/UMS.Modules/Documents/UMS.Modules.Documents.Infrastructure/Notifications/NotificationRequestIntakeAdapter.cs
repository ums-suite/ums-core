using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Documents.Application.Abstractions;
using UMS.Shared.Notifications;

namespace UMS.Modules.Documents.Infrastructure.Notifications;

/// <summary>
/// DOC-13's real implementation, now that Notifications (release/DEVELOPMENT_PLAN.md Flow #8)
/// exists: adapts Documents' own <see cref="INotificationRequestPublisher"/> call sites onto
/// Notifications' real, shared <see cref="INotificationRequestIntake"/> cross-module contract - the
/// same in-process adapter pattern Identity's <c>RecipientDirectoryAdapter</c> and Identity's
/// <c>OrganizationNodeExistenceCheckerAdapter</c> both already established for their own first real
/// consumer. Best-effort at every call site (see those methods' own remarks) - a Notifications
/// outage never fails or re-queues a document generation that itself already succeeded.
/// </summary>
internal sealed class NotificationRequestIntakeAdapter(INotificationRequestIntake intake, ILogger<NotificationRequestIntakeAdapter> logger) : INotificationRequestPublisher
{
    private const string SourceModule = "documents";

    public async Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
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
                "Notifications rejected the {EventType} request for {RecipientUserId}: {Error} [correlationId={CorrelationId}].",
                request.EventType,
                request.RecipientUserId,
                result.Error,
                request.CorrelationId);
        }
    }
}
