using UMS.Modules.Organization.Domain.Campuses;
using UMS.Modules.Organization.Domain.Common;
using UMS.Modules.Organization.Domain.Events;
using UMS.Modules.Organization.Domain.Faculties;

namespace UMS.Modules.Organization.UnitTests.Faculties;

public class FacultyTests
{
    private static readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_raises_FacultyCreated()
    {
        var campusId = CampusId.New();

        var faculty = Faculty.Create(campusId, "Faculty of Engineering", _now);

        var raised = Assert.Single(faculty.DomainEvents);
        var created = Assert.IsType<FacultyCreated>(raised);
        Assert.Equal(faculty.Id.Value, created.FacultyId);
        Assert.Equal(campusId.Value, created.CampusId);
    }

    [Fact]
    public void Rename_raises_OrganizationNodeRenamed_only_when_the_name_changes()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", _now);
        faculty.ClearDomainEvents();

        var unchanged = faculty.Rename("Faculty of Engineering", _now);
        Assert.False(unchanged);
        Assert.Empty(faculty.DomainEvents);

        var changed = faculty.Rename("Faculty of Science", _now);
        Assert.True(changed);
        var raised = Assert.Single(faculty.DomainEvents);
        var renamed = Assert.IsType<OrganizationNodeRenamed>(raised);
        Assert.Equal(OrganizationNodeType.Faculty, renamed.NodeType);
        Assert.Equal("Faculty of Engineering", renamed.PreviousName);
        Assert.Equal("Faculty of Science", renamed.NewName);
    }

    [Fact]
    public void Deactivate_raises_FacultyDeactivated_and_rejects_a_second_call()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", _now);
        faculty.ClearDomainEvents();

        faculty.Deactivate(_now);

        Assert.Equal(NodeStatus.Inactive, faculty.Status);
        Assert.IsType<FacultyDeactivated>(Assert.Single(faculty.DomainEvents));
        Assert.Throws<InvalidOperationException>(() => faculty.Deactivate(_now));
    }

    [Fact]
    public void SetTranslation_upserts_a_single_row_per_language()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", _now);

        faculty.SetTranslation("bn", "প্রকৌশল অনুষদ");
        faculty.SetTranslation("bn", "প্রকৌশল অনুষদ (সংশোধিত)");

        var translation = Assert.Single(faculty.Translations);
        Assert.Equal("bn", translation.LanguageCode);
        Assert.Equal("প্রকৌশল অনুষদ (সংশোধিত)", translation.Name);
    }

    [Fact]
    public void ResolveName_falls_back_to_the_canonical_English_name()
    {
        var faculty = Faculty.Create(CampusId.New(), "Faculty of Engineering", _now);
        faculty.SetTranslation("bn", "প্রকৌশল অনুষদ");

        Assert.Equal("Faculty of Engineering", faculty.ResolveName(null));
        Assert.Equal("Faculty of Engineering", faculty.ResolveName("en"));
        Assert.Equal("প্রকৌশল অনুষদ", faculty.ResolveName("bn"));
        Assert.Equal("Faculty of Engineering", faculty.ResolveName("fr"));
    }
}
