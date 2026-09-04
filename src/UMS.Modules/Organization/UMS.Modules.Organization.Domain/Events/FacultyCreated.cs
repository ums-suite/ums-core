using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Events;

/// <summary>requirement-spec.md organization §3: Faculty lifecycle change, dispatched to Audit (synchronous, via the calling Application service - see Faculties/FacultyManagementService.cs).</summary>
public sealed record FacultyCreated(Guid FacultyId, Guid CampusId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
