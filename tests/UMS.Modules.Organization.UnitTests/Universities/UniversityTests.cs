using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.UnitTests.Universities;

public class UniversityTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_with_blank_name_throws()
    {
        Assert.Throws<ArgumentException>(() => University.Create("   ", null, _now));
    }

    [Fact]
    public void Create_trims_name_and_starts_active()
    {
        var university = University.Create("  Example University  ", "EXU", _now);

        Assert.Equal("Example University", university.Name);
        Assert.Equal(NodeStatus.Active, university.Status);
        Assert.Equal("EXU", university.Code);
    }

    [Fact]
    public void Rename_to_the_same_name_reports_no_change_and_raises_no_event()
    {
        var university = University.Create("Example University", null, _now);

        var changed = university.Rename("Example University", _now);

        Assert.False(changed);
        Assert.Empty(university.DomainEvents);
    }

    [Fact]
    public void Rename_to_a_different_name_reports_change()
    {
        var university = University.Create("Example University", null, _now);

        var changed = university.Rename("Renamed University", _now);

        Assert.True(changed);
        Assert.Equal("Renamed University", university.Name);
    }

    [Fact]
    public void Deactivate_twice_throws()
    {
        var university = University.Create("Example University", null, _now);

        university.Deactivate();

        Assert.Throws<InvalidOperationException>(university.Deactivate);
    }

    [Fact]
    public void Activate_an_already_active_university_throws()
    {
        var university = University.Create("Example University", null, _now);

        Assert.Throws<InvalidOperationException>(university.Activate);
    }
}
