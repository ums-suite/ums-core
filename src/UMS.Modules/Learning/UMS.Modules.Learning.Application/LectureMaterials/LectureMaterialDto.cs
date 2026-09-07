namespace UMS.Modules.Learning.Application.LectureMaterials;

public sealed record LectureMaterialDto(
    Guid Id,
    Guid CourseOfferingId,
    string MaterialType,
    string ModuleGroup,
    int SortOrder,
    string Title,
    string? Description,
    string ResolvedLanguage,
    Guid PublishedByUserId,
    DateTimeOffset CreatedAt,
    LectureMaterialVersionDto? CurrentVersion,
    IReadOnlyCollection<LectureMaterialVersionSummaryDto> Versions);

public sealed record LectureMaterialVersionDto(
    Guid Id,
    int VersionNumber,
    Guid? ArtifactId,
    string? ExternalUrl,
    string? ChangeNote,
    Guid PublishedByUserId,
    DateTimeOffset PublishedAt);

public sealed record LectureMaterialVersionSummaryDto(Guid Id, int VersionNumber, DateTimeOffset PublishedAt, bool IsCurrent);

/// <summary><paramref name="TitleEn"/> is required; the Bengali pair is optional and falls back to English server-side (ADR-0011, ums-conventions.md's Localization Implementation).</summary>
public sealed record CreateLectureMaterialRequest(
    string MaterialType,
    string ModuleGroup,
    int SortOrder,
    string TitleEn,
    string? DescriptionEn,
    string? TitleBn,
    string? DescriptionBn,
    Guid? ArtifactId,
    string? ExternalUrl);

public sealed record PublishLectureMaterialVersionRequest(Guid? ArtifactId, string? ExternalUrl, string? ChangeNote);

public sealed record RequestLectureMaterialUploadRequest(string MimeType);

public sealed record LectureMaterialUploadSlotDto(Guid ArtifactId, string Status, string? UploadUrl);

public sealed record ConfirmLectureMaterialUploadRequest(Guid ArtifactId);
