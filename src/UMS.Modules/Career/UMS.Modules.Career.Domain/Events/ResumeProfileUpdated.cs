using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>requirement-spec.md §3: raised on a new upload or field edit against an existing `ResumeProfile`. Consumer: Reporting.</summary>
public sealed record ResumeProfileUpdated(Guid ResumeProfileId, Guid StudentId, DateTimeOffset OccurredAt) : IDomainEvent;
