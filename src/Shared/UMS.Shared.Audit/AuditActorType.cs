namespace UMS.Shared.Audit;

/// <summary>
/// Whether an <c>AuditLogEntry</c>'s actor is a human <see cref="User"/> or a well-known
/// <c>system:&lt;job-name&gt;</c> principal (requirement-spec.md audit §2/§8: "a background/system
/// job performs a sensitive mutation with no human actor" - the actor field must never be
/// <c>null</c>, which would make "who/what did this" queries silently incomplete).
/// </summary>
public enum AuditActorType
{
    User = 0,
    System = 1,
}
