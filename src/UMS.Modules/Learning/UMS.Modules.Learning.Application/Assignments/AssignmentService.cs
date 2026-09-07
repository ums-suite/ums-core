using System.Text.Json;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.Common;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Shared.Academic;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Assignments;

/// <summary>
/// LRN-1/LRN-2/LRN-3: Assignment publication, closure, cancellation, and per-Student
/// <c>SubmissionExtension</c> grants.
///
/// <para>
/// <b>Every mutating path here re-checks Instructor ownership</b> against
/// <see cref="ICourseOfferingLookup.IsInstructorForOfferingAsync"/> - requirement-spec.md §6's own
/// note: "Instructor actions are additionally gated by a resource-ownership check against the
/// CourseOffering's CourseAssignment ... not merely a ScopeGrant - a Department Head's ScopeGrant
/// covers oversight/reporting reads across their Department, never unscoped write access to another
/// Instructor's Assignment." The endpoint's <c>learning.assignment.manage</c> permission is
/// necessary but never sufficient, mirroring how Faculty's <c>LeaveRequest</c> approve/reject and
/// Academic's attendance gate both layer a resource check on top of a permission string.
/// </para>
/// </summary>
public sealed class AssignmentService(
    IAssignmentRepository assignments,
    ICourseOfferingLookup courseOfferings,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>The disclosed buffer edge-cases.md's client-vs-server-clock entry calls for when an Instructor doesn't configure one explicitly: "a short, configurable buffer, e.g. a few minutes".</summary>
    private static readonly TimeSpan DefaultGracePeriod = TimeSpan.FromMinutes(5);

    public async Task<Result<AssignmentDto>> CreateAsync(Guid callerUserId, CreateAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<AllowedSubmissionType>(request.AllowedSubmissionType, ignoreCase: true, out var allowedSubmissionType))
        {
            return Error.Validation("assignment.invalid_submission_type", $"'{request.AllowedSubmissionType}' is not a valid allowed submission type.");
        }

        var offering = await courseOfferings.GetAsync(request.CourseOfferingId, cancellationToken).ConfigureAwait(false);
        if (offering is null)
        {
            return Error.NotFound("assignment.courseoffering_not_found", $"No CourseOffering exists with id '{request.CourseOfferingId}'.");
        }

        var ownership = await RequireInstructorAsync(request.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var policy = BuildLatePenaltyPolicy(request.LatePenaltyTiers);
        if (policy.IsFailure)
        {
            return policy.Error!;
        }

        var window = SubmissionWindow.Create(
            request.OpensAt,
            request.Deadline,
            request.GracePeriod ?? DefaultGracePeriod,
            policy.Value,
            request.HardCloseAt);
        if (window.IsFailure)
        {
            return window.Error!;
        }

        var assignment = Assignment.Create(
            request.CourseOfferingId,
            request.Title,
            request.Instructions ?? string.Empty,
            allowedSubmissionType,
            request.AllowResubmission,
            request.MaxPoints,
            window.Value,
            callerUserId,
            clock.UtcNow);
        if (assignment.IsFailure)
        {
            return assignment.Error!;
        }

        assignments.Add(assignment.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(assignment.Value);
    }

    public Task<Result<AssignmentDto>> PublishAsync(Guid assignmentId, Guid callerUserId, CancellationToken cancellationToken = default) =>
        TransitionAsync(assignmentId, callerUserId, a => a.Publish(clock.UtcNow), cancellationToken);

    public Task<Result<AssignmentDto>> CloseAsync(Guid assignmentId, Guid callerUserId, CancellationToken cancellationToken = default) =>
        TransitionAsync(assignmentId, callerUserId, a => a.Close(clock.UtcNow), cancellationToken);

    /// <summary>LRN-2 / edge-cases.md "A CourseOffering is cancelled mid-semester": the manual, human-initiated path. Submissions already collected stay retained and readable.</summary>
    public Task<Result<AssignmentDto>> CancelAsync(Guid assignmentId, Guid callerUserId, CancelAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return TransitionAsync(assignmentId, callerUserId, a => a.Cancel(request.Reason, clock.UtcNow), cancellationToken);
    }

    /// <summary>
    /// LRN-3: grants a per-Student accommodation, written to Audit synchronously in the same
    /// transaction as the grant itself (requirement-spec.md §5 Auditability - "a fairness-affecting
    /// accommodation decision is squarely a sensitive mutation"). The named Student must actually be
    /// enrolled in this Assignment's CourseOffering, so an extension can never be granted to someone
    /// who could not have submitted in the first place.
    /// </summary>
    public async Task<Result<SubmissionExtensionDto>> GrantExtensionAsync(
        Guid assignmentId,
        Guid callerUserId,
        GrantSubmissionExtensionRequest request,
        AuditContext audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(audit);

        var assignment = await assignments.GetByIdAsync(new AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'.");
        }

        var ownership = await RequireInstructorAsync(assignment.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var enrolled = await courseOfferings.GetEnrolledStudentByStudentIdAsync(assignment.CourseOfferingId, request.StudentId, cancellationToken).ConfigureAwait(false);
        if (enrolled is null)
        {
            return Error.Validation("assignment.student_not_enrolled", $"Student '{request.StudentId}' is not enrolled in CourseOffering '{assignment.CourseOfferingId}'.");
        }

        var granted = assignment.GrantExtension(
            request.StudentId,
            enrolled.IdentityUserId ?? Guid.Empty,
            request.ExtendedDeadline,
            callerUserId,
            request.Reason,
            request.WaivesLatePenalty,
            clock.UtcNow);
        if (granted.IsFailure)
        {
            return granted.Error!;
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "SubmissionExtension",
            granted.Value.Id.Value.ToString(),
            AuditActions.Create,
            null,
            JsonSerializer.Serialize(new
            {
                assignmentId,
                request.StudentId,
                request.ExtendedDeadline,
                request.WaivesLatePenalty,
            }),
            request.Reason);

        var commit = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commit.IsFailure ? commit.Error! : ToDto(granted.Value);
    }

    internal static AssignmentDto ToDto(Assignment assignment) => new(
        assignment.Id.Value,
        assignment.CourseOfferingId,
        assignment.Title,
        assignment.Instructions,
        assignment.AllowedSubmissionType.ToString(),
        assignment.AllowResubmission,
        assignment.MaxPoints,
        assignment.Status.ToString(),
        new SubmissionWindowDto(
            assignment.SubmissionWindow.OpensAt,
            assignment.SubmissionWindow.Deadline,
            assignment.SubmissionWindow.GracePeriod,
            assignment.SubmissionWindow.EffectiveDeadline,
            assignment.SubmissionWindow.HardCloseAt,
            assignment.SubmissionWindow.LatePenaltyPolicy.Tiers.Select(t => new LatePenaltyTierDto(t.MaxLateness, t.DeductionPercentage)).ToList()),
        assignment.CreatedByUserId,
        assignment.CreatedAt,
        assignment.PublishedAt,
        assignment.ClosedAt,
        assignment.CancelledAt,
        assignment.CancellationReason,
        assignment.Extensions.Select(ToDto).ToList());

    internal static SubmissionExtensionDto ToDto(SubmissionExtension extension) => new(
        extension.Id.Value,
        extension.StudentId,
        extension.ExtendedDeadline,
        extension.GrantedByUserId,
        extension.Reason,
        extension.WaivesLatePenalty,
        extension.GrantedAt);

    private static Result<LatePenaltyPolicy> BuildLatePenaltyPolicy(IReadOnlyCollection<LatePenaltyTierDto>? tiers)
    {
        if (tiers is null || tiers.Count == 0)
        {
            return LatePenaltyPolicy.NoDeduction;
        }

        var built = new List<LatePenaltyTier>(tiers.Count);
        foreach (var tier in tiers)
        {
            var created = LatePenaltyTier.Create(tier.MaxLateness, tier.DeductionPercentage);
            if (created.IsFailure)
            {
                return created.Error!;
            }

            built.Add(created.Value);
        }

        return LatePenaltyPolicy.Create(built);
    }

    private async Task<Result> RequireInstructorAsync(Guid courseOfferingId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var isInstructor = await courseOfferings.IsInstructorForOfferingAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        return isInstructor
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                "assignment.not_assigned_instructor",
                $"The calling user is not the assigned, active Instructor for CourseOffering '{courseOfferingId}'."));
    }

    private async Task<Result<AssignmentDto>> TransitionAsync(Guid assignmentId, Guid callerUserId, Func<Assignment, Result> transition, CancellationToken cancellationToken)
    {
        var assignment = await assignments.GetByIdAsync(new AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'.");
        }

        var ownership = await RequireInstructorAsync(assignment.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var result = transition(assignment);
        if (result.IsFailure)
        {
            return result.Error!;
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("assignment.version_conflict", ex.Message);
        }

        return ToDto(assignment);
    }
}
