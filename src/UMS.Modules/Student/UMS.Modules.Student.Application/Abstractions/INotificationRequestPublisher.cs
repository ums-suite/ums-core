namespace UMS.Modules.Student.Application.Abstractions;

/// <summary>
/// STU-4: Student's own local port onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> -
/// mirrors Documents'/Faculty's own <c>INotificationRequestPublisher</c>/
/// <c>NotificationRequestIntakeAdapter</c> pattern exactly. Called synchronously, best-effort,
/// directly from <c>CreateStudentRecordService</c> - mirroring Documents' own
/// <c>GenerateDocumentService.PublishNotificationSafelyAsync</c> (a Notifications outage must never
/// fail or roll back a Student creation that itself already succeeded, per ADR-0009).
/// </summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>Student's own fan-out request, exactly as it calls <see cref="INotificationRequestPublisher.PublishAsync"/>.</summary>
/// <param name="DedupeKey">Notifications' own dedup key (NTF-3) - the Student id is a stable, natural choice for the one-time welcome notification.</param>
public sealed record NotificationRequest(
    Guid RecipientUserId,
    string EventType,
    string SourceEntityId,
    IReadOnlyDictionary<string, string> MergeFields,
    string DedupeKey);
