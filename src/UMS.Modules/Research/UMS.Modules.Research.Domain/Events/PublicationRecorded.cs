using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

/// <summary>requirement-spec.md §3/§6: consumed by Reporting; a ResearchProfile display (Faculty-owned) is resolved client-side against this module's public API, never a direct subscription (§1, §9).</summary>
public sealed record PublicationRecorded(Guid PublicationId, DateTimeOffset OccurredAt) : IDomainEvent;
