using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Events;

/// <summary>requirement-spec.md organization §3: Program record lifecycle change, dispatched to Audit (synchronous).</summary>
public sealed record ProgramDeactivated(Guid ProgramId, DateTimeOffset OccurredAt) : IDomainEvent;
