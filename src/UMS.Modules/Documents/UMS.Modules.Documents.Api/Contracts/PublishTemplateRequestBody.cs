namespace UMS.Modules.Documents.Api.Contracts;

public sealed record PublishTemplateRequestBody(string DocumentType, string? LayoutAssetKey, IReadOnlyCollection<TemplateTranslationRequestBody> Translations);
