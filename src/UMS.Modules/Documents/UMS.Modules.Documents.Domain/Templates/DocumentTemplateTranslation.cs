namespace UMS.Modules.Documents.Domain.Templates;

/// <summary>
/// One language's localizable text for a <see cref="DocumentTemplate"/> (ADR-0011's
/// <c>{table}_translations</c> pattern, keyed <c>(entity_id, language_code)</c> -
/// ums-conventions.md, Localization Implementation). <see cref="LabelsJson"/> holds the
/// template's static chrome/field-label text as a flat string-to-string JSON map (e.g.
/// <c>{"studentName":"Student Name"}</c> in English, <c>{"studentName":"শিক্ষার্থীর নাম"}</c> in
/// Bengali) - the renderer looks up each rendered field's label from this map for the request's
/// resolved language, falling back to English server-side (never resolved client-side, per the
/// same ADR-0011 rule Organization's own translation tables follow).
/// </summary>
public sealed class DocumentTemplateTranslation
{
    private DocumentTemplateTranslation()
    {
    }

    public DocumentTemplateId TemplateId { get; private init; }

    public Common.LanguageCode Language { get; private init; }

    public string Title { get; private init; } = string.Empty;

    public string LabelsJson { get; private init; } = "{}";

    internal static DocumentTemplateTranslation Create(DocumentTemplateId templateId, Common.LanguageCode language, string title, string labelsJson) => new()
    {
        TemplateId = templateId,
        Language = language,
        Title = title,
        LabelsJson = labelsJson,
    };
}
