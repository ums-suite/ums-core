using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "New request created" - consumed by Notifications and the relevant Department Head queue (grievance routing, STU-11).</summary>
public sealed record StudentRequestSubmitted(Guid StudentRequestId, Guid StudentId, string RequestType, DateTimeOffset OccurredAt) : IDomainEvent;
