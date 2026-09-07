using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Domain.Events;

/// <summary>requirement-spec.md §3: "Immediately after submit" - consumed by Audit.</summary>
public sealed record ApplicationLocked(Guid ApplicationId, DateTimeOffset OccurredAt) : IDomainEvent;
