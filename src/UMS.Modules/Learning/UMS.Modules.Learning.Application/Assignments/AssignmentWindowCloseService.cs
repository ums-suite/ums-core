using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.PlagiarismChecks;

namespace UMS.Modules.Learning.Application.Assignments;

/// <summary>
/// LRN-2/LRN-9's automatic half: closes any <c>Published</c> Assignment whose <c>hardCloseAt</c> has
/// passed (raising <c>AssignmentClosed</c>), then re-enqueues a <c>PlagiarismCheck</c> for every
/// counted Submission of that Assignment that still has no <c>Completed</c> one.
///
/// <para>
/// That second step is edge-cases.md's own residual note made real: "a PlagiarismCheck that failed
/// during an outage is automatically re-attempted once when the Assignment's window reaches
/// hardCloseAt ... giving genuinely-failed checks a second chance without requiring an Instructor to
/// manually notice and retry every one individually". Window close is the natural batch point
/// design-decisions.md picks because provider load is, by then, past its worst deadline-rush spike.
/// </para>
///
/// <para>
/// Deliberately separate from <see cref="AssignmentService"/>: this path has no human actor, so it
/// has no Instructor-ownership check to perform and no <c>AuditContext</c> to carry. Folding it into
/// the Instructor-facing service would have meant either a nullable actor threaded through every
/// method or an ownership check bypassed by a flag - both worse than one small, explicitly
/// system-initiated service.
/// </para>
/// </summary>
public sealed class AssignmentWindowCloseService(
    IAssignmentRepository assignments,
    PlagiarismCheckService plagiarismChecks,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    /// <summary>Returns how many Assignments were closed by this pass.</summary>
    public async Task<int> CloseElapsedWindowsAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var elapsed = await assignments.GetPublishedPastHardCloseAsync(now, batchSize, cancellationToken).ConfigureAwait(false);
        if (elapsed.Count == 0)
        {
            return 0;
        }

        var closed = new List<Guid>(elapsed.Count);
        foreach (var assignment in elapsed)
        {
            var result = assignment.Close(now);
            if (result.IsSuccess)
            {
                closed.Add(assignment.Id.Value);
            }
        }

        if (closed.Count == 0)
        {
            return 0;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var assignmentId in closed)
        {
            await plagiarismChecks.RequeueUncheckedForAssignmentAsync(assignmentId, cancellationToken).ConfigureAwait(false);
        }

        return closed.Count;
    }
}
