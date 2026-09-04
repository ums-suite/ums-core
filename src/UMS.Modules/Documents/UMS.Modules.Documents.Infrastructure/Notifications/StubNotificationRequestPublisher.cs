using Microsoft.Extensions.Logging;
using UMS.Modules.Documents.Application.Abstractions;

namespace UMS.Modules.Documents.Infrastructure.Notifications;

/// <summary>
/// EXPLICIT SEAM, not a real dispatch: Notifications (release/DEVELOPMENT_PLAN.md Flow #8) is
/// being built concurrently, in a separate branch, and does not exist on this branch - there is no
/// real interface to call yet. Every <see cref="NotificationRequest"/> is logged and dropped until
/// Notifications lands and this registration is replaced with one that calls Notifications' real
/// public interface in-process. Mirrors exactly how Identity originally stubbed
/// <c>IOrganizationNodeExistenceChecker</c> with a <c>StubOrganizationNodeExistenceChecker</c>
/// before Organization (Flow #6) existed - deliberately kept as its own named type (rather than an
/// inline lambda) so it is easy to find and delete when that day comes.
/// </summary>
internal sealed class StubNotificationRequestPublisher(ILogger<StubNotificationRequestPublisher> logger) : INotificationRequestPublisher
{
    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "NotificationRequest stub: would notify user {RecipientUserId} of '{EventType}': {Subject} [correlationId={CorrelationId}]. Notifications (Flow #8) does not exist yet on this branch - no real dispatch occurred.",
                request.RecipientUserId,
                request.EventType,
                request.Subject,
                request.CorrelationId);
        }

        return Task.CompletedTask;
    }
}
