namespace UMS.Modules.Documents.Domain.Common;

public static class LanguageCodeExtensions
{
    /// <summary>English is the universal fallback (ADR-0011) - never a hardcoded string at every call site.</summary>
    public const LanguageCode Fallback = LanguageCode.En;

    public static string ToCode(this LanguageCode language) => language switch
    {
        LanguageCode.En => "en",
        LanguageCode.Bn => "bn",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, "Unrecognized LanguageCode."),
    };

    public static LanguageCode? TryParse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "en" => LanguageCode.En,
        "bn" => LanguageCode.Bn,
        _ => null,
    };
}
