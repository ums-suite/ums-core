namespace UMS.Modules.Documents.Api.Contracts;

public sealed record TemplateTranslationRequestBody(string Language, string Title, string LabelsJson);
