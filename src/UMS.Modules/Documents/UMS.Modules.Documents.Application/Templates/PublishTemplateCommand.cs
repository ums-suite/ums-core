using UMS.Modules.Documents.Domain.Common;

namespace UMS.Modules.Documents.Application.Templates;

public sealed record PublishTemplateCommand(DocumentType DocumentType, string? LayoutAssetKey, IReadOnlyCollection<PublishTemplateTranslationInput> Translations);
