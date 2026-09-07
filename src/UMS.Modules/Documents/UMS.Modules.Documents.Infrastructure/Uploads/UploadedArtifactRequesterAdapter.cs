using UMS.Modules.Documents.Application.Uploads;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Infrastructure.Uploads;

/// <summary>
/// The one real implementation of <see cref="IUploadedArtifactRequester"/>
/// (release/DEVELOPMENT_PLAN.md Flow #13, LRN-5/LRN-13) - delegates straight to Documents' own
/// <see cref="UploadedArtifactService"/>, the identical in-process shared-interface pattern
/// <c>UMS.Modules.Documents.Infrastructure.Student.DocumentGenerationRequesterAdapter</c> already
/// established for Student's Flow #11 call site, so Learning never takes a forbidden dependency on
/// <c>UMS.Modules.Documents.*</c> internals.
///
/// <para>
/// Documents' own HTTP upload endpoints (<c>POST /api/v1/documents/uploads</c>, <c>.../confirm</c>)
/// and their <c>document.document.generate</c> gate are deliberately left untouched by this
/// adapter - see <see cref="IUploadedArtifactRequester"/>'s own remarks for why a new in-process
/// contract, not a loosened permission, is the right shape here.
/// </para>
/// </summary>
internal sealed class UploadedArtifactRequesterAdapter(UploadedArtifactService uploadedArtifacts) : IUploadedArtifactRequester
{
    public async Task<Result<UploadedArtifactSlot>> RequestUploadAsync(RequestUploadedArtifactCommand command, CancellationToken cancellationToken = default)
    {
        var result = await uploadedArtifacts
            .RequestUploadAsync(command.OwnerId, command.ArtifactType, command.MimeType, cancellationToken)
            .ConfigureAwait(false);

        return result.Match<Result<UploadedArtifactSlot>>(
            dto => new UploadedArtifactSlot(dto.Id, dto.Status, dto.UploadUrl),
            error => error);
    }

    public async Task<Result<UploadedArtifactReference>> ConfirmAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var result = await uploadedArtifacts.ConfirmAsync(artifactId, cancellationToken).ConfigureAwait(false);

        return result.Match<Result<UploadedArtifactReference>>(
            dto => new UploadedArtifactReference(dto.Id, dto.Status, dto.MimeType, dto.SizeBytes),
            error => error);
    }
}
