using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: deadline sweep (CAR-3) or explicit staff action. Consumer: Reporting.</summary>
public sealed record InternshipApplicationsClosed(Guid InternshipId, DateTimeOffset OccurredAt) : IDomainEvent;
