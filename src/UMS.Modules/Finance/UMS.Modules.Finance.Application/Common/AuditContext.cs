using UMS.Shared.Audit;

namespace UMS.Modules.Finance.Application.Common;

/// <summary>Bundles the caller-identity fields every mutation call site needs to pass to <see cref="IAuditRecorder"/> - mirrors every other module's own <c>AuditContext</c> exactly.</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-finance-internal";

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

    /// <summary>A system/background-job actor (the stuck-payment sweep) - no client IP, matching requirement-spec.md audit §8's own <c>system:&lt;job-name&gt;</c> example.</summary>
    public static RecordAuditEntryRequest ForSystemJob(string jobName, string correlationId, string entityType, string entityId, string action, string? beforeValueJson, string? afterValueJson) =>
        new(
            ActorId: $"system:{jobName}",
            ActorType: AuditActorType.System,
            IpAddress: null,
            Application: "system",
            EntityType: entityType,
            EntityId: entityId,
            Action: action,
            BeforeValueJson: beforeValueJson,
            AfterValueJson: afterValueJson,
            CorrelationId: correlationId);
}
