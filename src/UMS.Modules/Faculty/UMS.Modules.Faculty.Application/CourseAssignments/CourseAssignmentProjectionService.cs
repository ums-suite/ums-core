using System.Text.Json;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Domain.CourseAssignments;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Application.CourseAssignments;

/// <summary>
/// FAC-4: consumes Academic's `InstructorAssigned`/`InstructorUnassigned` events (via
/// <see cref="IInstructorAssignmentEventSource"/>, Infrastructure) and maintains Faculty's own
/// eventually-consistent <c>CourseAssignment</c> projection (requirement-spec.md faculty §2, §9's
/// first Decision). Called from <c>UMS.Workers</c>' relay worker, never from an HTTP request path.
///
/// <para>
/// design-decisions.md, "Audit Synchronous Integration Transaction Boundary": the projection
/// upsert's own commit and its Audit entry commit together even though the *originating* event
/// delivery itself is asynchronous - the actor is <see cref="AuditActorType.System"/> since no
/// human initiated this specific write (Academic's own Registrar/Department-Head actor is
/// recorded on Academic's own audit trail for the originating action).
/// </para>
/// </summary>
public sealed class CourseAssignmentProjectionService(
    ICourseAssignmentRepository courseAssignments,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder)
{
    private const string SystemActorId = "system:faculty-course-assignment-projection";

    public async Task<Result> ApplyInstructorAssignedAsync(InstructorAssignmentPayload payload, DateTimeOffset eventOccurredAt, string correlationId, CancellationToken cancellationToken = default) =>
        await ApplyAsync(payload, eventOccurredAt, correlationId, unassign: false, cancellationToken).ConfigureAwait(false);

    public async Task<Result> ApplyInstructorUnassignedAsync(InstructorAssignmentPayload payload, DateTimeOffset eventOccurredAt, string correlationId, CancellationToken cancellationToken = default) =>
        await ApplyAsync(payload, eventOccurredAt, correlationId, unassign: true, cancellationToken).ConfigureAwait(false);

    private async Task<Result> ApplyAsync(InstructorAssignmentPayload payload, DateTimeOffset eventOccurredAt, string correlationId, bool unassign, CancellationToken cancellationToken)
    {
        var existing = await courseAssignments.GetForUpdateAsync(payload.FacultyMemberId, payload.CourseOfferingId, cancellationToken).ConfigureAwait(false);

        bool applied;
        CourseAssignment courseAssignment;
        string action;

        if (existing is null)
        {
            if (unassign)
            {
                // An Unassigned event with no prior projection row at all - nothing to reflect
                // (edge-cases.md, "Duplicate or Out-of-Order ... Delivery": the guard is per-row,
                // this is the zero-row case of the same class of problem). Ack and move on.
                return Result.Success();
            }

            courseAssignment = CourseAssignment.CreateFromAssigned(payload.FacultyMemberId, payload.CourseOfferingId, eventOccurredAt);
            courseAssignments.Add(courseAssignment);
            applied = true;
            action = "project_assigned";
        }
        else
        {
            courseAssignment = existing;
            applied = unassign ? courseAssignment.ApplyUnassigned(eventOccurredAt) : courseAssignment.ApplyAssigned(eventOccurredAt);
            action = unassign ? "project_unassigned" : "project_assigned";
        }

        if (!applied)
        {
            // Stale/duplicate event per the monotonic ordering guard (design-decisions.md) - a
            // no-op, not an error; the outbox relay should still ack it.
            return Result.Success();
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var auditRequest = new RecordAuditEntryRequest(
            ActorId: SystemActorId,
            ActorType: AuditActorType.System,
            IpAddress: null,
            Application: "ums-core",
            EntityType: "CourseAssignment",
            EntityId: courseAssignment.Id.Value.ToString(),
            Action: action,
            BeforeValueJson: null,
            AfterValueJson: JsonSerializer.Serialize(new { facultyMemberId = payload.FacultyMemberId, courseOfferingId = payload.CourseOfferingId, status = courseAssignment.Status.ToString() }),
            CorrelationId: correlationId);

        var auditResult = await auditRecorder.RecordEntryAsync(auditRequest, transaction.DbTransaction, cancellationToken).ConfigureAwait(false);
        if (auditResult.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return auditResult;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
