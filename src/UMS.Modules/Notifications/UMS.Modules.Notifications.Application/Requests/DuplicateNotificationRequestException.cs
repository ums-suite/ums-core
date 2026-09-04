namespace UMS.Modules.Notifications.Application.Requests;

/// <summary>
/// Thrown by the Infrastructure DbContext when the dedupe unique constraint on
/// <c>(source_module, event_type, source_entity_id, recipient_id)</c> rejects an insert - design-
/// decisions.md, "Dedup-Key Enforcement Mechanism": "a constraint violation is treated as 'already
/// accepted'". <see cref="SubmitNotificationRequestService"/> is the only place that catches this -
/// mirrors Identity's own <c>DuplicateUserException</c> translation exactly.
/// </summary>
public sealed class DuplicateNotificationRequestException : Exception
{
    public DuplicateNotificationRequestException()
        : base("A NotificationRequest with this (sourceModule, eventType, sourceEntityId, recipientId) already exists.")
    {
    }
}
