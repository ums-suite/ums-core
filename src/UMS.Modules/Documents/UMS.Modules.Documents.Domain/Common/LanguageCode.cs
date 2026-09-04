namespace UMS.Modules.Documents.Domain.Common;

/// <summary>
/// ADR-0011's translation-table pattern (ums-conventions.md, Localization Implementation): every
/// module reuses the same <c>{table}_translations</c> keyed <c>(entity_id, language_code)</c>
/// convention. Documents' only bilingual entity is <see cref="Templates.DocumentTemplate"/> - binary
/// layout/branding assets are template-versioned, not per-language (requirement-spec.md documents
/// §2).
/// </summary>
public enum LanguageCode
{
    En = 0,
    Bn = 1,
}
