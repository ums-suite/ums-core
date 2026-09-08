using UMS.Modules.Hostel.Domain.Common;

namespace UMS.Modules.Hostel.Domain.Events;

/// <summary>HOS-6: requirement-spec.md §3 - consumers: Notifications, Student (read-model), Audit.</summary>
public sealed record HostelApplicationRejected(Guid ApplicationId, Guid StudentId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
