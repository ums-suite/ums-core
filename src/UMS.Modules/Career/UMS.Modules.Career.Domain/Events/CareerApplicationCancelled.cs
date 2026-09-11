using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>
/// requirement-spec.md §3/§2.6: raised ONLY by <c>CareerApplication.CancelDueToPostingWithdrawal()</c>
/// - never by the staff `Reject` decision path (design-decisions.md "Internship/Drive Withdrawal
/// Cascade to CareerApplication"). Consumers: Notifications (mandatory - §2.6), Audit, Reporting.
/// </summary>
public sealed record CareerApplicationCancelled(Guid CareerApplicationId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
