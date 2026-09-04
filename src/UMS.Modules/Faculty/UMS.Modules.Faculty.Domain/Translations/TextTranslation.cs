namespace UMS.Modules.Faculty.Domain.Translations;

/// <summary>
/// ADR-0011's `{table}_translations` pattern applied to a free-text field (LeaveRequest's
/// <c>Reason</c>) rather than a short name - same shape as Organization's own
/// <c>NameTranslation</c>, duplicated here per this codebase's current per-module-copy convention
/// (Organization/Identity/Notifications each keep their own small value objects rather than a
/// shared kernel beyond `UMS.Shared.Domain`'s primitives).
/// </summary>
public sealed class TextTranslation
{
    private TextTranslation()
    {
    }

    private TextTranslation(string languageCode, string text)
    {
        LanguageCode = languageCode;
        Text = text;
    }

    public string LanguageCode { get; private set; } = string.Empty;

    public string Text { get; private set; } = string.Empty;

    public static TextTranslation Create(string languageCode, string text)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
        {
            throw new ArgumentException("Language code is required.", nameof(languageCode));
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Translated text is required.", nameof(text));
        }

        return new TextTranslation(languageCode.Trim().ToLowerInvariant(), text.Trim());
    }

    internal void UpdateText(string text) => Text = text.Trim();
}
