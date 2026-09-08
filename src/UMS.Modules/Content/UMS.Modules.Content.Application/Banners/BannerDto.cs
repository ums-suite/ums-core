namespace UMS.Modules.Content.Application.Banners;

public sealed record BannerDto(
    Guid Id,
    string Headline,
    string ImageUrl,
    string? LinkUrl,
    int SortOrder,
    string Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? ExpireAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);
