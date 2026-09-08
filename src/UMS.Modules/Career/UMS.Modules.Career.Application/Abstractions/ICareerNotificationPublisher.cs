namespace UMS.Modules.Career.Application.Abstractions;

/// <summary>
/// Career's own outbound-notification port (requirement-spec.md §7 Notifications, ADR-0009) - adapted
/// onto the real, shared <c>UMS.Shared.Notifications.INotificationRequestIntake</c> by
/// Infrastructure's own adapter, mirroring every other module's own
/// <c>IXxxNotificationPublisher</c>/adapter pair exactly.
/// </summary>
public interface ICareerNotificationPublisher
{
    public Task PublishAsync(CareerNotificationRequest request, CancellationToken cancellationToken = default);
}

/// <param name="EventType">e.g. <c>"InternshipPublished"</c>, <c>"CareerApplicationStatusChanged"</c>, <c>"CareerApplicationCancelled"</c>, <c>"InterviewSlotBooked"</c> (requirement-spec.md §3's event catalog / §7).</param>
/// <param name="SourceEntityId">The Career entity id this notification concerns.</param>
/// <param name="RecipientId">The Identity `UserId` to notify.</param>
/// <param name="MergeFields">Template merge-field data.</param>
public sealed record CareerNotificationRequest(string EventType, string SourceEntityId, Guid RecipientId, IReadOnlyDictionary<string, string> MergeFields);
