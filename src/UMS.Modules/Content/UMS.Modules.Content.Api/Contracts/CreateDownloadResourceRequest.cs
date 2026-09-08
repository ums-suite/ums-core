namespace UMS.Modules.Content.Api.Contracts;

public sealed record CreateDownloadResourceRequest(string Title, string Category, Guid ArtifactId);

public sealed record UpdateDownloadResourceMetadataRequest(string Title, string Category, uint Version);

public sealed record UpdateDownloadResourceScheduleRequest(DateTimeOffset? PublishAt, DateTimeOffset? ExpireAt, uint Version);

public sealed record DownloadResourceVersionedActionRequest(uint Version);
