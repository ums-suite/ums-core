using UMS.Shared.Audit;

namespace UMS.Modules.Research.Application.Common;

/// <summary>
/// requirement-spec.md §4 last bullet / §7 Audit bullet (RES-15): every Grant lifecycle transition,
/// investigator change, Publication create/update/merge, and repository-entry deposit/embargo-lift
/// is a synchronous, same-transaction audit write (ADR-0012) - Research's lower-alerting-priority
/// tier (outside the Money-and-Academic-Standing Criticality class, §4/§5) affects
/// observability/paging severity only, never audit durability/atomicity. Mirrors every other
/// module's own <c>AuditContext</c> exactly (per-module duplication is the established convention).
/// </summary>
public sealed record AuditContext(Guid ActorUserId, string? ActorIpAddress, string CorrelationId)
{
    private const string CallingApplication = "ums-research-internal";

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
