namespace UMS.Modules.Content.Application.Notices;

/// <summary>
/// The language-resolved read shape (ADR-0011: "never returning both languages for the client to
/// pick") - <see cref="LanguageCode"/> names which language actually won (the caller's preference,
/// or `"en"` on fallback).
/// </summary>
public sealed record NoticeDto(
    Guid Id,
    string Title,
    string Body,
    string LanguageCode,
    string[] Audience,
    Guid? OrganizationNodeId,
    bool IsUrgent,
    string Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? ExpireAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool HasBengaliTranslation,
    uint Version);

public sealed record NoticeListPage(IReadOnlyList<NoticeDto> Items, int TotalCount, int Skip, int Take);
