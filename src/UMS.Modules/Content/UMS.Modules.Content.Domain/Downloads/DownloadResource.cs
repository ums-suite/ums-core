using UMS.Modules.Content.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Domain.Downloads;

/// <summary>
/// CNT-11: glossary (module-local term pending glossary merge, requirement-spec.md §9) - "a
/// lightweight, categorized list of downloadable resources (forms, prospectuses, policy documents)."
/// requirement-spec.md §2.6: the file itself lives in <c>Documents</c>' object storage
/// (<see cref="ArtifactId"/> references a <c>Documents</c> <c>UploadedArtifact</c> via
/// <c>UMS.Shared.Documents.IUploadedArtifactRequester</c>) - Content stores ONLY the metadata and
/// reference, never the blob. Reuses the identical <see cref="SchedulableStatus"/> scheduling
/// mechanism as Notice/Banner.
/// </summary>
public sealed class DownloadResource : AggregateRoot<DownloadResourceId>
{
    private DownloadResource()
    {
    }

    private DownloadResource(DownloadResourceId id, string title, string category, Guid artifactId, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Category = category;
        ArtifactId = artifactId;
        Status = SchedulableStatus.Draft;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    /// <summary>A `Documents` <c>UploadedArtifact</c> id - see class remarks. Never a raw file path/blob.</summary>
    public Guid ArtifactId { get; private set; }

    public SchedulableStatus Status { get; private set; }

    public DateTimeOffset? PublishAt { get; private set; }

    public DateTimeOffset? ExpireAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<DownloadResource> Create(string title, string category, Guid artifactId, Guid createdByUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("download.title_required", "A DownloadResource requires a title.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return Error.Validation("download.category_required", "A DownloadResource requires a category.");
        }

        if (artifactId == Guid.Empty)
        {
            return Error.Validation("download.artifact_required", "A DownloadResource requires a Documents artifact reference.");
        }

        return new DownloadResource(DownloadResourceId.New(), title.Trim(), category.Trim(), artifactId, createdByUserId, now);
    }

    public Result UpdateMetadata(string title, string category, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("download.title_required", "A DownloadResource requires a title."));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            return Result.Failure(Error.Validation("download.category_required", "A DownloadResource requires a category."));
        }

        Title = title.Trim();
        Category = category.Trim();
        UpdatedAt = now;
        return Result.Success();
    }

    public Result UpdateSchedule(DateTimeOffset? publishAt, DateTimeOffset? expireAt, DateTimeOffset now)
    {
        if (Status is SchedulableStatus.Published or SchedulableStatus.Archived)
        {
            return Result.Failure(Error.Conflict("download.not_schedulable", $"DownloadResource '{Id}' cannot have its schedule changed - it is currently '{Status}'."));
        }

        if (publishAt is null && expireAt is not null)
        {
            return Result.Failure(Error.Validation("download.expire_without_publish", "An expire_at cannot be set without a publish_at."));
        }

        if (publishAt is not null && expireAt is not null && expireAt <= publishAt)
        {
            return Result.Failure(Error.Validation("download.invalid_window", "expire_at must be strictly after publish_at."));
        }

        PublishAt = publishAt;
        ExpireAt = expireAt;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Schedule(DateTimeOffset now)
    {
        if (Status != SchedulableStatus.Draft)
        {
            return Result.Failure(Error.Conflict("download.not_draft", $"DownloadResource '{Id}' cannot be scheduled - it is currently '{Status}'."));
        }

        if (PublishAt is null)
        {
            return Result.Failure(Error.Validation("download.publish_at_required", "A publish_at is required to schedule a DownloadResource."));
        }

        Status = SchedulableStatus.Scheduled;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Publish(DateTimeOffset now)
    {
        if (Status is not (SchedulableStatus.Draft or SchedulableStatus.Scheduled))
        {
            return Result.Failure(Error.Conflict("download.not_publishable", $"DownloadResource '{Id}' cannot be published - it is currently '{Status}'."));
        }

        Status = SchedulableStatus.Published;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Archive(DateTimeOffset now)
    {
        if (Status != SchedulableStatus.Published)
        {
            return Result.Failure(Error.Conflict("download.not_published", $"DownloadResource '{Id}' cannot be archived - it is currently '{Status}'."));
        }

        Status = SchedulableStatus.Archived;
        UpdatedAt = now;
        return Result.Success();
    }
}
