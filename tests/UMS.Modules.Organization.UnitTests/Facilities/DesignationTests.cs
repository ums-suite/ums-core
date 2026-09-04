using UMS.Modules.Organization.Domain.Designations;

namespace UMS.Modules.Organization.UnitTests.Facilities;

public class DesignationTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_with_blank_title_throws()
    {
        Assert.Throws<ArgumentException>(() => Designation.Create("  ", _now));
    }

    [Fact]
    public void ResolveTitle_falls_back_to_the_canonical_English_title()
    {
        var designation = Designation.Create("Professor", _now);
        designation.SetTranslation("bn", "অধ্যাপক");

        Assert.Equal("Professor", designation.ResolveTitle(null));
        Assert.Equal("অধ্যাপক", designation.ResolveTitle("bn"));
    }
}
