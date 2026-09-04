namespace UMS.Modules.Organization.Domain.Translations;

/// <summary>
/// ORG-8/ADR-0011: one `(languageCode, name)` row in a `{table}_translations` companion table.
/// The base entity's own `Name`/`Title` column is always the English/canonical value (used for
/// uniqueness constraints, audit entries, and the English-fallback read path) - a translation row
/// exists only for a *non*-English override, e.g. `bn`. Reused as the owned-collection element
/// type for Faculty/Department/Program/Designation (each gets its own physical
/// `{table}_translations` table via its own `OwnsMany` configuration - ADR-0011: "the base entity
/// holds language-independent fields, the translation table holds every localizable field").
/// </summary>
public sealed class NameTranslation
{
    private NameTranslation()
    {
    }

    private NameTranslation(string languageCode, string name)
    {
        LanguageCode = languageCode;
        Name = name;
    }

    public string LanguageCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public static NameTranslation Create(string languageCode, string name)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("A translation's language code is required.", nameof(languageCode));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A translation's name is required.", nameof(name));
        }

        return new NameTranslation(languageCode.Trim().ToLowerInvariant(), name.Trim());
    }

    internal void UpdateName(string name) => Name = name.Trim();
}
