using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Generation;

/// <summary>DOC-11: ownership-scoped filtered listing (requirement-spec.md documents §6).</summary>
public sealed class ListDocumentsService(IGeneratedDocumentRepository documents)
{
    public async Task<Result<IReadOnlyList<GeneratedDocumentDto>>> ListAsync(Guid? ownerId, DocumentType? documentType, Guid callerUserId, bool callerCanReadAny, CancellationToken cancellationToken = default)
    {
        if (!callerCanReadAny && ownerId != callerUserId)
        {
            return Error.Forbidden("document.forbidden", "You may only list your own documents.");
        }

        var effectiveOwnerId = callerCanReadAny ? ownerId : callerUserId;
        var results = await documents.ListAsync(effectiveOwnerId, documentType, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<GeneratedDocumentDto> dtos = results.Select(d => GeneratedDocumentDto.FromDomain(d, downloadUrl: null)).ToList();
        return Result.Success(dtos);
    }
}
