using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Domain.Banners;

/// <summary>
/// CNT-8/CNT-9: glossary "a schedulable homepage promotional element." requirement-spec.md §2.3:
/// the IDENTICAL `publish_at`/`expire_at` scheduling mechanism as <see cref="Notices.Notice"/>
/// (reusing <see cref="SchedulableStatus"/>) - "one scheduling mechanism, applied to two entities,
/// not two bespoke implementations." Homepage-scoped only in this first pass, no translations (not
/// named as a localized entity in requirement-spec.md §2.3/§2.4).
///
/// <para>
/// Concurrency: the identical <see cref="AggregateRoot{TId}.Version"/> (`xmin`) optimistic-check
/// mechanism as Notice - see <see cref="Notices.Notice"/>'s own remarks, restated here per
/// design-decisions.md's "uniformly applied to every writer that mutates a Notice/Banner row."
/// </para>
///
/// <para>
/// Ordering: <see cref="IDisplayOrderable"/> (<see cref="SortOrder"/>/<see cref="CreatedAt"/>) -
/// see <see cref="ContentOrdering.ByDisplayOrder{T}(IEnumerable{T})"/> for the one shared tiebreaker
/// rule this shares with <see cref="HomepageSections.HomepageSection"/>.
/// </para>
/// </summary>
public sealed class Banner : AggregateRoot<BannerId>, IDisplayOrderable
{
    private Banner()
    {
    }

    private Banner(BannerId id, string headline, string imageUrl, string? linkUrl, int sortOrder, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        Headline = headline;
        ImageUrl = imageUrl;
        LinkUrl = linkUrl;
        SortOrder = sortOrder;
        Status = SchedulableStatus.Draft;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Headline { get; private set; } = string.Empty;

    public string ImageUrl { get; private set; } = string.Empty;

    public string? LinkUrl { get; private set; }

    public int SortOrder { get; private set; }

    public SchedulableStatus Status { get; private set; }

    public DateTimeOffset? PublishAt { get; private set; }

    public DateTimeOffset? ExpireAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<Banner> Create(string headline, string imageUrl, string? linkUrl, int sortOrder, Guid createdByUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(headline))
        {
            return Error.Validation("banner.headline_required", "A Banner requires a headline.");
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Error.Validation("banner.image_required", "A Banner requires an image reference.");
        }

        return new Banner(BannerId.New(), headline.Trim(), imageUrl.Trim(), string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim(), sortOrder, createdByUserId, now);
    }

    public Result UpdateDetails(string headline, string imageUrl, string? linkUrl, int sortOrder, DateTimeOffset now)
    {
        if (Status == SchedulableStatus.Archived)
        {
            return Result.Failure(Error.Conflict("banner.archived", "An Archived Banner can no longer be edited."));
        }

        if (string.IsNullOrWhiteSpace(headline))
        {
            return Result.Failure(Error.Validation("banner.headline_required", "A Banner requires a headline."));
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return Result.Failure(Error.Validation("banner.image_required", "A Banner requires an image reference."));
        }

        Headline = headline.Trim();
        ImageUrl = imageUrl.Trim();
        LinkUrl = string.IsNullOrWhiteSpace(linkUrl) ? null : linkUrl.Trim();
        SortOrder = sortOrder;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result UpdateSchedule(DateTimeOffset? publishAt, DateTimeOffset? expireAt, DateTimeOffset now)
    {
        if (Status is SchedulableStatus.Published or SchedulableStatus.Archived)
        {
            return Result.Failure(Error.Conflict("banner.not_schedulable", $"Banner '{Id}' cannot have its schedule changed - it is currently '{Status}'."));
        }

        if (publishAt is null && expireAt is not null)
        {
            return Result.Failure(Error.Validation("banner.expire_without_publish", "An expire_at cannot be set without a publish_at."));
        }

        if (publishAt is not null && expireAt is not null && expireAt <= publishAt)
        {
            return Result.Failure(Error.Validation("banner.invalid_window", "expire_at must be strictly after publish_at."));
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
            return Result.Failure(Error.Conflict("banner.not_draft", $"Banner '{Id}' cannot be scheduled - it is currently '{Status}'."));
        }

        if (PublishAt is null)
        {
            return Result.Failure(Error.Validation("banner.publish_at_required", "A publish_at is required to schedule a Banner."));
        }

        Status = SchedulableStatus.Scheduled;
        UpdatedAt = now;
        return Result.Success();
    }

    public Result Publish(DateTimeOffset now)
    {
        if (Status is not (SchedulableStatus.Draft or SchedulableStatus.Scheduled))
        {
            return Result.Failure(Error.Conflict("banner.not_publishable", $"Banner '{Id}' cannot be published - it is currently '{Status}'."));
        }

        Status = SchedulableStatus.Published;
        UpdatedAt = now;
        Raise(new BannerPublished(Id.Value, now));
        return Result.Success();
    }

    public Result Archive(DateTimeOffset now)
    {
        if (Status != SchedulableStatus.Published)
        {
            return Result.Failure(Error.Conflict("banner.not_published", $"Banner '{Id}' cannot be archived - it is currently '{Status}'."));
        }

        Status = SchedulableStatus.Archived;
        UpdatedAt = now;
        return Result.Success();
    }
}
