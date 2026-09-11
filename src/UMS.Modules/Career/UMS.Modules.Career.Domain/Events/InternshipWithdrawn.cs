using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>
/// requirement-spec.md §3/§2.6; edge-cases.md "An employer withdraws an Internship after Students
/// have already applied"; design-decisions.md "Internship/Drive Withdrawal Cascade to
/// CareerApplication". Handled in-process (ADR-0003) by
/// <c>Applications.InternshipWithdrawalCascadeHandler</c>, which cancels every non-terminal
/// <c>CareerApplication</c> against <see cref="InternshipId"/> via
/// <c>CareerApplication.CancelDueToPostingWithdrawal()</c> - never the same code path a staff
/// `Reject` decision uses - each in its own transaction, plus a mandatory `NotificationRequest` per
/// affected Student. Consumers: Notifications (per affected Student), Reporting.
/// </summary>
public sealed record InternshipWithdrawn(Guid InternshipId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
