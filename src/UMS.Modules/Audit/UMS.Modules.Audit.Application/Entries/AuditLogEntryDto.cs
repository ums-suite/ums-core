using UMS.Modules.Audit.Domain.Entries;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.Application.Entries;

public sealed record AuditLogEntryDto(
    string Id,
    DateTimeOffset OccurredAt,
    string ActorId,
    AuditActorType ActorType,
    string? IpAddress,
    string Application,
    string EntityType,
    string EntityId,
    string Action,
    string? BeforeValue,
    string? AfterValue,
    string CorrelationId,
    string? Reason,
    Guid? OrganizationScopeId)
{
    public static AuditLogEntryDto FromDomain(AuditLogEntry entry) => new(
        entry.Id.Value,
        entry.OccurredAt,
        entry.ActorId,
        entry.ActorType,
        entry.IpAddress,
        entry.Application,
        entry.EntityType,
        entry.EntityId,
        entry.Action.Value,
        entry.BeforeValueJson,
        entry.AfterValueJson,
        entry.CorrelationId,
        entry.Reason,
        entry.OrganizationScopeId);
}

public sealed record AuditLogEntryListPage(IReadOnlyList<AuditLogEntryDto> Items, int TotalCount, int Skip, int Take);
