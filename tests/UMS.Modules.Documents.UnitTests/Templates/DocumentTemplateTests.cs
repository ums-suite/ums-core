using UMS.Modules.Documents.Domain.Common;
using UMS.Modules.Documents.Domain.Templates;

namespace UMS.Modules.Documents.UnitTests.Templates;

/// <summary>DOC-1: versioning/bilingual invariants (requirement-spec.md documents §2 Template Management).</summary>
public sealed class DocumentTemplateTests
{
    private static Dictionary<LanguageCode, (string Title, string LabelsJson)> ValidTranslations() => new()
    {
        [LanguageCode.En] = ("Transcript", "{}"),
        [LanguageCode.Bn] = ("ট্রান্সক্রিপ্ট", "{}"),
    };

    [Fact]
    public void Create_with_a_valid_English_translation_succeeds()
    {
        var result = DocumentTemplate.Create(DocumentType.Transcript, version: 1, layoutAssetKey: null, ValidTranslations(), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(2, result.Value.Translations.Count);
    }

    [Fact]
    public void Create_without_an_English_translation_is_rejected()
    {
        var translations = new Dictionary<LanguageCode, (string Title, string LabelsJson)>
        {
            [LanguageCode.Bn] = ("ট্রান্সক্রিপ্ট", "{}"),
        };

        var result = DocumentTemplate.Create(DocumentType.Transcript, version: 1, layoutAssetKey: null, translations, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("document_template.english_required", result.Error!.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_with_a_non_positive_version_is_rejected(int version)
    {
        var result = DocumentTemplate.Create(DocumentType.Transcript, version, layoutAssetKey: null, ValidTranslations(), DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("document_template.version_invalid", result.Error!.Code);
    }

    [Fact]
    public void Create_with_a_blank_title_is_rejected()
    {
        var translations = new Dictionary<LanguageCode, (string Title, string LabelsJson)>
        {
            [LanguageCode.En] = ("   ", "{}"),
        };

        var result = DocumentTemplate.Create(DocumentType.Transcript, version: 1, layoutAssetKey: null, translations, DateTimeOffset.UtcNow);

        Assert.True(result.IsFailure);
        Assert.Equal("document_template.title_required", result.Error!.Code);
    }

    [Fact]
    public void ResolveTranslation_returns_the_requested_language_when_present()
    {
        var template = DocumentTemplate.Create(DocumentType.Transcript, version: 1, layoutAssetKey: null, ValidTranslations(), DateTimeOffset.UtcNow).Value;

        var translation = template.ResolveTranslation(LanguageCode.Bn);

        Assert.Equal(LanguageCode.Bn, translation.Language);
    }

    [Fact]
    public void ResolveTranslation_falls_back_to_English_when_the_requested_language_is_missing()
    {
        var translations = new Dictionary<LanguageCode, (string Title, string LabelsJson)>
        {
            [LanguageCode.En] = ("Transcript", "{}"),
        };
        var template = DocumentTemplate.Create(DocumentType.Transcript, version: 1, layoutAssetKey: null, translations, DateTimeOffset.UtcNow).Value;

        var translation = template.ResolveTranslation(LanguageCode.Bn);

        Assert.Equal(LanguageCode.En, translation.Language);
    }
}
