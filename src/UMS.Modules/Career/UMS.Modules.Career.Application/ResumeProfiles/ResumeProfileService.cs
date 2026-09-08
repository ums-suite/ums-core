using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Domain.ResumeProfiles;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Career.Application.ResumeProfiles;

/// <summary>
/// CAR-5: `ResumeProfile` CRUD (requirement-spec.md §2.7). Uses
/// `UMS.Shared.Documents.IUploadedArtifactRequester` (requirement-spec.md §7.1, confirmed/unblocked)
/// - stores only the returned `artifactId`, never file bytes.
///
/// <para>
/// edge-cases.md "ResumeProfile submission racing storage confirmation": <see cref="CreateAsync"/>
/// synchronously re-confirms the artifact is `Ready` (Documents' own state) before writing the
/// `ResumeProfile` row - a Student sees a brief wait rather than an instant "uploaded" response with
/// silent background processing (mirrors Documents' own single-artifact synchronous posture).
/// </para>
/// </summary>
public sealed class ResumeProfileService(IResumeProfileRepository resumeProfiles, IUploadedArtifactRequester artifactRequester, IUnitOfWork unitOfWork, IClock clock)
{
    private const string ArtifactType = "CareerResumeProfile";

    public static ResumeProfileDto ToDto(ResumeProfile profile) => new(
        profile.Id.Value, profile.StudentId, profile.Label, profile.ArtifactId, profile.FileName, profile.IsDefault, profile.CreatedAt, profile.UpdatedAt);

    /// <summary>Step 1: requests a presigned upload slot for the Student's own resume PDF.</summary>
    public async Task<Result<ResumeUploadSlotDto>> RequestUploadAsync(Guid studentIdentityUserId, RequestResumeUploadRequest request, CancellationToken cancellationToken = default)
    {
        var slot = await artifactRequester.RequestUploadAsync(new RequestUploadedArtifactCommand(studentIdentityUserId, ArtifactType, request.MimeType), cancellationToken).ConfigureAwait(false);
        return slot.Match<Result<ResumeUploadSlotDto>>(
            value => new ResumeUploadSlotDto(value.ArtifactId, value.UploadUrl),
            error => error);
    }

    /// <summary>Step 2: confirms the upload landed, then creates the `ResumeProfile` row.</summary>
    public async Task<Result<ResumeProfileDto>> CreateAsync(Guid studentId, CreateResumeProfileRequest request, CancellationToken cancellationToken = default)
    {
        var confirmed = await artifactRequester.ConfirmAsync(request.ArtifactId, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return confirmed.Error!;
        }

        if (!string.Equals(confirmed.Value.Status, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Conflict("resumeprofile.artifact_not_ready", "The uploaded resume is still processing - try again shortly.");
        }

        ResumeProfile profile;
        try
        {
            profile = ResumeProfile.Create(studentId, request.Label, request.ArtifactId, ExtractFileName(request.ArtifactId), request.IsDefault, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("resumeprofile.invalid", ex.Message);
        }

        if (request.IsDefault)
        {
            var previousDefault = await resumeProfiles.GetDefaultAsync(studentId, cancellationToken).ConfigureAwait(false);
            previousDefault?.UnmarkAsDefault();
        }

        resumeProfiles.Add(profile);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(profile);
    }

    public async Task<Result<ResumeProfileDto>> UpdateAsync(Guid id, Guid studentId, UpdateResumeProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await resumeProfiles.GetByIdAsync(new ResumeProfileId(id), cancellationToken).ConfigureAwait(false);
        if (profile is null || profile.StudentId != studentId)
        {
            return Error.NotFound("resumeprofile.not_found", $"No ResumeProfile exists with id '{id}' for this Student.");
        }

        Guid? confirmedArtifactId = null;
        if (request.NewArtifactId is { } newArtifactId)
        {
            var confirmed = await artifactRequester.ConfirmAsync(newArtifactId, cancellationToken).ConfigureAwait(false);
            if (confirmed.IsFailure)
            {
                return confirmed.Error!;
            }

            if (!string.Equals(confirmed.Value.Status, "Ready", StringComparison.OrdinalIgnoreCase))
            {
                return Error.Conflict("resumeprofile.artifact_not_ready", "The uploaded resume is still processing - try again shortly.");
            }

            confirmedArtifactId = newArtifactId;
        }

        try
        {
            profile.Update(request.Label, confirmedArtifactId, confirmedArtifactId is null ? null : ExtractFileName(confirmedArtifactId.Value), clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("resumeprofile.invalid", ex.Message);
        }

        if (request.IsDefault && !profile.IsDefault)
        {
            var previousDefault = await resumeProfiles.GetDefaultAsync(studentId, cancellationToken).ConfigureAwait(false);
            previousDefault?.UnmarkAsDefault();
            profile.MarkAsDefault();
        }
        else if (!request.IsDefault)
        {
            profile.UnmarkAsDefault();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(profile);
    }

    /// <summary>edge-cases.md "A Student deletes a ResumeProfile that a past, still-viewable CareerApplication snapshot references" - a soft delete; already-taken snapshots are untouched (they never dereference this row live).</summary>
    public async Task<Result> DeleteAsync(Guid id, Guid studentId, CancellationToken cancellationToken = default)
    {
        var profile = await resumeProfiles.GetByIdAsync(new ResumeProfileId(id), cancellationToken).ConfigureAwait(false);
        if (profile is null || profile.StudentId != studentId)
        {
            return Result.Failure(Error.NotFound("resumeprofile.not_found", $"No ResumeProfile exists with id '{id}' for this Student."));
        }

        profile.Delete();
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<IReadOnlyList<ResumeProfileDto>> ListByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        (await resumeProfiles.ListByStudentAsync(studentId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    public async Task<Result<ResumeProfileDto>> GetByIdAsync(Guid id, Guid studentId, CancellationToken cancellationToken = default)
    {
        var profile = await resumeProfiles.GetByIdAsync(new ResumeProfileId(id), cancellationToken).ConfigureAwait(false);
        return profile is null || profile.StudentId != studentId
            ? Error.NotFound("resumeprofile.not_found", $"No ResumeProfile exists with id '{id}' for this Student.")
            : ToDto(profile);
    }

    private static string ExtractFileName(Guid artifactId) => $"resume-{artifactId}.pdf";
}
