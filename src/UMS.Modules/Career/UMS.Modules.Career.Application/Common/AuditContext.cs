using UMS.Shared.Audit;

namespace UMS.Modules.Career.Application.Common;

/// <summary>requirement-spec.md §5 Auditability / §7 Audit (ADR-0012): mirrors every other module's own `AuditContext` exactly (per-module duplication is the established convention). `CallingApplication = "ums-career-internal"` per this module's own mandatory-mechanisms note.</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-career-internal";

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
