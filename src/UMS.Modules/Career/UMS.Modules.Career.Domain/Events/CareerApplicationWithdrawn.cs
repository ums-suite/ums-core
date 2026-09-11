using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3/§4: Student withdraws before a terminal status - no cross-application side effects. Consumer: Reporting.</summary>
public sealed record CareerApplicationWithdrawn(Guid CareerApplicationId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
