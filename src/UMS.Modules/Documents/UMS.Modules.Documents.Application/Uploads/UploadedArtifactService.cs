using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.UploadedArtifacts;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Uploads;

/// <summary>
/// requirement-spec.md documents §2 Uploaded Artifact Storage / §6's three upload endpoints:
/// request a presigned upload URL, confirm completion (triggering existence/checksum verification),
/// and read back metadata + a presigned download URL. Never accepts the raw upload stream itself
/// (§1 Scope: virus-scanning and the actual byte transfer stay the calling module's/client's own
/// concern - Documents only brokers the presigned URL and verifies the result).
/// </summary>
public sealed class UploadedArtifactService(
    IUploadedArtifactRepository artifacts,
    IUnitOfWork unitOfWork,
    IObjectStorage objectStorage,
    IClock clock)
{
    private static readonly TimeSpan UploadUrlExpiry = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DownloadUrlExpiry = TimeSpan.FromMinutes(15);

    public async Task<Result<UploadedArtifactDto>> RequestUploadAsync(Guid ownerId, string artifactType, string mimeType, CancellationToken cancellationToken = default)
    {
        var objectKey = $"uploaded-artifacts/{artifactType.ToLowerInvariant()}/{Guid.NewGuid()}";

        var created = UploadedArtifact.RequestUpload(ownerId, artifactType, mimeType, objectKey, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        artifacts.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var uploadUrl = await objectStorage.GetUploadUrlAsync(objectKey, mimeType, UploadUrlExpiry, cancellationToken).ConfigureAwait(false);
        return UploadedArtifactDto.FromDomain(created.Value, uploadUrl, downloadUrl: null);
    }

    public async Task<Result<UploadedArtifactDto>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = await artifacts.GetByIdAsync(new UploadedArtifactId(artifactId), cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return Error.NotFound("uploaded_artifact.not_found", $"No UploadedArtifact exists with id '{artifactId}'.");
        }

        var metadata = await objectStorage.TryGetMetadataAsync(artifact.StorageKey, cancellationToken).ConfigureAwait(false);
        if (metadata is null)
        {
            var failed = artifact.MarkFailed("No object was found at the expected storage key - the upload may not have completed.");
            if (failed.IsFailure)
            {
                return failed.Error!;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return UploadedArtifactDto.FromDomain(artifact, uploadUrl: null, downloadUrl: null);
        }

        var confirmed = artifact.Confirm(metadata.ETag.Trim('"'), metadata.SizeBytes, clock.UtcNow);
        if (confirmed.IsFailure)
        {
            return confirmed.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return UploadedArtifactDto.FromDomain(artifact, uploadUrl: null, downloadUrl: null);
    }

    public async Task<Result<UploadedArtifactDto>> GetAsync(Guid artifactId, Guid callerUserId, bool callerCanReadAny, CancellationToken cancellationToken = default)
    {
        var artifact = await artifacts.GetByIdAsync(new UploadedArtifactId(artifactId), cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            return Error.NotFound("uploaded_artifact.not_found", $"No UploadedArtifact exists with id '{artifactId}'.");
        }

        if (artifact.OwnerId != callerUserId && !callerCanReadAny)
        {
            return Error.Forbidden("uploaded_artifact.forbidden", "You may only read your own uploaded artifacts.");
        }

        string? downloadUrl = null;
        if (artifact.Status == UploadedArtifactStatus.Ready)
        {
            downloadUrl = await objectStorage.GetDownloadUrlAsync(artifact.StorageKey, DownloadUrlExpiry, cancellationToken).ConfigureAwait(false);
        }

        return UploadedArtifactDto.FromDomain(artifact, uploadUrl: null, downloadUrl);
    }
}
