using UMS.Modules.Alumni.Domain.Common;

namespace UMS.Modules.Alumni.Domain.Events;

/// <summary>requirement-spec.md §3: raised once the Student→Alumnus transition completes. Consumers: Notifications (welcome email, ALM-15), Reporting.</summary>
public sealed record AlumnusCreated(Guid AlumnusId, Guid StudentIdRef, DateTimeOffset OccurredAt) : IDomainEvent;
