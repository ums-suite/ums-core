using UMS.Modules.Documents.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Documents.Domain.Templates;

/// <summary>
/// DOC-1: a named, versioned, bilingual layout for one <see cref="DocumentType"/>
/// (requirement-spec.md documents §2 Template Management, §3). Publishing a new version never
/// mutates an existing one - there is no "edit" method anywhere on this class, by construction:
/// each <see cref="Create"/> call produces a brand-new, immutable row, and "the current template
/// for a type" is resolved (by the repository/application layer, not here) as the highest
/// <see cref="Version"/> among templates sharing a <see cref="DocumentType"/>. Every
/// <c>GeneratedDocument</c> records the exact <see cref="Id"/> + <see cref="Version"/> it was
/// rendered from (§2), so a later publish can never retroactively change what an already-rendered
/// document says it was rendered from.
/// </summary>
public sealed class DocumentTemplate
{
    private readonly List<DocumentTemplateTranslation> _translations = [];

    private DocumentTemplate()
    {
    }

    public DocumentTemplateId Id { get; private init; }

    public DocumentType DocumentType { get; private init; }

    /// <summary>1-based, monotonically increasing per <see cref="DocumentType"/> - never reused, never decremented.</summary>
    public int Version { get; private init; }

    /// <summary>
    /// A template-versioned (not per-language, requirement-spec.md documents §2) reference to a
    /// branding/layout asset in object storage - e.g. a university crest image. Optional: a
    /// template with no distinct branding asset renders using the renderer's own default chrome.
    /// </summary>
    public string? LayoutAssetKey { get; private init; }

    public DateTimeOffset PublishedAt { get; private init; }

    public IReadOnlyCollection<DocumentTemplateTranslation> Translations => _translations;

    public static Result<DocumentTemplate> Create(
        DocumentType documentType,
        int version,
        string? layoutAssetKey,
        IReadOnlyDictionary<LanguageCode, (string Title, string LabelsJson)> translations,
        DateTimeOffset now)
    {
        if (version < 1)
        {
            return Error.Validation("document_template.version_invalid", "Template version must be a positive, monotonically increasing integer.");
        }

        if (!translations.ContainsKey(LanguageCodeExtensions.Fallback))
        {
            return Error.Validation("document_template.english_required", "An English translation is required - it is the universal fallback (ADR-0011).");
        }

        foreach (var (language, content) in translations)
        {
            if (string.IsNullOrWhiteSpace(content.Title))
            {
                return Error.Validation("document_template.title_required", $"A title is required for the '{language}' translation.");
            }
        }

        var template = new DocumentTemplate
        {
            Id = DocumentTemplateId.New(),
            DocumentType = documentType,
            Version = version,
            LayoutAssetKey = string.IsNullOrWhiteSpace(layoutAssetKey) ? null : layoutAssetKey,
            PublishedAt = now,
        };

        foreach (var (language, content) in translations)
        {
            template._translations.Add(DocumentTemplateTranslation.Create(template.Id, language, content.Title, content.LabelsJson));
        }

        return template;
    }

    /// <summary>Resolves a translation for the caller's preferred language, falling back to English server-side (ADR-0011) - never resolved client-side.</summary>
    public DocumentTemplateTranslation ResolveTranslation(LanguageCode preferred)
    {
        return _translations.FirstOrDefault(t => t.Language == preferred)
            ?? _translations.First(t => t.Language == LanguageCodeExtensions.Fallback);
    }
}
