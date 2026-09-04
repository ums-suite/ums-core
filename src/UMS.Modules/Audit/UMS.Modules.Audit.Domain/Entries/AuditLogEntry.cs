using System.Text.Json;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Domain.Entries;

/// <summary>
/// The append-only record of one sensitive mutation (glossary; requirement-spec.md audit §1/§3).
/// This is Audit's sole business-data aggregate (§1: "Audit owns exactly one aggregate"). It is
/// deliberately not an <c>AggregateRoot</c> in Identity's sense (no base class, no domain events)
/// - ADR-0012: "Audit is a terminal write target... it does not itself publish domain events for
/// other modules to consume." Its entire lifecycle is the single transition modeled by the one
/// factory method below: created. There is no other public constructor, setter, or mutation
/// method anywhere on this class - by construction, not by convention, matching design-decisions.md's
/// "Append-Only Enforcement Mechanism" (API-layer/type-layer omission as one of its two layers of
/// defense in depth, the other being the database GRANT/REVOKE AUD-2 adds).
/// </summary>
public sealed class AuditLogEntry
{
    private AuditLogEntry()
    {
    }

    public AuditLogEntryId Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public string ActorId { get; private set; } = string.Empty;

    public AuditActorType ActorType { get; private set; }

    public string? IpAddress { get; private set; }

    public string Application { get; private set; } = string.Empty;

    public string EntityType { get; private set; } = string.Empty;

    public string EntityId { get; private set; } = string.Empty;

    public AuditAction Action { get; private set; } = null!;

    public string? BeforeValueJson { get; private set; }

    public string? AfterValueJson { get; private set; }

    public string CorrelationId { get; private set; } = string.Empty;

    public string? Reason { get; private set; }

    public Guid? OrganizationScopeId { get; private set; }

    /// <summary>
    /// The sole way an <see cref="AuditLogEntry"/> ever comes into existence (requirement-spec.md
    /// audit §4's invariants, all enforced here, at the aggregate boundary, rather than left to
    /// caller discipline in <c>UMS.Modules.Audit.Infrastructure</c>'s <c>AuditRecorder</c>):
    /// actor/application/entity/action/correlation-id are all required; before/after payloads, if
    /// present, must be well-formed JSON snapshots (never the full row - that bound is a calling-module
    /// discipline this validation cannot see, only enforce the shape of); and a reversal-shaped
    /// action (an inherent one, or one the caller flags via <paramref name="isCorrection"/>) must
    /// carry a non-empty <paramref name="reason"/>.
    /// </summary>
    public static Result<AuditLogEntry> Record(
        string actorId,
        AuditActorType actorType,
        string? ipAddress,
        string application,
        string entityType,
        string entityId,
        string action,
        string? beforeValueJson,
        string? afterValueJson,
        string correlationId,
        string? reason,
        bool isCorrection,
        Guid? organizationScopeId,
        DateTimeOffset occurredAt)
    {
        if (string.IsNullOrWhiteSpace(actorId))
        {
            return Error.Validation("audit_entry.actor_required", "Actor id is required - use a well-known 'system:<job-name>' principal for background jobs, never a null/empty actor.");
        }

        if (string.IsNullOrWhiteSpace(application))
        {
            return Error.Validation("audit_entry.application_required", "Application is required.");
        }

        if (string.IsNullOrWhiteSpace(entityType))
        {
            return Error.Validation("audit_entry.entity_type_required", "Entity type is required and must match docs/ddd/ubiquitous-language.md exactly.");
        }

        if (!char.IsUpper(entityType.TrimStart()[0]))
        {
            return Error.Validation("audit_entry.entity_type_invalid", "Entity type must be the aggregate's PascalCase glossary name (e.g. 'Grade'), never an abbreviation or a table name.");
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return Error.Validation("audit_entry.entity_id_required", "Entity id is required.");
        }

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            return Error.Validation("audit_entry.correlation_id_required", "Correlation id is required.");
        }

        var actionResult = AuditAction.Create(action);
        if (actionResult.IsFailure)
        {
            return actionResult.Error!;
        }

        if (!IsWellFormedJsonOrNull(beforeValueJson))
        {
            return Error.Validation("audit_entry.before_value_invalid", "Before-value snapshot must be well-formed JSON.");
        }

        if (!IsWellFormedJsonOrNull(afterValueJson))
        {
            return Error.Validation("audit_entry.after_value_invalid", "After-value snapshot must be well-formed JSON.");
        }

        var requiresReason = isCorrection || actionResult.Value.IsInherentlyReversal;
        if (requiresReason && string.IsNullOrWhiteSpace(reason))
        {
            return Error.Validation(
                "audit_entry.reason_required",
                $"Reason is required for a reversal-shaped action ('{actionResult.Value.Value}'{(isCorrection ? ", flagged as a correction" : string.Empty)}).");
        }

        return new AuditLogEntry
        {
            Id = AuditLogEntryId.New(),
            OccurredAt = occurredAt,
            ActorId = actorId.Trim(),
            ActorType = actorType,
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress.Trim(),
            Application = application.Trim(),
            EntityType = entityType.Trim(),
            EntityId = entityId.Trim(),
            Action = actionResult.Value,
            BeforeValueJson = beforeValueJson,
            AfterValueJson = afterValueJson,
            CorrelationId = correlationId.Trim(),
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            OrganizationScopeId = organizationScopeId,
        };
    }

    private static bool IsWellFormedJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var parsed = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
