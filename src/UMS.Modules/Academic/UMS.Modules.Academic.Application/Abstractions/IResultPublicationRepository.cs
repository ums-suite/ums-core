using UMS.Modules.Academic.Domain.ResultPublications;

namespace UMS.Modules.Academic.Application.Abstractions;

/// <summary>design-decisions.md "Grade-Lock State Machine Design" - see <see cref="TryTransitionAsync"/>'s own remarks; the ONLY sanctioned way ACD-10..13 mutate <c>ResultPublication.Status</c> (or, for <c>Reject</c>, its rejection metadata) once the row already exists.</summary>
public interface IResultPublicationRepository
{
    public Task<ResultPublication?> GetByIdAsync(ResultPublicationId id, CancellationToken cancellationToken = default);

    public Task<ResultPublication?> GetByCourseOfferingIdAsync(Guid courseOfferingId, CancellationToken cancellationToken = default);

    /// <summary>Plain tracked insert - safe as an ordinary write since there is no existing row to race against (guarded instead by a DB unique index on <c>course_offering_id</c>, translated to an idempotent "already exists, fetch instead" outcome by the caller).</summary>
    public void Add(ResultPublication resultPublication);

    /// <summary>
    /// design-decisions.md "Grade-Lock State Machine Design": issues
    /// <c>UPDATE result_publications SET status = @newStatus, ... WHERE id = @id AND status =
    /// @expectedPriorStatus</c> via <c>ExecuteUpdateAsync</c>. <paramref name="applyColumns"/> lets
    /// each call site set its own transition-specific metadata columns (e.g. `locked_at`,
    /// `locked_by_user_id`) in the SAME statement as the guarded status change - still one
    /// indivisible conditional UPDATE. Returns <see langword="true"/> only if a row was actually
    /// affected; <see langword="false"/> means another writer already moved this batch past
    /// <paramref name="expectedPriorStatus"/> - the caller must re-read the row's actual current
    /// status and surface it in an explicit, named rejection (never a silent overwrite).
    /// </summary>
    public Task<bool> TryTransitionAsync(
        ResultPublicationId id,
        IReadOnlyCollection<ResultPublicationStatus> expectedPriorStatuses,
        ResultPublicationStatus newStatus,
        Action<ResultPublicationTransitionColumns> applyColumns,
        CancellationToken cancellationToken = default);

    /// <summary>ACD-11's reject path - a conditional metadata-only update (status unchanged) guarded by the identical `WHERE status = Calculated` predicate, so it correctly loses/wins against a concurrent Lock racing the same row (see <c>ResultPublication</c>'s own class remarks).</summary>
    public Task<bool> TryRejectAsync(ResultPublicationId id, string reason, Guid rejectedByUserId, DateTimeOffset now, CancellationToken cancellationToken = default);
}

/// <summary>The transition-specific metadata columns a <see cref="IResultPublicationRepository.TryTransitionAsync"/> call may set, mutually exclusive per call site.</summary>
public sealed class ResultPublicationTransitionColumns
{
    public DateTimeOffset? CalculatedAt { get; set; }

    public DateTimeOffset? LockedAt { get; set; }

    public Guid? LockedByUserId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTimeOffset? ArchivedAt { get; set; }

    public bool IncrementCorrectionCount { get; set; }

    public bool ClearRejection { get; set; }

    public bool ClearPublishedMetadata { get; set; }
}
