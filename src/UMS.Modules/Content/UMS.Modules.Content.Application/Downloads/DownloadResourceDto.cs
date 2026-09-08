namespace UMS.Modules.Content.Application.Downloads;

public sealed record DownloadResourceDto(
    Guid Id,
    string Title,
    string Category,
    Guid ArtifactId,
    string Status,
    DateTimeOffset? PublishAt,
    DateTimeOffset? ExpireAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version);

public sealed record DownloadResourceListPage(IReadOnlyList<DownloadResourceDto> Items, int TotalCount, int Skip, int Take);
