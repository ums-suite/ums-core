using UMS.Modules.Audit.Domain.Common;

namespace UMS.Modules.Audit.Domain.Entries;

/// <summary>
/// Audit's own strongly-typed id for <see cref="AuditLogEntry"/> - a ULID text value rather than
/// a <see cref="Guid"/> (ums-conventions.md, Domain Modeling: "every module's own strongly-typed
/// ID"; design-decisions.md's Ordering/Sequencing Mechanism decision: the ULID doubles as the
/// authoritative, lexicographically-sortable <c>ORDER BY</c> key for entity-history and
/// <c>correlationId</c>-filtered queries).
/// </summary>
public readonly record struct AuditLogEntryId(string Value)
{
    public static AuditLogEntryId New() => new(Ulid.NewUlid());

    public override string ToString() => Value;
}
