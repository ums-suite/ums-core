using UMS.Modules.Career.Application.Abstractions;

namespace UMS.Modules.Career.Application.StudentGraduation;

/// <summary>
/// CAR-16: consumes `StudentGraduated`/`StudentStatusChanged` from Student's own outbox
/// (requirement-spec.md §3 "Consumed", §7). design-decisions.md "Student-Graduation Boundary for
/// In-Flight Career Activity": this handler is DELIBERATELY trivial - it acknowledges every envelope
/// with NO further mutation to any `CareerApplication`, since the real `Student.status = Active`
/// eligibility gate is a separate, LIVE read (`InternshipApplicationService`/`DriveApplicationService`
/// call `IStudentStatusChecker` fresh at each new-submission attempt, never from a cached/consumed
/// copy of this event). This is a deliberately DIFFERENT posture from Hostel's own
/// `AllocationReviewFlag` (which DOES flag existing records on a status change) - Career does nothing
/// to in-flight records at all; do not copy the flagging pattern here.
/// </summary>
public sealed class StudentStatusEventConsumerService(IStudentStatusEventSource eventSource)
{
    public async Task<int> ConsumeUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var envelopes = await eventSource.GetUnprocessedAsync(batchSize, cancellationToken).ConfigureAwait(false);

        foreach (var envelope in envelopes)
        {
            // No mutation - see this class's own remarks. Acknowledging is the entire job.
            await eventSource.MarkProcessedAsync(envelope.EventId, cancellationToken).ConfigureAwait(false);
        }

        return envelopes.Count;
    }
}
