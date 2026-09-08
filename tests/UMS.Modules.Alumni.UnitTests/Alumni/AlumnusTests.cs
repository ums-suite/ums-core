using UMS.Modules.Alumni.Domain.Alumni;
using UMS.Modules.Alumni.Domain.Events;

namespace UMS.Modules.Alumni.UnitTests.Alumni;

/// <summary>requirement-spec.md §2.1/§4: default-private visibility, independence from Student's live record after creation.</summary>
public sealed class AlumnusTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_defaults_to_Private_visibility()
    {
        var studentId = Guid.NewGuid();
        var alumnus = Alumnus.Create(studentId, 2026, Guid.NewGuid(), Guid.NewGuid(), Now);

        Assert.Equal(ProfileVisibility.Private, alumnus.ProfileVisibility);
        Assert.Equal(studentId, alumnus.StudentIdRef);
        Assert.Equal(2026, alumnus.GraduationYear);
    }

    [Fact]
    public void Create_raises_exactly_one_AlumnusCreated_domain_event()
    {
        var alumnus = Alumnus.Create(Guid.NewGuid(), 2026, Guid.NewGuid(), Guid.NewGuid(), Now);

        var domainEvent = Assert.Single(alumnus.DomainEvents);
        var alumnusCreated = Assert.IsType<AlumnusCreated>(domainEvent);
        Assert.Equal(alumnus.Id.Value, alumnusCreated.AlumnusId);
    }

    [Fact]
    public void SetVisibility_can_toggle_Public_and_back_to_Private()
    {
        var alumnus = Alumnus.Create(Guid.NewGuid(), 2026, Guid.NewGuid(), Guid.NewGuid(), Now);

        alumnus.SetVisibility(ProfileVisibility.Public);
        Assert.Equal(ProfileVisibility.Public, alumnus.ProfileVisibility);

        alumnus.SetVisibility(ProfileVisibility.Private);
        Assert.Equal(ProfileVisibility.Private, alumnus.ProfileVisibility);
    }

    [Fact]
    public void UpdateProfile_blanks_whitespace_only_fields_to_null_and_records_field_level_visibility_flags()
    {
        var alumnus = Alumnus.Create(Guid.NewGuid(), 2026, Guid.NewGuid(), Guid.NewGuid(), Now);

        alumnus.UpdateProfile("Acme Corp", "  ", "Dhaka", "a@b.com", "0170000000", hideCurrentEmployer: true, hideContactDetails: false);

        Assert.Equal("Acme Corp", alumnus.CurrentEmployer);
        Assert.Null(alumnus.Bio);
        Assert.Equal("Dhaka", alumnus.Location);
        Assert.True(alumnus.HideCurrentEmployer);
        Assert.False(alumnus.HideContactDetails);
    }
}
