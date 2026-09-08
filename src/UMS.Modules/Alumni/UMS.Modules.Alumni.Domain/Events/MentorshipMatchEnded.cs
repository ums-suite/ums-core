using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Events;

/// <summary>requirement-spec.md §3/§8: raised when either party ends an active match (or a proposal is rejected before acceptance). Consumer: Reporting; the mentee's own re-matchability is handled by the application layer (edge-cases.md "A mentor withdraws mid-match").</summary>
public sealed record MentorshipMatchEnded(Guid MentorshipMatchId, Guid MentorAlumnusId, Guid MenteeStudentId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
