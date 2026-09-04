using UMS.Modules.Notifications.Application.Abstractions;
using UMS.Modules.Notifications.Application.Requests;
using UMS.Modules.Notifications.Domain.Requests;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Notifications.Application.Admin;

/// <summary>NTF-14: admin/ops delivery-status and dead-letter triage - <c>GET /notifications/requests/{id}</c>, <c>GET /notifications/dead-letters</c>.</summary>
public sealed class NotificationStatusQueryService(INotificationRequestRepository requests, INotificationDeliveryAttemptRepository attempts)
{
    public async Task<Result<NotificationRequestDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var request = await requests.GetByIdAsync(new NotificationRequestId(id), cancellationToken).ConfigureAwait(false);
        if (request is null)
        {
            return Error.NotFound("notification_request.not_found", $"No NotificationRequest exists with id '{id}'.");
        }

        return NotificationRequestDto.FromDomain(request);
    }

    public async Task<IReadOnlyList<DeadLetterDto>> ListDeadLettersAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        var deadLetters = await attempts.ListDeadLettersAsync(skip, take, cancellationToken).ConfigureAwait(false);
        if (deadLetters.Count == 0)
        {
            return [];
        }

        var requestIds = deadLetters.Select(a => a.NotificationRequestId.Value).Distinct().ToArray();
        var parentRequests = (await requests.GetByIdsAsync(requestIds, cancellationToken).ConfigureAwait(false)).ToDictionary(r => r.Id);

        return [.. deadLetters.Select(attempt =>
        {
            var parent = parentRequests.GetValueOrDefault(attempt.NotificationRequestId);
            return new DeadLetterDto(
                attempt.Id.Value,
                attempt.NotificationRequestId.Value,
                parent?.EventType ?? "unknown",
                parent?.Category,
                attempt.Channel,
                attempt.DeadLetterReason,
                attempt.LastError,
                attempt.AttemptCount,
                attempt.UpdatedAt);
        })];
    }
}
