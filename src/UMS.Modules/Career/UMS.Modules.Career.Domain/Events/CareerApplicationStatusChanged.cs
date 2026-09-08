using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: any lifecycle transition (UnderReview/Shortlisted/Interviewed/Offered/Rejected, and InterviewScheduled on slot booking). Consumers: Notifications, Audit (§5, for Offered/Rejected), Reporting.</summary>
public sealed record CareerApplicationStatusChanged(Guid CareerApplicationId, Guid StudentId, string PreviousStatus, string NewStatus, DateTimeOffset OccurredAt) : IDomainEvent;
