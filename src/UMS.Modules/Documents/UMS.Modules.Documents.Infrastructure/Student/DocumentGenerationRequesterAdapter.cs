using UMS.Modules.Documents.Application.Generation;
using UMS.Modules.Documents.Domain.Common;
using UMS.Shared.Documents;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Infrastructure.Student;

/// <summary>
/// The one real implementation of <see cref="IDocumentGenerationRequester"/> (release/
/// DEVELOPMENT_PLAN.md Flow #11, STU-3) - delegates to Documents' own <see cref="GenerateDocumentService"/>,
/// the same in-process, shared-interface pattern <c>UMS.Modules.Documents.Infrastructure.Notifications.NotificationRequestIntakeAdapter</c>
/// already established for outbound calls, applied here to an inbound one, so Student never takes
/// a forbidden dependency on <c>UMS.Modules.Documents.*</c> internals.
/// </summary>
internal sealed class DocumentGenerationRequesterAdapter(GenerateDocumentService generateDocumentService) : IDocumentGenerationRequester
{
    public async Task<Result<GeneratedDocumentSummary>> RequestAsync(RequestDocumentGenerationCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<DocumentType>(command.DocumentType, ignoreCase: true, out var documentType))
        {
            return Error.Validation("document.invalid_document_type", $"'{command.DocumentType}' is not a valid Documents document type.");
        }

        LanguageCode? language = null;
        if (!string.IsNullOrWhiteSpace(command.LanguageCode))
        {
            if (!Enum.TryParse<LanguageCode>(command.LanguageCode, ignoreCase: true, out var parsedLanguage))
            {
                return Error.Validation("document.invalid_language_code", $"'{command.LanguageCode}' is not a valid Documents language code.");
            }

            language = parsedLanguage;
        }

        var generateCommand = new GenerateDocumentCommand(
            command.OwnerId,
            documentType,
            command.SourceReferenceId,
            command.Fields,
            language,
            command.RequestedByUserId ?? command.OwnerId,
            command.CorrelationId);

        var result = await generateDocumentService.GenerateAsync(generateCommand, cancellationToken).ConfigureAwait(false);
        return result.Match<Result<GeneratedDocumentSummary>>(
            dto => new GeneratedDocumentSummary(dto.Id, dto.Status, dto.DownloadUrl),
            error => error);
    }
}
