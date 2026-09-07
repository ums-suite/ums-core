using UMS.Modules.Academic.Domain.CourseOfferings;

namespace UMS.Modules.Academic.Application.Abstractions;

/// <summary>design-decisions.md "Seat-Limit Concurrency Control Pattern" - see <see cref="TryIncrementEnrolledCountAsync"/>/<see cref="TryDecrementEnrolledCountAsync"/>'s own remarks; these are the ONLY sanctioned way ACD-6/ACD-7 mutate <c>CourseOffering.EnrolledCount</c>.</summary>
public interface ICourseOfferingRepository
{
    public Task<CourseOffering?> GetByIdAsync(CourseOfferingId id, CancellationToken cancellationToken = default);

    public Task<IReadOnlyList<CourseOffering>> GetBySemesterAsync(Guid semesterId, CancellationToken cancellationToken = default);

    public void Add(CourseOffering courseOffering);

    /// <summary>
    /// ACD-6's atomic seat-limit check/claim - design-decisions.md's "Seat-Limit Concurrency Control
    /// Pattern": issues exactly <c>UPDATE course_offerings SET enrolled_count = enrolled_count + 1
    /// WHERE id = @id AND enrolled_count &lt; capacity</c> via <c>ExecuteUpdateAsync</c>, bypassing
    /// the change tracker entirely. Returns <see langword="true"/> only if a row was actually
    /// affected (a seat was genuinely still available at the instant Postgres evaluated the
    /// predicate) - <see langword="false"/> means the seat is gone; the caller must fail the whole
    /// enrollment transaction, never retry the same attempt silently.
    /// </summary>
    public Task<bool> TryIncrementEnrolledCountAsync(CourseOfferingId id, CancellationToken cancellationToken = default);

    /// <summary>ACD-7's atomic seat-release - the identical mechanism shape, decrementing (design-decisions.md "Drop-Then-Reenroll Seat-Release Ordering"). Guarded by <c>enrolled_count &gt; 0</c> so a drop can never push the counter negative even under a defensive double-drop race.</summary>
    public Task<bool> TryDecrementEnrolledCountAsync(CourseOfferingId id, CancellationToken cancellationToken = default);
}
