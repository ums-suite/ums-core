using System.Text.Json;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Domain.Common;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Application.Banners;

/// <summary>
/// CNT-8/CNT-9: Banner CRUD + the identical scheduling/concurrency mechanism as Notice
/// (requirement-spec.md §2.3) - see <see cref="Domain.Banners.Banner"/>'s own remarks.
/// </summary>
public sealed class BannerService(IBannerRepository banners, IUnitOfWork unitOfWork, IAuditRecorder auditRecorder, ICacheInvalidator cacheInvalidator, IClock clock)
{
    public static BannerDto ToDto(Domain.Banners.Banner banner) => new(
        banner.Id.Value,
        banner.Headline,
        banner.ImageUrl,
        banner.LinkUrl,
        banner.SortOrder,
        banner.Status.ToString(),
        banner.PublishAt,
        banner.ExpireAt,
        banner.CreatedAt,
        banner.UpdatedAt,
        banner.Version);

    public async Task<Result<BannerDto>> CreateAsync(string headline, string imageUrl, string? linkUrl, int sortOrder, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var created = Domain.Banners.Banner.Create(headline, imageUrl, linkUrl, sortOrder, actorUserId, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        banners.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<BannerDto>> UpdateDetailsAsync(Guid id, string headline, string imageUrl, string? linkUrl, int sortOrder, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var banner = await banners.GetByIdAsync(new Domain.Banners.BannerId(id), cancellationToken).ConfigureAwait(false);
        if (banner is null)
        {
            return Error.NotFound("banner.not_found", $"No Banner exists with id '{id}'.");
        }

        var updated = banner.UpdateDetails(headline, imageUrl, linkUrl, sortOrder, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(banner, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("banner.concurrency_conflict", ex.Message);
        }

        return ToDto(banner);
    }

    public async Task<Result<BannerDto>> UpdateScheduleAsync(Guid id, DateTimeOffset? publishAt, DateTimeOffset? expireAt, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var banner = await banners.GetByIdAsync(new Domain.Banners.BannerId(id), cancellationToken).ConfigureAwait(false);
        if (banner is null)
        {
            return Error.NotFound("banner.not_found", $"No Banner exists with id '{id}'.");
        }

        var updated = banner.UpdateSchedule(publishAt, expireAt, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(banner, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("banner.concurrency_conflict", ex.Message);
        }

        return ToDto(banner);
    }

    public async Task<Result<BannerDto>> ScheduleAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var banner = await banners.GetByIdAsync(new Domain.Banners.BannerId(id), cancellationToken).ConfigureAwait(false);
        if (banner is null)
        {
            return Error.NotFound("banner.not_found", $"No Banner exists with id '{id}'.");
        }

        var scheduled = banner.Schedule(clock.UtcNow);
        if (scheduled.IsFailure)
        {
            return scheduled.Error!;
        }

        unitOfWork.SetExpectedVersion(banner, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("banner.concurrency_conflict", ex.Message);
        }

        return ToDto(banner);
    }

    /// <summary>CNT-9: manual immediate publish, audited (design-decisions.md "Audit-Write Synchronicity" - restated for Banner), followed by a best-effort cache-invalidation attempt (the <see cref="Domain.Events.BannerPublished"/> domain event is the cache-invalidation trigger).</summary>
    public async Task<Result<BannerDto>> PublishAsync(Guid id, AuditContext audit, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var banner = await banners.GetByIdAsync(new Domain.Banners.BannerId(id), cancellationToken).ConfigureAwait(false);
        if (banner is null)
        {
            return Error.NotFound("banner.not_found", $"No Banner exists with id '{id}'.");
        }

        var statusBefore = banner.Status;
        var published = banner.Publish(clock.UtcNow);
        if (published.IsFailure)
        {
            return published.Error!;
        }

        unitOfWork.SetExpectedVersion(banner, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Banner", id.ToString(), AuditActions.Publish, JsonSerializer.Serialize(new { status = statusBefore.ToString() }), "{\"status\":\"Published\"}");
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        await cacheInvalidator.InvalidateAsync("content:banners:active", cancellationToken).ConfigureAwait(false);
        return ToDto(banner);
    }

    public async Task<Result<BannerDto>> ArchiveAsync(Guid id, AuditContext audit, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var banner = await banners.GetByIdAsync(new Domain.Banners.BannerId(id), cancellationToken).ConfigureAwait(false);
        if (banner is null)
        {
            return Error.NotFound("banner.not_found", $"No Banner exists with id '{id}'.");
        }

        var archived = banner.Archive(clock.UtcNow);
        if (archived.IsFailure)
        {
            return archived.Error!;
        }

        unitOfWork.SetExpectedVersion(banner, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Banner", id.ToString(), AuditActions.Update, "{\"status\":\"Published\"}", "{\"status\":\"Archived\"}");
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        await cacheInvalidator.InvalidateAsync("content:banners:active", cancellationToken).ConfigureAwait(false);
        return ToDto(banner);
    }

    /// <summary>Public read: currently-Published banners in <see cref="ContentOrdering.ByDisplayOrder{T}(IEnumerable{T})"/> order.</summary>
    public async Task<IReadOnlyList<BannerDto>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        (await banners.ListActiveOrderedAsync(cancellationToken).ConfigureAwait(false)).ByDisplayOrder().Select(ToDto).ToList();

    public async Task<Result<BannerDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var banner = await banners.GetByIdAsync(new Domain.Banners.BannerId(id), cancellationToken).ConfigureAwait(false);
        return banner is null
            ? Error.NotFound("banner.not_found", $"No Banner exists with id '{id}'.")
            : ToDto(banner);
    }
}
