using UMS.Shared.Audit;

namespace UMS.Modules.Audit.Application.Entries;

/// <summary>AUD-6: the combinable filter set `GET /audit/entries` supports (requirement-spec.md audit §6).</summary>
public sealed record AuditEntryFilter(
    string? EntityType = null,
    string? EntityId = null,
    string? ActorId = null,
    DateTimeOffset? DateFrom = null,
    DateTimeOffset? DateTo = null,
    string? Action = null,
    string? Application = null);
