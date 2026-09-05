using Microsoft.EntityFrameworkCore;
using UMS.Modules.Academic.Application.Abstractions;
using UMS.Modules.Academic.Domain.ResultPublications;

namespace UMS.Modules.Academic.Infrastructure.Persistence.Repositories;

/// <summary>
/// design-decisions.md "Grade-Lock State Machine Design": every transition is a single
/// <c>UPDATE academic.result_publications SET status = @newStatus, ... WHERE id = @id AND status =
/// ANY(@expectedPriorStatuses)</c>. <see cref="TryTransitionAsync"/> builds this as raw
/// parameterized SQL (<see cref="RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade,string,object[])"/>)
/// rather than EF's <c>ExecuteUpdateAsync</c> LINQ builder specifically because the set of columns
/// to update varies per call site (<see cref="ResultPublicationTransitionColumns"/>) - composing
/// that dynamically is not expressible as the single expression tree <c>ExecuteUpdateAsync</c>
/// requires, whereas a conditionally-built SQL string with positional parameters is plain,
/// ordinary C#. Both mechanisms compile to the identical single atomic conditional-UPDATE
/// statement shape; <c>CourseOfferingRepository</c>'s seat-limit counter uses
/// <c>ExecuteUpdateAsync</c> directly since its shape never varies.
///
/// <para>
/// This is the ONE code path both edge-cases.md's "Grade lock racing a still-in-flight grade
/// submission" (Faculty submission vs. Department-Head lock) and "Concurrent Department-Head
/// review/approval of the same grade batch" (two reviewers) resolve through - whichever caller's
/// statement is evaluated by Postgres against the row's CURRENT (already-committed) status wins;
/// the loser's statement affects zero rows.
/// </para>
/// </summary>
internal sealed class ResultPublicationRepository(AcademicDbContext context) : IResultPublicationRepository
{
    // AsNoTracking deliberately, on BOTH reads: ResultPublication.Status (and every transition
    // column) is NEVER mutated through this DbContext's normal tracked SaveChanges path - every
    // transition is the raw-SQL conditional UPDATE above, which bypasses the change tracker
    // entirely. Without AsNoTracking, EF Core's identity-map behavior would return an
    // ALREADY-TRACKED (and therefore stale) instance for a row this same DbContext loaded earlier
    // in the request, even though the raw SQL UPDATE already committed a newer value to the actual
    // database row - exactly the bug this comment exists to prevent a regression of.
    public Task<ResultPublication?> GetByIdAsync(ResultPublicationId id, CancellationToken cancellationToken = default) =>
        context.ResultPublications.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ResultPublication?> GetByCourseOfferingIdAsync(Guid courseOfferingId, CancellationToken cancellationToken = default) =>
        context.ResultPublications.AsNoTracking().FirstOrDefaultAsync(r => r.CourseOfferingId == courseOfferingId, cancellationToken);

    public void Add(ResultPublication resultPublication) => context.ResultPublications.Add(resultPublication);

    public async Task<bool> TryTransitionAsync(
        ResultPublicationId id,
        IReadOnlyCollection<ResultPublicationStatus> expectedPriorStatuses,
        ResultPublicationStatus newStatus,
        Action<ResultPublicationTransitionColumns> applyColumns,
        CancellationToken cancellationToken = default)
    {
        var columns = new ResultPublicationTransitionColumns();
        applyColumns(columns);

        var setClauses = new List<string>();
        var parameters = new List<object>();

        void AddSet(string column, object value)
        {
            setClauses.Add($"{column} = {{{parameters.Count}}}");
            parameters.Add(value);
        }

        AddSet("status", newStatus.ToString());

        if (columns.CalculatedAt is { } calculatedAt)
        {
            AddSet("calculated_at", calculatedAt);
        }

        if (columns.LockedAt is { } lockedAt)
        {
            AddSet("locked_at", lockedAt);
        }

        if (columns.LockedByUserId is { } lockedByUserId)
        {
            AddSet("locked_by_user_id", lockedByUserId);
        }

        if (columns.ApprovedAt is { } approvedAt)
        {
            AddSet("approved_at", approvedAt);
        }

        if (columns.ApprovedByUserId is { } approvedByUserId)
        {
            AddSet("approved_by_user_id", approvedByUserId);
        }

        if (columns.PublishedAt is { } publishedAt)
        {
            AddSet("published_at", publishedAt);
        }

        if (columns.PublishedByUserId is { } publishedByUserId)
        {
            AddSet("published_by_user_id", publishedByUserId);
        }

        if (columns.ArchivedAt is { } archivedAt)
        {
            AddSet("archived_at", archivedAt);
        }

        if (columns.IncrementCorrectionCount)
        {
            setClauses.Add("correction_count = correction_count + 1");
        }

        if (columns.ClearRejection)
        {
            setClauses.Add("rejected_at = NULL");
            setClauses.Add("rejected_by_user_id = NULL");
            setClauses.Add("rejection_reason = NULL");
        }

        if (columns.ClearPublishedMetadata)
        {
            setClauses.Add("published_at = NULL");
            setClauses.Add("published_by_user_id = NULL");
        }

        var idPlaceholder = parameters.Count;
        parameters.Add(id.Value);
        var statusesPlaceholder = parameters.Count;
        parameters.Add(expectedPriorStatuses.Select(s => s.ToString()).ToArray());

        var sql = $"UPDATE academic.result_publications SET {string.Join(", ", setClauses)} WHERE id = {{{idPlaceholder}}} AND status = ANY({{{statusesPlaceholder}}})";

        var affected = await context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }

    public async Task<bool> TryRejectAsync(ResultPublicationId id, string reason, Guid rejectedByUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE academic.result_publications SET rejected_at = {0}, rejected_by_user_id = {1}, rejection_reason = {2} WHERE id = {3} AND status = {4}";
        var affected = await context.Database.ExecuteSqlRawAsync(
            sql,
            [now, rejectedByUserId, reason, id.Value, ResultPublicationStatus.Calculated.ToString()],
            cancellationToken).ConfigureAwait(false);
        return affected == 1;
    }
}
