using UMS.Shared.Audit;

namespace UMS.Modules.Library.Application.Common;

/// <summary>ADR-0012/requirement-spec.md §5: loan issuance, fine accrual/waiver, and lost-copy write-offs are 100%-audited sensitive mutations. Mirrors every other module's own <c>AuditContext</c> exactly (per-module duplication is the established convention, not something to unify).</summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-library-internal";

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
