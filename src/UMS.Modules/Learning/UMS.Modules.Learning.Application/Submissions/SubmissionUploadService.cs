using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Shared.Academic;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.Submissions;

/// <summary>
/// LRN-5: the presigned direct-to-object-storage upload flow for a <c>Submission</c>'s file(s),
/// brokered entirely through Documents' own <c>UploadedArtifact</c> pipeline
/// (<see cref="IUploadedArtifactGateway"/>) - request a slot, the client PUTs the bytes straight to
/// object storage bypassing both modules' API tiers, then confirm.
///
/// <para>
/// <b>Authorization lives here, not in Documents.</b> Documents' own upload endpoints are gated by
/// <c>document.document.generate</c> - a capability a Student will never hold - so this module
/// gates the upload against its OWN rule instead: the caller must be an enrolled Student of the
/// Assignment's CourseOffering, and the Assignment must still be accepting submissions from them at
/// this instant. That is the same division documents requirement-spec.md §1 already draws for
/// virus-scanning ("owned wherever the upload endpoint lives, not by Documents' own rendering
/// path").
/// </para>
///
/// <para>
/// <b>Known gap, stated rather than glossed over:</b> requirement-spec.md §7.1 assigns Learning the
/// responsibility to trigger virus-scanning of uploaded source material before marking a file
/// usable. No scanning service or shared contract for one exists anywhere in this platform today
/// (nothing in <c>ums-devops</c>' dev topology provides a ClamAV/equivalent endpoint), so this
/// build confirms the artifact against Documents' own checksum/existence verification and stops
/// there - flagged in the module's PR rather than silently treated as satisfied.
/// </para>
/// </summary>
public sealed class SubmissionUploadService(
    IAssignmentRepository assignments,
    ICourseOfferingLookup courseOfferings,
    IUploadedArtifactGateway artifacts,
    IClock clock)
{
    private const string ArtifactType = "LearningSubmission";

    public async Task<Result<SubmissionUploadSlotDto>> RequestUploadAsync(
        Guid assignmentId,
        Guid callerUserId,
        RequestSubmissionUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eligibility = await RequireOpenForCallerAsync(assignmentId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (eligibility.IsFailure)
        {
            return eligibility.Error!;
        }

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            return Error.Validation("submission_upload.file_name_required", "A file name is required.");
        }

        var slot = await artifacts.RequestUploadAsync(callerUserId, ArtifactType, request.MimeType, cancellationToken).ConfigureAwait(false);
        return slot.IsFailure
            ? slot.Error!
            : new SubmissionUploadSlotDto(slot.Value.ArtifactId, slot.Value.Status, slot.Value.UploadUrl, request.FileName.Trim(), request.MimeType);
    }

    /// <summary>Confirms the bytes actually landed. A non-<c>Ready</c> outcome is returned as a real failure - an unconfirmed artifact is never a usable Submission file reference (documents requirement-spec.md §4).</summary>
    public async Task<Result<SubmissionUploadSlotDto>> ConfirmUploadAsync(
        Guid assignmentId,
        Guid callerUserId,
        ConfirmSubmissionUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eligibility = await RequireOpenForCallerAsync(assignmentId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (eligibility.IsFailure)
        {
            return eligibility.Error!;
        }

        var confirmed = await artifacts.ConfirmAsync(request.ArtifactId, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return confirmed.Error!;
        }

        return confirmed.Value.IsReady
            ? new SubmissionUploadSlotDto(confirmed.Value.ArtifactId, confirmed.Value.Status, null, string.Empty, confirmed.Value.MimeType)
            : Error.Conflict("submission_upload.not_ready", $"Uploaded artifact '{request.ArtifactId}' is in status '{confirmed.Value.Status}' - the upload did not complete.");
    }

    private async Task<Result> RequireOpenForCallerAsync(Guid assignmentId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var assignment = await assignments.GetByIdAsync(new AssignmentId(assignmentId), cancellationToken).ConfigureAwait(false);
        if (assignment is null)
        {
            return Result.Failure(Error.NotFound("assignment.not_found", $"No Assignment exists with id '{assignmentId}'."));
        }

        var enrolled = await courseOfferings.GetEnrolledStudentAsync(assignment.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (enrolled is null)
        {
            return Result.Failure(Error.Forbidden("submission.not_enrolled", $"The calling user is not an enrolled Student in CourseOffering '{assignment.CourseOfferingId}'."));
        }

        // Deliberately the SAME accept check the submission itself will run, so a Student is never
        // handed a presigned URL for a window that has already closed for them. The window is
        // re-evaluated at submit time regardless - this is an early, courteous rejection, never the
        // authoritative gate.
        var acceptance = assignment.EvaluateAcceptance(clock.UtcNow, enrolled.StudentId);
        return acceptance.IsAccepted
            ? Result.Success()
            : Result.Failure(Error.Conflict(acceptance.RejectionCode!, acceptance.RejectionMessage!));
    }
}
