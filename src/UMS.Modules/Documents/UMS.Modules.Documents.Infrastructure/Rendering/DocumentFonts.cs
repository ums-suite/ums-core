using System.Reflection;
using QuestPDF.Drawing;

namespace UMS.Modules.Documents.Infrastructure.Rendering;

/// <summary>
/// edge-cases.md's Bengali-name-rendering edge case: "requires a properly embedded Bengali-capable
/// font in the rendering engine - treated as a release-blocking check for every document type, not
/// a cosmetic nice-to-have" (ADR-0011's Unicode round-trip requirement). Fonts are embedded
/// resources in this assembly (Noto Sans / Noto Sans Bengali, SIL Open Font License 1.1 - free,
/// no attribution-in-output requirement, safe to bundle in a compiled binary) rather than relying
/// on whatever fonts happen to be installed on a given container image, so Bengali conjuncts and
/// diacritics render identically in every environment.
/// </summary>
internal static class DocumentFonts
{
    public const string EnglishFamily = "Noto Sans";
    public const string BengaliFamily = "Noto Sans Bengali";

    private static readonly object SyncRoot = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
        {
            return;
        }

        lock (SyncRoot)
        {
            if (_registered)
            {
                return;
            }

            RegisterEmbeddedFont("NotoSans-Regular.ttf");
            RegisterEmbeddedFont("NotoSans-Bold.ttf");
            RegisterEmbeddedFont("NotoSansBengali-Regular.ttf");
            RegisterEmbeddedFont("NotoSansBengali-Bold.ttf");

            _registered = true;
        }
    }

    private static void RegisterEmbeddedFont(string fileName)
    {
        const string resourcePrefix = "UMS.Modules.Documents.Infrastructure.Assets.Fonts.";
        var assembly = typeof(DocumentFonts).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourcePrefix + fileName)
            ?? throw new InvalidOperationException($"Embedded font resource '{resourcePrefix}{fileName}' was not found - check the csproj's EmbeddedResource glob.");

        FontManager.RegisterFont(stream);
    }
}
