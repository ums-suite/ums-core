using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "Review decision" (approve half) - STU-13.</summary>
public sealed record StudentRequestApproved(Guid StudentRequestId, Guid StudentId, Guid ApproverUserId, DateTimeOffset OccurredAt) : IDomainEvent;
