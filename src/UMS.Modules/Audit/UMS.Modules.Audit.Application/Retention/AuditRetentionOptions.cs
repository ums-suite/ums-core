namespace UMS.Modules.Audit.Application.Retention;

/// <summary>
/// AUD-4: per-entity-type retention policy configuration (requirement-spec.md audit §5/§9.4:
/// "indefinite by default... configurable shorter retention only for lower-stakes entity types").
/// Bound from <c>Audit:Retention:EntityTypeRetentionMonths</c>. An entity type absent from this
/// dictionary is retained indefinitely - the safe default, since a missing config entry must never
/// silently shorten an academic/financial record's retention.
/// </summary>
public sealed class AuditRetentionOptions
{
    public Dictionary<string, int> EntityTypeRetentionMonths { get; set; } = [];
}
