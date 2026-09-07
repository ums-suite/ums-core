using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.LectureMaterials;
using UMS.Shared.Academic;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.LectureMaterials;

/// <summary>
/// LRN-12/LRN-13/LRN-14: LectureMaterial publication, its large-file presigned upload path (the
/// same Documents mechanism <c>SubmissionUploadService</c> uses - one shared mechanism serving both
/// large-file cases in this module, not two), and append-only versioning.
///
/// <para>
/// Every write path is Instructor-owned, resolved via
/// <see cref="ICourseOfferingLookup.IsInstructorForOfferingAsync"/> - the resource-ownership check
/// requirement-spec.md §6 requires on top of the <c>learning.lecturematerial.manage</c> permission
/// string.
/// </para>
/// </summary>
public sealed class LectureMaterialService(
    ILectureMaterialRepository materials,
    ICourseOfferingLookup courseOfferings,
    IUploadedArtifactGateway artifacts,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    private const string ArtifactType = "LectureMaterial";

    public async Task<Result<LectureMaterialDto>> PublishAsync(
        Guid courseOfferingId,
        Guid callerUserId,
        CreateLectureMaterialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Enum.TryParse<LectureMaterialType>(request.MaterialType, ignoreCase: true, out var materialType))
        {
            return Error.Validation("lecture_material.invalid_material_type", $"'{request.MaterialType}' is not a valid LectureMaterial type.");
        }

        var ownership = await RequireInstructorAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var material = LectureMaterial.Create(courseOfferingId, materialType, request.ModuleGroup, request.SortOrder, callerUserId, clock.UtcNow);
        if (material.IsFailure)
        {
            return material.Error!;
        }

        var english = material.Value.SetTranslation(LanguageCode.En, request.TitleEn, request.DescriptionEn);
        if (english.IsFailure)
        {
            return english.Error!;
        }

        if (!string.IsNullOrWhiteSpace(request.TitleBn))
        {
            var bengali = material.Value.SetTranslation(LanguageCode.Bn, request.TitleBn, request.DescriptionBn);
            if (bengali.IsFailure)
            {
                return bengali.Error!;
            }
        }

        var version = material.Value.PublishNewVersion(request.ArtifactId, request.ExternalUrl, null, callerUserId, clock.UtcNow);
        if (version.IsFailure)
        {
            return version.Error!;
        }

        materials.Add(material.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(material.Value, LanguageCode.En);
    }

    /// <summary>LRN-14: publishes a new version. The prior version is neither mutated nor deleted - it simply stops being the default retrieval target and stays addressable by its own id.</summary>
    public async Task<Result<LectureMaterialDto>> PublishVersionAsync(
        Guid lectureMaterialId,
        Guid callerUserId,
        PublishLectureMaterialVersionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var material = await materials.GetByIdAsync(new LectureMaterialId(lectureMaterialId), cancellationToken).ConfigureAwait(false);
        if (material is null)
        {
            return Error.NotFound("lecture_material.not_found", $"No LectureMaterial exists with id '{lectureMaterialId}'.");
        }

        var ownership = await RequireInstructorAsync(material.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var version = material.PublishNewVersion(request.ArtifactId, request.ExternalUrl, request.ChangeNote, callerUserId, clock.UtcNow);
        if (version.IsFailure)
        {
            return version.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(material, LanguageCode.En);
    }

    /// <summary>LRN-13: the same Documents presigned-upload mechanism LRN-5 uses, for the multi-hundred-MB video case requirement-spec.md §5 calls out as unable to pass through the API tier.</summary>
    public async Task<Result<LectureMaterialUploadSlotDto>> RequestUploadAsync(
        Guid courseOfferingId,
        Guid callerUserId,
        RequestLectureMaterialUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownership = await RequireInstructorAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var slot = await artifacts.RequestUploadAsync(callerUserId, ArtifactType, request.MimeType, cancellationToken).ConfigureAwait(false);
        return slot.IsFailure
            ? slot.Error!
            : new LectureMaterialUploadSlotDto(slot.Value.ArtifactId, slot.Value.Status, slot.Value.UploadUrl);
    }

    public async Task<Result<LectureMaterialUploadSlotDto>> ConfirmUploadAsync(
        Guid courseOfferingId,
        Guid callerUserId,
        ConfirmLectureMaterialUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownership = await RequireInstructorAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (ownership.IsFailure)
        {
            return ownership.Error!;
        }

        var confirmed = await artifacts.ConfirmAsync(request.ArtifactId, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return confirmed.Error!;
        }

        return confirmed.Value.IsReady
            ? new LectureMaterialUploadSlotDto(confirmed.Value.ArtifactId, confirmed.Value.Status, null)
            : Error.Conflict("lecture_material_upload.not_ready", $"Uploaded artifact '{request.ArtifactId}' is in status '{confirmed.Value.Status}' - the upload did not complete.");
    }

    internal static LectureMaterialDto ToDto(LectureMaterial material, LanguageCode language)
    {
        var translation = material.ResolveTranslation(language);
        var resolvedLanguage = material.Translations.Any(t => t.Language == language) ? language : LanguageCode.En;
        var current = material.CurrentVersion;

        return new LectureMaterialDto(
            material.Id.Value,
            material.CourseOfferingId,
            material.MaterialType.ToString(),
            material.ModuleGroup,
            material.SortOrder,
            translation?.Title ?? string.Empty,
            translation?.Description,
            resolvedLanguage.ToString(),
            material.PublishedByUserId,
            material.CreatedAt,
            current is null ? null : ToDto(current),
            material.Versions
                .OrderBy(v => v.VersionNumber)
                .Select(v => new LectureMaterialVersionSummaryDto(v.Id.Value, v.VersionNumber, v.PublishedAt, v.Id == material.CurrentVersionId))
                .ToList());
    }

    internal static LectureMaterialVersionDto ToDto(LectureMaterialVersion version) => new(
        version.Id.Value,
        version.VersionNumber,
        version.ArtifactId,
        version.ExternalUrl,
        version.ChangeNote,
        version.PublishedByUserId,
        version.PublishedAt);

    private async Task<Result> RequireInstructorAsync(Guid courseOfferingId, Guid callerUserId, CancellationToken cancellationToken)
    {
        var isInstructor = await courseOfferings.IsInstructorForOfferingAsync(courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        return isInstructor
            ? Result.Success()
            : Result.Failure(Error.Forbidden(
                "lecture_material.not_assigned_instructor",
                $"The calling user is not the assigned, active Instructor for CourseOffering '{courseOfferingId}'."));
    }
}
