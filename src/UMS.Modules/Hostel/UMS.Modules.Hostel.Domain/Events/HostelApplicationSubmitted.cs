using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-3: requirement-spec.md §3 - consumers: Audit, Notifications.</summary>
public sealed record HostelApplicationSubmitted(Guid ApplicationId, Guid StudentId, Guid ApplicationWindowId, DateTimeOffset OccurredAt) : IDomainEvent;
