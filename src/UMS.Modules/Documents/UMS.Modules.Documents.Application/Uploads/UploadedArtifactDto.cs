using UMS.Modules.Documents.Domain.UploadedArtifacts;

namespace UMS.Modules.Documents.Application.Uploads;

public sealed record UploadedArtifactDto(
    Guid Id,
    Guid OwnerId,
    string ArtifactType,
    string MimeType,
    string Status,
    long? SizeBytes,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReadyAt,
    string? UploadUrl,
    string? DownloadUrl)
{
    public static UploadedArtifactDto FromDomain(UploadedArtifact artifact, string? uploadUrl, string? downloadUrl) => new(
        artifact.Id.Value,
        artifact.OwnerId,
        artifact.ArtifactType,
        artifact.MimeType,
        artifact.Status.ToString(),
        artifact.SizeBytes,
        artifact.RequestedAt,
        artifact.ReadyAt,
        uploadUrl,
        downloadUrl);
}
