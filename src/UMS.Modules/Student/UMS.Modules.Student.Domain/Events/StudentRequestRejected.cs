using UMS.Modules.Student.Domain.Common;

namespace UMS.Modules.Student.Domain.Events;

/// <summary>requirement-spec.md student §3: "Review decision" (reject half) - STU-14, reason mandatory.</summary>
public sealed record StudentRequestRejected(Guid StudentRequestId, Guid StudentId, Guid ApproverUserId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
