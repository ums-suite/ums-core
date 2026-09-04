using UMS.Modules.Faculty.Domain.Common;

namespace UMS.Modules.Faculty.Domain.Events;

/// <summary>Published (via Faculty's own outbox) - consumed by Content/Public-Website's read path and Reporting (requirement-spec.md faculty §6/§9).</summary>
public sealed record ResearchProfileUpdated(Guid ResearchProfileId, Guid FacultyMemberId, DateTimeOffset OccurredAt) : IDomainEvent;
