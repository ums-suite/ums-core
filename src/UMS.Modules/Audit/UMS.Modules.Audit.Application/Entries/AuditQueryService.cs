using Microsoft.Extensions.Logging;
using UMS.Modules.Audit.Application.Abstractions;
using UMS.Modules.Audit.Domain.Entries;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Application.Entries;

/// <summary>
/// AUD-6/7/8: filtered listing, single-entry detail, and entity-history reads. Every method logs
/// "who queried what" via standard structured logging (AUD-11; requirement-spec.md audit §5/§9.3:
/// "a lighter tier... to avoid infinite regress" - deliberately not a second <see cref="AuditLogEntry"/>
/// per read) rather than skipping read-access logging - <c>correlationId</c>-traceable per
/// ums-conventions.md Observability, since <see cref="ILogger"/> is already enriched with it
/// platform-wide.
///
/// <para>
/// Scope note: requirement-spec.md audit §2/§4 expects a further ScopeGrant-based row filter (e.g.
/// a Department Head reading only their own department's history) resolved against each entry's
/// <see cref="AuditLogEntry.OrganizationScopeId"/>. That check depends on a cross-module
/// scope-resolution capability that does not exist yet - Organization (release/DEVELOPMENT_PLAN.md
/// Flow #6) is still Not Started, and Identity itself only stub-checks OrganizationNode existence
/// today (<c>StubOrganizationNodeExistenceChecker</c>). <see cref="OrganizationScopeId"/> is
/// captured at write time (AUD-1) specifically so this filter can be added later without a data
/// migration; until then, every reader holding <c>audit.entry.read</c> sees every entry regardless
/// of scope - a deliberate, documented gap, not an oversight.
/// </para>
/// </summary>
public sealed class AuditQueryService(IAuditLogEntryRepository repository, ILogger<AuditQueryService> logger)
{
    public async Task<AuditLogEntryListPage> ListAsync(AuditEntryFilter filter, int skip, int take, Guid queryingUserId, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var (items, total) = await repository.ListAsync(filter, skip, take, cancellationToken).ConfigureAwait(false);

        LogReadAccess("ListEntries", queryingUserId, filter);

        return new AuditLogEntryListPage(items.Select(AuditLogEntryDto.FromDomain).ToList(), total, skip, take);
    }

    public async Task<Result<AuditLogEntryDto>> GetByIdAsync(string id, Guid queryingUserId, CancellationToken cancellationToken = default)
    {
        var entry = await repository.GetByIdAsync(new AuditLogEntryId(id), cancellationToken).ConfigureAwait(false);

        LogReadAccess("GetEntryById", queryingUserId, id);

        return entry is null
            ? Error.NotFound("audit_entry.not_found", $"No audit entry exists with id '{id}'.")
            : AuditLogEntryDto.FromDomain(entry);
    }

    public async Task<IReadOnlyList<AuditLogEntryDto>> GetEntityHistoryAsync(string entityType, string entityId, Guid queryingUserId, CancellationToken cancellationToken = default)
    {
        var entries = await repository.GetEntityHistoryAsync(entityType, entityId, cancellationToken).ConfigureAwait(false);

        LogReadAccess("GetEntityHistory", queryingUserId, $"{entityType}/{entityId}");

        return entries.Select(AuditLogEntryDto.FromDomain).ToList();
    }

    private void LogReadAccess(string queryKind, Guid queryingUserId, object query)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Audit read access: {QueryKind} by user {QueryingUserId} with query {@Query}",
                queryKind,
                queryingUserId,
                query);
        }
    }
}
