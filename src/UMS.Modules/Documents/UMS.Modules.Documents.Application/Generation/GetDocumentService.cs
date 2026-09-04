using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.GeneratedDocuments;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>DOC-10: metadata + short-lived presigned download URL, ownership-scoped (requirement-spec.md documents §6).</summary>
public sealed class GetDocumentService(IGeneratedDocumentRepository documents, IObjectStorage objectStorage)
{
    private static readonly TimeSpan DownloadUrlExpiry = TimeSpan.FromMinutes(15);

    public async Task<Result<GeneratedDocumentDto>> GetAsync(Guid id, Guid callerUserId, bool callerCanReadAny, CancellationToken cancellationToken = default)
    {
        var document = await documents.GetByIdAsync(new GeneratedDocumentId(id), cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return Error.NotFound("document.not_found", $"No GeneratedDocument exists with id '{id}'.");
        }

        // Ownership scoping (requirement-spec.md documents §6): the owner may always read their
        // own document; any other caller needs document.read against a broader scope - the
        // permission check itself (RequirePermission) already gated entry to this endpoint, so
        // callerCanReadAny distinguishes "read my own" from "read anyone's" within that.
        if (document.OwnerId != callerUserId && !callerCanReadAny)
        {
            return Error.Forbidden("document.forbidden", "You may only read your own documents.");
        }

        string? downloadUrl = null;
        if (document.Status == GeneratedDocumentStatus.Ready && document.StorageKey is not null)
        {
            downloadUrl = await objectStorage.GetDownloadUrlAsync(document.StorageKey, DownloadUrlExpiry, cancellationToken).ConfigureAwait(false);
        }

        return GeneratedDocumentDto.FromDomain(document, downloadUrl);
    }
}
