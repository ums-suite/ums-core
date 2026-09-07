namespace UMS.Modules.Finance.Domain.Ledger;

/// <summary>
/// requirement-spec.md finance §3: "Entity (append-only, own table). Written in the same transaction
/// as the state change it records - no separate aggregate boundary needed since it has no children
/// and is never updated." Deliberately NOT an <c>AggregateRoot</c> - it raises no domain events of
/// its own (design-decisions.md "Ledger-Entry Atomicity Mechanism": the row itself is a
/// direct, same-transaction write, never routed through the outbox; the separate
/// <c>LedgerEntryPosted</c> event other modules/Reporting consume is enqueued explicitly by the
/// calling Application service, not raised from this type).
///
/// <para>
/// <b>Immutable by construction</b> (requirement-spec.md §4) - every property is set once, in the
/// constructor, with no setter anywhere on this type; PostgreSQL-level enforcement
/// (<c>REVOKE UPDATE, DELETE</c>) is Infrastructure's job, mirroring Audit's own
/// <c>AuditLogEntry</c> write-once discipline (ADR-0012), applied here to Finance's own ledger
/// concept (a deliberate parallel, not a claim that this IS an AuditLogEntry).
/// </para>
/// </summary>
public sealed class LedgerEntry
{
    private LedgerEntry()
    {
    }

    private LedgerEntry(LedgerEntryId id, LedgerEntryType entryType, string referenceType, Guid referenceId, decimal amount, string currency, string description, string correlationId, DateTimeOffset occurredAt)
    {
        Id = id;
        EntryType = entryType;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        Amount = amount;
        Currency = currency;
        Description = description;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
    }

    public LedgerEntryId Id { get; private init; }

    public LedgerEntryType EntryType { get; private init; }

    public string ReferenceType { get; private init; } = string.Empty;

    public Guid ReferenceId { get; private init; }

    public decimal Amount { get; private init; }

    public string Currency { get; private init; } = string.Empty;

    public string Description { get; private init; } = string.Empty;

    public string CorrelationId { get; private init; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private init; }

    public static LedgerEntry Create(LedgerEntryType entryType, string referenceType, Guid referenceId, decimal amount, string currency, string description, string correlationId, DateTimeOffset occurredAt) =>
        new(LedgerEntryId.New(), entryType, referenceType, referenceId, amount, currency, description, correlationId, occurredAt);
}
