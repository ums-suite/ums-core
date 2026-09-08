using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>
/// design-decisions.md "HostelApplication Withdrawal as a First-Class State Transition": an addition
/// beyond requirement-spec.md's own §3 event catalog (which pre-dates the Withdrawn state entirely) -
/// consumers: Notifications, Audit, same fan-out shape as Approved/Rejected.
/// </summary>
public sealed record HostelApplicationWithdrawn(Guid ApplicationId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
