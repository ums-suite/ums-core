using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Domain.Downloads;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Application.Downloads;

/// <summary>
/// CNT-11: categorized metadata + a `Documents` file reference (requirement-spec.md §2.6) - "the
/// exact pattern Learning already established as this contract's first real caller"
/// (<see cref="IUploadedArtifactRequester"/>). Reuses Notice/Banner's identical scheduling
/// mechanism.
/// </summary>
public sealed class DownloadResourceService(IDownloadResourceRepository downloads, IUnitOfWork unitOfWork, IUploadedArtifactRequester artifacts, IClock clock)
{
    public static DownloadResourceDto ToDto(DownloadResource resource) => new(
        resource.Id.Value,
        resource.Title,
        resource.Category,
        resource.ArtifactId,
        resource.Status.ToString(),
        resource.PublishAt,
        resource.ExpireAt,
        resource.CreatedAt,
        resource.UpdatedAt,
        resource.Version);

    /// <summary>
    /// Admin upload metadata (`POST /content/downloads`): confirms the artifact Documents already
    /// has a `PendingUpload`/`Ready` row for (the caller uploaded the bytes first via
    /// <see cref="IUploadedArtifactRequester.RequestUploadAsync"/>/<c>.../ConfirmAsync</c>, both
    /// already exercised by the calling Admin's own upload flow) before creating the metadata row -
    /// Content never stores the blob itself.
    /// </summary>
    public async Task<Result<DownloadResourceDto>> CreateAsync(string title, string category, Guid artifactId, Guid createdByUserId, CancellationToken cancellationToken = default)
    {
        var confirmed = await artifacts.ConfirmAsync(artifactId, cancellationToken).ConfigureAwait(false);
        if (confirmed.IsFailure)
        {
            return confirmed.Error!;
        }

        if (!string.Equals(confirmed.Value.Status, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            return Error.Conflict("download.artifact_not_ready", $"Artifact '{artifactId}' is not yet Ready (status: '{confirmed.Value.Status}') - the file upload must complete before a DownloadResource can reference it.");
        }

        var created = DownloadResource.Create(title, category, artifactId, createdByUserId, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        downloads.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<DownloadResourceDto>> UpdateMetadataAsync(Guid id, string title, string category, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var resource = await downloads.GetByIdAsync(new DownloadResourceId(id), cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return Error.NotFound("download.not_found", $"No DownloadResource exists with id '{id}'.");
        }

        var updated = resource.UpdateMetadata(title, category, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(resource, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("download.concurrency_conflict", ex.Message);
        }

        return ToDto(resource);
    }

    public async Task<Result<DownloadResourceDto>> UpdateScheduleAsync(Guid id, DateTimeOffset? publishAt, DateTimeOffset? expireAt, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var resource = await downloads.GetByIdAsync(new DownloadResourceId(id), cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return Error.NotFound("download.not_found", $"No DownloadResource exists with id '{id}'.");
        }

        var updated = resource.UpdateSchedule(publishAt, expireAt, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(resource, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("download.concurrency_conflict", ex.Message);
        }

        return ToDto(resource);
    }

    public async Task<Result<DownloadResourceDto>> ScheduleAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var resource = await downloads.GetByIdAsync(new DownloadResourceId(id), cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return Error.NotFound("download.not_found", $"No DownloadResource exists with id '{id}'.");
        }

        var scheduled = resource.Schedule(clock.UtcNow);
        if (scheduled.IsFailure)
        {
            return scheduled.Error!;
        }

        unitOfWork.SetExpectedVersion(resource, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("download.concurrency_conflict", ex.Message);
        }

        return ToDto(resource);
    }

    public async Task<Result<DownloadResourceDto>> PublishAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var resource = await downloads.GetByIdAsync(new DownloadResourceId(id), cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return Error.NotFound("download.not_found", $"No DownloadResource exists with id '{id}'.");
        }

        var published = resource.Publish(clock.UtcNow);
        if (published.IsFailure)
        {
            return published.Error!;
        }

        unitOfWork.SetExpectedVersion(resource, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("download.concurrency_conflict", ex.Message);
        }

        return ToDto(resource);
    }

    public async Task<Result<DownloadResourceDto>> ArchiveAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var resource = await downloads.GetByIdAsync(new DownloadResourceId(id), cancellationToken).ConfigureAwait(false);
        if (resource is null)
        {
            return Error.NotFound("download.not_found", $"No DownloadResource exists with id '{id}'.");
        }

        var archived = resource.Archive(clock.UtcNow);
        if (archived.IsFailure)
        {
            return archived.Error!;
        }

        unitOfWork.SetExpectedVersion(resource, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("download.concurrency_conflict", ex.Message);
        }

        return ToDto(resource);
    }

    public async Task<DownloadResourceListPage> ListPublishedAsync(string? category, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await downloads.ListPublishedAsync(category, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await downloads.CountPublishedAsync(category, cancellationToken).ConfigureAwait(false);
        return new DownloadResourceListPage(items.Select(ToDto).ToList(), total, skip, take);
    }
}
