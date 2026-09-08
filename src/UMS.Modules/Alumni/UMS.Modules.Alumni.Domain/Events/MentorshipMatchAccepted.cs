using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Events;

/// <summary>requirement-spec.md §3: raised the instant BOTH sides have accepted a proposed match. Consumer: Notifications (ALM-15).</summary>
public sealed record MentorshipMatchAccepted(Guid MentorshipMatchId, Guid MentorAlumnusId, Guid MenteeStudentId, DateTimeOffset OccurredAt) : IDomainEvent;
