namespace UMS.Modules.Notifications.Application.Abstractions;

/// <summary>One row claimed for dispatch (NTF-13/NTF-16) - a lightweight projection, not the tracked entity, since the claim query's own atomic UPDATE already performed the InFlight transition (see the Infrastructure implementation's remarks on why claiming and processing are two separate steps).</summary>
public sealed record ClaimedAttempt(Guid AttemptId, Guid NotificationRequestId, int AttemptCount);
