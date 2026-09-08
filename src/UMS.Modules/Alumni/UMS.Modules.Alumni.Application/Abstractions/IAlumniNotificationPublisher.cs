namespace UMS.Modules.Alumni.Application.Abstractions;

/// <summary>ALM-15: Alumni's own outbound port onto <c>UMS.Shared.Notifications.INotificationRequestIntake</c> - mirrors every other module's own per-module notification-publisher port.</summary>
public interface IAlumniNotificationPublisher
{
    public Task PublishAsync(AlumniNotificationRequest request, CancellationToken cancellationToken = default);
}

public sealed record AlumniNotificationRequest(string EventType, string SourceEntityId, Guid RecipientId, IReadOnlyDictionary<string, string?> MergeFields);
