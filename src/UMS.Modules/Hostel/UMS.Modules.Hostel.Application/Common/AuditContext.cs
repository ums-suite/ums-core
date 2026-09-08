using UMS.Shared.Audit;

namespace UMS.Modules.Hostel.Application.Common;

/// <summary>ADR-0012: every allocation-state transition, check-in/out, and complaint resolution is a 100%-audited sensitive mutation (requirement-spec.md §5). Mirrors every other module's own <c>AuditContext</c> exactly.</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-hostel-internal";

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
