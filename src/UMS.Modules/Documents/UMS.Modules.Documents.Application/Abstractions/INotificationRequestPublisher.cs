namespace UMS.Modules.Documents.Application.Abstractions;

/// <summary>
/// DOC-13: raises a <c>NotificationRequest</c> so the requester's own module doesn't have to poll
/// for generation completion (requirement-spec.md documents §2/§6/§7; ADR-0009: "never a direct
/// email/SMS/push call"). Notifications (release/DEVELOPMENT_PLAN.md Flow #8) is being built
/// concurrently in a separate branch and does not exist yet on this branch - the Infrastructure
/// implementation registered today is an explicit stub seam, not a real dispatch, mirroring
/// exactly how Identity originally stubbed <c>IOrganizationNodeExistenceChecker</c> before
/// Organization (Flow #6) existed (see that interface/its former
/// <c>StubOrganizationNodeExistenceChecker</c> implementation for the precedent this follows).
/// Once Notifications lands, this registration is replaced with one that calls Notifications'
/// real public interface in-process - no other Documents code changes.
/// </summary>
public interface INotificationRequestPublisher
{
    public Task PublishAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
