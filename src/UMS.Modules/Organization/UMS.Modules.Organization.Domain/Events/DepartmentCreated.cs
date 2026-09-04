using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Events;

/// <summary>requirement-spec.md organization §3: Department lifecycle change, dispatched to Audit (synchronous).</summary>
public sealed record DepartmentCreated(Guid DepartmentId, Guid FacultyId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
