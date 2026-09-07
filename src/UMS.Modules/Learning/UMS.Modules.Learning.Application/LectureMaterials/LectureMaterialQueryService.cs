using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Application.Assignments;
using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.LectureMaterials;
using UMS.Shared.Academic;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Application.LectureMaterials;

/// <summary>
/// LRN-15: enrollment-scoped browse, ordered by <c>(moduleGroup, sortOrder)</c> with the latest
/// version resolved by default - plus the explicit prior-version read
/// (<c>GET /lecture-materials/{id}/versions/{versionId}</c>) that makes
/// design-decisions.md's "old version stays individually addressable" guarantee real rather than
/// nominal.
/// </summary>
public sealed class LectureMaterialQueryService(ILectureMaterialRepository materials, ICourseOfferingLookup courseOfferings)
{
    public async Task<Result<IReadOnlyList<LectureMaterialDto>>> ListByCourseOfferingAsync(
        Guid courseOfferingId,
        Guid callerUserId,
        string? languageCode,
        CancellationToken cancellationToken = default)
    {
        var access = await AssignmentQueryService.ResolveAccessAsync(courseOfferings, courseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var language = ResolveLanguage(languageCode);
        var items = await materials.GetByCourseOfferingAsync(courseOfferingId, cancellationToken).ConfigureAwait(false);
        return items.Select(m => LectureMaterialService.ToDto(m, language)).ToList();
    }

    /// <summary>requirement-spec.md §6 <c>GET /lecture-materials/{id}/versions/{versionId}</c> - a prior version is served exactly as published, never rewritten to point at the current one.</summary>
    public async Task<Result<LectureMaterialVersionDto>> GetVersionAsync(
        Guid lectureMaterialId,
        Guid versionId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var material = await materials.GetByIdAsync(new LectureMaterialId(lectureMaterialId), cancellationToken).ConfigureAwait(false);
        if (material is null)
        {
            return Error.NotFound("lecture_material.not_found", $"No LectureMaterial exists with id '{lectureMaterialId}'.");
        }

        var access = await AssignmentQueryService.ResolveAccessAsync(courseOfferings, material.CourseOfferingId, callerUserId, cancellationToken).ConfigureAwait(false);
        if (access.IsFailure)
        {
            return access.Error!;
        }

        var version = material.VersionById(new LectureMaterialVersionId(versionId));
        return version is null
            ? Error.NotFound("lecture_material_version.not_found", $"No version '{versionId}' exists for LectureMaterial '{lectureMaterialId}'.")
            : LectureMaterialService.ToDto(version);
    }

    private static LanguageCode ResolveLanguage(string? languageCode) =>
        Enum.TryParse<LanguageCode>(languageCode, ignoreCase: true, out var parsed) ? parsed : LanguageCode.En;
}
