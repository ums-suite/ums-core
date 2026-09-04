using UMS.Modules.Organization.Domain.Translations;

namespace UMS.Modules.Organization.UnitTests.Facilities;

public class NameLocalizerTests
{
    [Fact]
    public void Resolve_returns_the_canonical_name_when_no_language_requested()
    {
        Assert.Equal("Canonical", NameLocalizer.Resolve("Canonical", [], null));
    }

    [Fact]
    public void Resolve_returns_the_canonical_name_when_English_is_requested()
    {
        var translations = new[] { NameTranslation.Create("bn", "Bengali Name") };

        Assert.Equal("Canonical", NameLocalizer.Resolve("Canonical", translations, "en"));
    }

    [Fact]
    public void Resolve_returns_the_matching_translation_when_one_exists()
    {
        var translations = new[] { NameTranslation.Create("bn", "Bengali Name") };

        Assert.Equal("Bengali Name", NameLocalizer.Resolve("Canonical", translations, "bn"));
    }

    [Fact]
    public void Resolve_falls_back_to_the_canonical_name_when_no_translation_matches()
    {
        var translations = new[] { NameTranslation.Create("bn", "Bengali Name") };

        Assert.Equal("Canonical", NameLocalizer.Resolve("Canonical", translations, "fr"));
    }
}
