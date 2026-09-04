using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Universities;

namespace UMS.Modules.Organization.UnitTests.Campuses;

public class CampusTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_with_blank_name_throws()
    {
        Assert.Throws<ArgumentException>(() => Campus.Create(UniversityId.New(), " ", _now));
    }

    [Fact]
    public void Create_links_to_the_given_parent_university_and_starts_active()
    {
        var universityId = UniversityId.New();

        var campus = Campus.Create(universityId, "Main Campus", _now);

        Assert.Equal(universityId, campus.UniversityId);
        Assert.Equal(NodeStatus.Active, campus.Status);
    }

    [Fact]
    public void Deactivate_twice_throws()
    {
        var campus = Campus.Create(UniversityId.New(), "Main Campus", _now);
        campus.Deactivate();

        Assert.Throws<InvalidOperationException>(campus.Deactivate);
    }

    [Fact]
    public void Rename_reports_whether_the_name_actually_changed()
    {
        var campus = Campus.Create(UniversityId.New(), "Main Campus", _now);

        Assert.False(campus.Rename("Main Campus", _now));
        Assert.True(campus.Rename("City Campus", _now));
    }
}
