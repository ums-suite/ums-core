using UMS.Modules.Research.Domain.Common;

namespace UMS.Modules.Research.Domain.Events;

/// <summary>design-decisions.md "InstitutionalRepositoryEntry Embargo-Lift Mechanism" - raised by both the daily ADR-0014 scheduled worker (automatic) and an explicit Admin early override, same event either way.</summary>
public sealed record InstitutionalRepositoryEntryEmbargoLifted(Guid RepositoryEntryId, DateTimeOffset OccurredAt) : IDomainEvent;
