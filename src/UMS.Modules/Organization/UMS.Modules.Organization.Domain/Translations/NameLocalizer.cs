namespace UMS.Modules.Organization.Domain.Translations;

/// <summary>
/// ADR-0011's "every query that renders user-facing content must explicitly resolve the caller's
/// language against the translation table (with a defined fallback language, e.g. English)" -
/// this is that one shared resolution helper, called from every read path (application-service
/// DTO mapping) that returns a Faculty/Department/Program name or a Designation title, so the
/// fallback rule can never drift between endpoints.
/// </summary>
public static class NameLocalizer
{
    public const string EnglishLanguageCode = "en";

    /// <summary>
    /// Resolves <paramref name="requestedLanguageCode"/> against <paramref name="translations"/>,
    /// falling back to <paramref name="canonicalName"/> (the base entity's own English name) when
    /// no requested language was given, the requested language is English itself, or no
    /// translation row exists for it (ADR-0011: "a defined fallback language, e.g. English").
    /// </summary>
    public static string Resolve(string canonicalName, IEnumerable<NameTranslation> translations, string? requestedLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguageCode) ||
            string.Equals(requestedLanguageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return canonicalName;
        }

        var match = translations.FirstOrDefault(t =>
            string.Equals(t.LanguageCode, requestedLanguageCode, StringComparison.OrdinalIgnoreCase));

        return match?.Name ?? canonicalName;
    }
}
