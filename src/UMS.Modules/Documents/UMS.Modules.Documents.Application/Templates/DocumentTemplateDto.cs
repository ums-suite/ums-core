using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.Application.Templates;

public sealed record DocumentTemplateDto(
    Guid Id,
    string DocumentType,
    int Version,
    string? LayoutAssetKey,
    DateTimeOffset PublishedAt,
    IReadOnlyCollection<DocumentTemplateTranslationDto> Translations)
{
    public static DocumentTemplateDto FromDomain(DocumentTemplate template) => new(
        template.Id.Value,
        template.DocumentType.ToString(),
        template.Version,
        template.LayoutAssetKey,
        template.PublishedAt,
        template.Translations.Select(t => new DocumentTemplateTranslationDto(t.Language.ToCode(), t.Title, t.LabelsJson)).ToList());
}
