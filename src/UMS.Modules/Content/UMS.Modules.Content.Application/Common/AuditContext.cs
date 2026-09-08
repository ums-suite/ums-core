using UMS.Shared.Audit;

namespace UMS.Modules.Content.Application.Common;

/// <summary>
/// design-decisions.md "Audit-Write Synchronicity": publish/archive/edit-after-publish are
/// synchronous, same-transaction audit writes - Content's lower-alerting-priority tier
/// (`ums-conventions.md`) affects observability/paging only, never audit durability/atomicity.
/// Mirrors every other module's own <c>AuditContext</c> exactly (per-module duplication is the
/// established convention).
/// </summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-content-internal";

    public RecordAuditEntryRequest ToRequest(
        string entityType,
        string entityId,
        string action,
        string? beforeValueJson,
        string? afterValueJson,
        string? reason = null,
        Guid? organizationScopeId = null) =>
        new(
            ActorUserId.ToString(),
            AuditActorType.User,
            ActorIpAddress,
            CallingApplication,
            entityType,
            entityId,
            action,
            beforeValueJson,
            afterValueJson,
            CorrelationId,
            reason,
            OrganizationScopeId: organizationScopeId);

    public static RecordAuditEntryRequest ForSystemJob(
        string jobName,
        string correlationId,
        string entityType,
        string entityId,
        string action,
        string? beforeValueJson,
        string? afterValueJson) =>
        new(
            $"system:{jobName}",
            AuditActorType.System,
            IpAddress: null,
            CallingApplication,
            entityType,
            entityId,
            action,
            beforeValueJson,
            afterValueJson,
            correlationId);
}
