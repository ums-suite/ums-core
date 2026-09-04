using UMS.Modules.Organization.Domain.Common;

namespace UMS.Modules.Organization.Domain.Events;

/// <summary>
/// requirement-spec.md organization §3: Program record lifecycle change, dispatched to Audit
/// (synchronous) *and* to the outbox as an informational notification to Academic - "Academic
/// still owns Curriculum creation as its own act" (§3's own dispatch column), so this event
/// carries only the bare structural facts Academic would need to know a Program record now
/// exists, never anything about Curriculum (Organization owns none). Academic (Flow #12) does not
/// exist yet, so nothing consumes this row today - same "not yet consumed" outbox state every
/// other module's own not-yet-built downstream leaves behind (release/DEVELOPMENT_PLAN.md).
/// </summary>
public sealed record ProgramCreated(Guid ProgramId, Guid DepartmentId, string Name, DateTimeOffset OccurredAt) : IDomainEvent;
