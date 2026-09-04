using UMS.Modules.Documents.Application.Abstractions;
using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Application.Templates;

/// <summary>DOC-1: publish (create) and read <see cref="DocumentTemplate"/> versions - Admin-managed (requirement-spec.md documents §2/§6).</summary>
public sealed class DocumentTemplateService(IDocumentTemplateRepository templates, IUnitOfWork unitOfWork, IClock clock)
{
    public async Task<Result<DocumentTemplateDto>> PublishAsync(PublishTemplateCommand command, CancellationToken cancellationToken = default)
    {
        var translations = new Dictionary<LanguageCode, (string Title, string LabelsJson)>();
        foreach (var t in command.Translations)
        {
            var language = LanguageCodeExtensions.TryParse(t.Language);
            if (language is null)
            {
                return Error.Validation("document_template.language_invalid", $"'{t.Language}' is not a recognized language code - use 'en' or 'bn'.");
            }

            translations[language.Value] = (t.Title, t.LabelsJson);
        }

        // design-decisions.md's template-version-pinning decision applies verbatim to publishing
        // itself too (edge-cases.md's "DocumentTemplate Published at the Exact Instant of
        // BulkGenerationJob Creation"): resolving "the next version number" and inserting the new
        // row happen back-to-back against the same repository/unit-of-work, and the
        // (DocumentType, Version) database unique constraint is the final backstop against two
        // concurrent publishes both reading the same "latest version" and racing to claim it.
        var nextVersion = await templates.GetLatestVersionAsync(command.DocumentType, cancellationToken).ConfigureAwait(false) + 1;

        var created = DocumentTemplate.Create(command.DocumentType, nextVersion, command.LayoutAssetKey, translations, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        templates.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return DocumentTemplateDto.FromDomain(created.Value);
    }

    public async Task<Result<DocumentTemplateDto>> GetCurrentAsync(DocumentType documentType, CancellationToken cancellationToken = default)
    {
        var template = await templates.GetCurrentAsync(documentType, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            return Error.NotFound("document_template.not_found", $"No published DocumentTemplate exists for type '{documentType}'.");
        }

        return DocumentTemplateDto.FromDomain(template);
    }

    public async Task<IReadOnlyList<DocumentTemplateDto>> ListAsync(DocumentType? documentType, CancellationToken cancellationToken = default)
    {
        var results = await templates.ListAsync(documentType, cancellationToken).ConfigureAwait(false);
        return results.Select(DocumentTemplateDto.FromDomain).ToList();
    }
}
