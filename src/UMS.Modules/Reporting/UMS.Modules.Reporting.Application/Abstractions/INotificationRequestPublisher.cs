namespace UMS.Modules.Reporting.Application.Abstractions;

/// <summary>Reporting's own local port, adapted onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> in Infrastructure - mirrors every other module's own <c>INotificationRequestPublisher</c> exactly.</summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(ReportingNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <summary>requirement-spec.md §3/§7: RegulatoryReportRunCompleted/RegulatoryReportRunFailed fan out to Notifications.</summary>
public sealed record ReportingNotificationRequest(string EventType, string SourceEntityId, Guid RecipientUserId, IReadOnlyDictionary<string, string?> MergeFields);
