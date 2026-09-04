namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>
/// DOC-13: raises a <c>NotificationRequest</c> so the requester's own module doesn't have to poll
/// for generation completion (requirement-spec.md documents §2/§6/§7; ADR-0009: "never a direct
/// email/SMS/push call"). Now that Notifications (release/DEVELOPMENT_PLAN.md Flow #8) exists, the
/// Infrastructure implementation (<c>NotificationRequestIntakeAdapter</c>) calls Notifications' real
/// <c>UMS.Shared.Notifications.INotificationRequestIntake</c> in-process - the same "real cross-
/// module contract, one implementation" pattern Organization's
/// <c>IOrganizationNodeExistenceChecker</c> established for Flow #6.
/// </summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
