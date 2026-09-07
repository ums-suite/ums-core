using UMS.Shared.Audit;

namespace UMS.Modules.Academic.Application.Common;

/// <summary>Bundles the caller-identity fields every mutation call site needs to pass to <see cref="IAuditRecorder"/> - mirrors Faculty/Student's own <c>AuditContext</c> exactly.</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-academic-web";

    public RecordAuditEntryRequest ToRequest(string entityType, string entityId, string action, string? beforeValueJson, string? afterValueJson, string? reason = null, Guid? organizationScopeId = null) =>
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
            Reason: reason,
            OrganizationScopeId: organizationScopeId);
}
