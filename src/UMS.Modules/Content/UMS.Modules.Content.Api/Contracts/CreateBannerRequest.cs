namespace UMS.Modules.Content.Api.Contracts;

public sealed record CreateBannerRequest(string Headline, string ImageUrl, string? LinkUrl, int SortOrder);

public sealed record UpdateBannerDetailsRequest(string Headline, string ImageUrl, string? LinkUrl, int SortOrder, uint Version);

public sealed record UpdateBannerScheduleRequest(DateTimeOffset? PublishAt, DateTimeOffset? ExpireAt, uint Version);

public sealed record BannerVersionedActionRequest(uint Version);
