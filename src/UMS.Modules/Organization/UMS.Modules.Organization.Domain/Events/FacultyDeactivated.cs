using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Events;

/// <summary>requirement-spec.md organization §3: Faculty lifecycle change, dispatched to Audit (synchronous).</summary>
public sealed record FacultyDeactivated(Guid FacultyId, DateTimeOffset OccurredAt) : IDomainEvent;
