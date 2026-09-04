using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Domain.Entries;

namespace UMS.Modules.Audit.Application.Abstractions;

/// <summary>
/// The read side of <see cref="AuditLogEntry"/> (AUD-6/7/8). Every method queries the partitioned
/// parent table directly, relying on PostgreSQL's own partition-pruning/merge-append (edge-cases.md,
/// "A history query spans a partition boundary" decision) - no method here is ever implemented
/// against a per-partition child table.
/// </summary>
public interface IAuditLogEntryRepository
{
    public Task<AuditLogEntry?> GetByIdAsync(AuditLogEntryId id, CancellationToken cancellationToken = default);

    public Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> ListAsync(
        AuditEntryFilter filter,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// AUD-9: the unbounded (no page-size clamp) equivalent of <see cref="ListAsync"/>, used only
    /// by <c>UMS.Workers</c>' export relay - an export's whole point is returning everything a
    /// filter matches, unlike the paginated admin-UI list view.
    /// </summary>
    public Task<IReadOnlyList<AuditLogEntry>> ListAllAsync(AuditEntryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>AUD-8: full ordered timeline for one entity, sorted by the monotonic ULID id (design-decisions.md's Ordering/Sequencing Mechanism), never by <c>occurred_at</c> alone.</summary>
    public Task<IReadOnlyList<AuditLogEntry>> GetEntityHistoryAsync(
        string entityType,
        string entityId,
        CancellationToken cancellationToken = default);
}
