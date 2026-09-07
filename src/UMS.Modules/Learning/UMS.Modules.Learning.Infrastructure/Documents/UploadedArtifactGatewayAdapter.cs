using UMS.Modules.Learning.Application.Abstractions;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Learning.Infrastructure.Documents;

/// <summary>
/// LRN-5/LRN-13: adapts this module's own <see cref="IUploadedArtifactGateway"/> port onto
/// Documents' real, shared <see cref="IUploadedArtifactRequester"/> cross-module contract - the
/// same in-process adapter pattern Documents' own <c>NotificationRequestIntakeAdapter</c> uses in
/// the other direction, so Learning never takes a forbidden dependency on
/// <c>UMS.Modules.Documents.*</c> internals (module-boundaries.md, ADR-0002).
/// </summary>
internal sealed class UploadedArtifactGatewayAdapter(IUploadedArtifactRequester requester) : IUploadedArtifactGateway
{
    private const string ReadyStatus = "Ready";

    public async Task<Result<ArtifactUploadSlot>> RequestUploadAsync(Guid ownerUserId, string artifactType, string mimeType, CancellationToken cancellationToken = default)
    {
        var result = await requester
            .RequestUploadAsync(new RequestUploadedArtifactCommand(ownerUserId, artifactType, mimeType), cancellationToken)
            .ConfigureAwait(false);

        return result.Match<Result<ArtifactUploadSlot>>(
            slot => new ArtifactUploadSlot(slot.ArtifactId, slot.Status, slot.UploadUrl),
            error => error);
    }

    public async Task<Result<ArtifactConfirmation>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var result = await requester.ConfirmAsync(artifactId, cancellationToken).ConfigureAwait(false);

        return result.Match<Result<ArtifactConfirmation>>(
            reference => new ArtifactConfirmation(
                reference.ArtifactId,
                reference.Status,
                reference.MimeType,
                reference.SizeBytes,
                string.Equals(reference.Status, ReadyStatus, StringComparison.OrdinalIgnoreCase)),
            error => error);
    }
}
