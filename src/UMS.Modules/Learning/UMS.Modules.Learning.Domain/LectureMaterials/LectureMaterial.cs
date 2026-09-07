using UMS.Modules.Learning.Domain.Common;
using UMS.Modules.Learning.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Domain.LectureMaterials;

/// <summary>
/// LRN-12/LRN-14/LRN-15: a video, document, or link resource published against Academic's
/// <c>CourseOffering</c>, ordered into a syllabus-like structure
/// (docs/ddd/ubiquitous-language.md).
///
/// <para>
/// Glossary-classified "Entity", but modeled here as its own persistence boundary
/// (<see cref="AggregateRoot{TId}"/>) - requirement-spec.md §9.6 states this explicitly and cites
/// the precedent: Documents' <c>DocumentTemplate</c> (also glossary-classified "Entity") gets the
/// identical treatment for the identical reason. No parent aggregate for it is named anywhere in
/// the glossary, and inventing a "Syllabus" the requirements don't describe would be worse than
/// reusing an established shape.
/// </para>
///
/// <para>
/// Versioning is append-only: <see cref="PublishNewVersion"/> adds a
/// <see cref="LectureMaterialVersion"/> and moves <see cref="CurrentVersionId"/> forward; it never
/// mutates or removes a prior version. Default retrieval resolves to
/// <see cref="CurrentVersion"/>; a prior version stays addressable by its own id
/// (design-decisions.md, "LectureMaterial Versioning &amp; Retrieval Default").
/// </para>
/// </summary>
public sealed class LectureMaterial : AggregateRoot<LectureMaterialId>
{
    private readonly List<LectureMaterialVersion> _versions = [];
    private readonly List<LectureMaterialTranslation> _translations = [];

    private LectureMaterial()
    {
    }

    private LectureMaterial(
        LectureMaterialId id,
        Guid courseOfferingId,
        LectureMaterialType materialType,
        string moduleGroup,
        int sortOrder,
        Guid publishedByUserId,
        DateTimeOffset now)
    {
        Id = id;
        CourseOfferingId = courseOfferingId;
        MaterialType = materialType;
        ModuleGroup = moduleGroup;
        SortOrder = sortOrder;
        PublishedByUserId = publishedByUserId;
        CreatedAt = now;
    }

    public Guid CourseOfferingId { get; private set; }

    public LectureMaterialType MaterialType { get; private set; }

    /// <summary>The syllabus grouping this material sits in, e.g. <c>"Week 3"</c> or <c>"Module 2 - Recursion"</c>. Free text by design - a university's own syllabus vocabulary is not something this module should enumerate.</summary>
    public string ModuleGroup { get; private set; } = string.Empty;

    /// <summary>Explicit ordering within <see cref="ModuleGroup"/> (requirement-spec.md §2: "module/week grouping + explicit sort order").</summary>
    public int SortOrder { get; private set; }

    public Guid PublishedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public LectureMaterialVersionId? CurrentVersionId { get; private set; }

    public IReadOnlyCollection<LectureMaterialVersion> Versions => _versions.AsReadOnly();

    public IReadOnlyCollection<LectureMaterialTranslation> Translations => _translations.AsReadOnly();

    /// <summary>The version default retrieval resolves to - always the most recently published one.</summary>
    public LectureMaterialVersion? CurrentVersion => _versions.Find(v => v.Id == CurrentVersionId);

    public static Result<LectureMaterial> Create(
        Guid courseOfferingId,
        LectureMaterialType materialType,
        string moduleGroup,
        int sortOrder,
        Guid publishedByUserId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(moduleGroup))
        {
            return Error.Validation("lecture_material.module_group_required", "A LectureMaterial must name the syllabus module/week grouping it belongs to.");
        }

        return sortOrder < 0
            ? Error.Validation("lecture_material.sort_order_negative", "A LectureMaterial's sortOrder cannot be negative.")
            : new LectureMaterial(LectureMaterialId.New(), courseOfferingId, materialType, moduleGroup.Trim(), sortOrder, publishedByUserId, now);
    }

    /// <summary>Sets (or replaces) one language's title/description. English is required at creation time so the platform-wide fallback always has something to resolve to (ADR-0011).</summary>
    public Result SetTranslation(LanguageCode language, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("lecture_material.title_required", $"A {language} title is required."));
        }

        var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (_translations.Find(t => t.Language == language) is { } existing)
        {
            existing.Update(title.Trim(), trimmedDescription);
            return Result.Success();
        }

        _translations.Add(LectureMaterialTranslation.Create(Id, language, title.Trim(), trimmedDescription));
        return Result.Success();
    }

    /// <summary>Resolves this material's title in the requested language, falling back to English (ums-conventions.md, Localization Implementation: "English as the universal fallback").</summary>
    public LectureMaterialTranslation? ResolveTranslation(LanguageCode language) =>
        _translations.Find(t => t.Language == language) ?? _translations.Find(t => t.Language == LanguageCode.En);

    /// <summary>LRN-12's first version, published in the same operation as the material itself, and LRN-14's every subsequent one. Never mutates or deletes an existing version.</summary>
    public Result<LectureMaterialVersion> PublishNewVersion(Guid? artifactId, string? externalUrl, string? changeNote, Guid publishedByUserId, DateTimeOffset now)
    {
        var created = LectureMaterialVersion.Create(Id, _versions.Count + 1, MaterialType, artifactId, externalUrl, changeNote, publishedByUserId, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var isFirstVersion = _versions.Count == 0;
        _versions.Add(created.Value);
        CurrentVersionId = created.Value.Id;

        var title = ResolveTranslation(LanguageCode.En)?.Title ?? string.Empty;
        if (isFirstVersion)
        {
            Raise(new LectureMaterialPublished(Id.Value, CourseOfferingId, MaterialType.ToString(), title, publishedByUserId, now));
        }
        else
        {
            Raise(new LectureMaterialVersionPublished(Id.Value, created.Value.Id.Value, CourseOfferingId, created.Value.VersionNumber, publishedByUserId, now));
        }

        return created.Value;
    }

    /// <summary>requirement-spec.md §6 <c>GET /lecture-materials/{id}/versions/{versionId}</c> - explicit prior-version access, the other half of "default retrieval resolves to latest".</summary>
    public LectureMaterialVersion? VersionById(LectureMaterialVersionId versionId) => _versions.Find(v => v.Id == versionId);
}
