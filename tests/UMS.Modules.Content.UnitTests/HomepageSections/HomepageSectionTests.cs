using UMS.Modules.Content.Domain.HomepageSections;

namespace UMS.Modules.Content.UnitTests.HomepageSections;

/// <summary>CNT-10: requirement-spec.md §2.5.</summary>
public sealed class HomepageSectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_requires_a_section_key_and_title()
    {
        Assert.Equal("homepage_section.key_required", HomepageSection.Create(" ", "Title", 1, true, null, Now).Error!.Code);
        Assert.Equal("homepage_section.title_required", HomepageSection.Create("hero", " ", 1, true, null, Now).Error!.Code);
    }

    [Fact]
    public void Create_lowercases_the_section_key()
    {
        var section = HomepageSection.Create("HERO-Banner", "Hero", 1, true, null, Now).Value;

        Assert.Equal("hero-banner", section.SectionKey);
    }

    [Fact]
    public void UpdateDetails_can_toggle_enabled_and_reorder()
    {
        var section = HomepageSection.Create("hero", "Hero", 1, true, null, Now).Value;

        var result = section.UpdateDetails("Hero Updated", 5, false, null, Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, section.SortOrder);
        Assert.False(section.IsEnabled);
        Assert.Equal("Hero Updated", section.Title);
    }
}
