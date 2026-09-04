namespace UMS.Modules.Faculty.Domain.Translations;

/// <summary>English-fallback resolution helper, mirrors Organization's own <c>NameLocalizer</c> exactly (ADR-0011).</summary>
public static class TextLocalizer
{
    public const string EnglishLanguageCode = "en";

    public static string Resolve(string canonicalText, IEnumerable<TextTranslation> translations, string? requestedLanguageCode)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguageCode) || string.Equals(requestedLanguageCode, EnglishLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            return canonicalText;
        }

        var match = translations.FirstOrDefault(t => string.Equals(t.LanguageCode, requestedLanguageCode, StringComparison.OrdinalIgnoreCase));
        return match?.Text ?? canonicalText;
    }
}
