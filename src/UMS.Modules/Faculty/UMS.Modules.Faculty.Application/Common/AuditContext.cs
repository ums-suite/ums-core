using UMS.Shared.Audit;

namespace UMS.Modules.Faculty.Application.Common;

/// <summary>Bundles the caller-identity fields every mutation call site needs to pass to <see cref="IAuditRecorder"/> - mirrors Organization's own <c>AuditContext</c> exactly.</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-faculty-web";

    /// <summary><paramref name="reason"/> is mandatory for a reversal-shaped action - Audit's own <c>AuditLogEntry.Create</c> rejects `reject`/`revoke` (requirement-spec.md audit §4) without one; every other action ignores it.</summary>
    public RecordAuditEntryRequest ToRequest(string entityType, string entityId, string action, string? beforeValueJson, string? afterValueJson, string? reason = null) =>
        new(
            ActorId: ActorUserId.ToString(),
            ActorType: AuditActorType.User,
            IpAddress: ActorIpAddress,
            Application: CallingApplication,
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            BeforeValueJson: beforeValueJson,
            AfterValueJson: afterValueJson,
            CorrelationId: CorrelationId,
            Reason: reason);
}
