using UMS.Shared.Audit;

namespace UMS.Modules.Organization.Application.Common;

/// <summary>
/// Bundles the caller-identity fields every one of Organization's ~15 mutation call sites needs to
/// pass to <see cref="IAuditRecorder"/> (actor, IP, correlation id) - extracted from
/// <c>HttpContext</c> once, at the Api layer, per endpoint, rather than threading five separate
/// primitive parameters through every Application-service method signature (as Identity's own
/// single call site, <c>UserStatusService.ChangeStatusAsync</c>, does - Organization has far more
/// call sites, so the extra indirection pays for itself here).
/// </summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    // TODO: "which of the six SPAs originated this request" needs a platform-wide calling-
    // application header convention (none exists yet, per UserStatusService's own identical TODO)
    // - hardcoded until that convention lands.
    private const string CallingApplication = "ums-admin-web";

    public RecordAuditEntryRequest ToRequest(string entityType, string entityId, string action, string? beforeValueJson, string? afterValueJson) =>
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
            CorrelationId: CorrelationId);
}
