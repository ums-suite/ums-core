using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.LectureMaterials;

/// <summary>
/// LRN-14 (module-local, pending glossary merge): one version-pinned file/link reference for a
/// <see cref="LectureMaterial"/>.
///
/// <para>
/// <b>Append-only, by construction.</b> requirement-spec.md §4: "LectureMaterialVersions are never
/// deleted or mutated once published". Every property here is <c>init</c>-only and there is no
/// mutating method of any kind on this type - the invariant is enforced by shape, not by a
/// convention a later call site could forget. A new version becomes the default retrieval target
/// going forward; every prior version stays individually addressable by its own id (design-
/// decisions.md, "LectureMaterial Versioning &amp; Retrieval Default", reusing Documents'
/// <c>DocumentTemplate</c> precedent).
/// </para>
///
/// <para>
/// <see cref="ArtifactId"/> is Documents' own <c>UploadedArtifactId</c> for a video/document
/// version; <see cref="ExternalUrl"/> is set instead for a link-type material. Exactly one of the
/// two is populated.
/// </para>
/// </summary>
public sealed class LectureMaterialVersion
{
    private LectureMaterialVersion()
    {
    }

    public LectureMaterialVersionId Id { get; private init; }

    public LectureMaterialId LectureMaterialId { get; private init; }

    /// <summary>1-based, strictly increasing within one <see cref="LectureMaterial"/>.</summary>
    public int VersionNumber { get; private init; }

    public Guid? ArtifactId { get; private init; }

    public string? ExternalUrl { get; private init; }

    public string? ChangeNote { get; private init; }

    public Guid PublishedByUserId { get; private init; }

    public DateTimeOffset PublishedAt { get; private init; }

    internal static Result<LectureMaterialVersion> Create(
        LectureMaterialId lectureMaterialId,
        int versionNumber,
        LectureMaterialType materialType,
        Guid? artifactId,
        string? externalUrl,
        string? changeNote,
        Guid publishedByUserId,
        DateTimeOffset now)
    {
        var hasArtifact = artifactId is not null && artifactId != Guid.Empty;
        var hasUrl = !string.IsNullOrWhiteSpace(externalUrl);

        if (hasArtifact == hasUrl)
        {
            return Error.Validation(
                "lecture_material_version.exactly_one_source_required",
                "A LectureMaterialVersion must reference exactly one of a Documents artifactId or an external URL.");
        }

        if (materialType == LectureMaterialType.Link && !hasUrl)
        {
            return Error.Validation("lecture_material_version.link_requires_url", "A Link LectureMaterial's version must carry an external URL.");
        }

        if (materialType != LectureMaterialType.Link && !hasArtifact)
        {
            return Error.Validation("lecture_material_version.file_requires_artifact", $"A {materialType} LectureMaterial's version must reference a confirmed Documents UploadedArtifact.");
        }

        return new LectureMaterialVersion
        {
            Id = LectureMaterialVersionId.New(),
            LectureMaterialId = lectureMaterialId,
            VersionNumber = versionNumber,
            ArtifactId = hasArtifact ? artifactId : null,
            ExternalUrl = hasUrl ? externalUrl!.Trim() : null,
            ChangeNote = string.IsNullOrWhiteSpace(changeNote) ? null : changeNote.Trim(),
            PublishedByUserId = publishedByUserId,
            PublishedAt = now,
        };
    }
}
