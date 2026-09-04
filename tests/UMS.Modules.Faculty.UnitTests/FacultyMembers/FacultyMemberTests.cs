using UMS.Modules.Faculty.Domain.FacultyMembers;

namespace UMS.Modules.Faculty.UnitTests.FacultyMembers;

public sealed class FacultyMemberTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Onboard_creates_an_Active_profile_and_raises_FacultyMemberOnboarded()
    {
        var userId = Guid.NewGuid();
        var departmentId = Guid.NewGuid();

        var facultyMember = FacultyMember.Onboard(userId, "EMP-001", departmentId, Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), Now);

        Assert.Equal(FacultyMemberStatus.Active, facultyMember.Status);
        Assert.Equal(userId, facultyMember.UserId);
        var domainEvent = Assert.Single(facultyMember.DomainEvents);
        var onboarded = Assert.IsType<UMS.Modules.Faculty.Domain.Events.FacultyMemberOnboarded>(domainEvent);
        Assert.Equal(departmentId, onboarded.DepartmentId);
    }

    [Fact]
    public void UpdateSelfServiceProfile_touches_only_contact_fields()
    {
        var facultyMember = FacultyMember.Onboard(Guid.NewGuid(), "EMP-002", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), Now);
        var originalDepartmentId = facultyMember.DepartmentId;

        facultyMember.UpdateSelfServiceProfile("me@example.edu.bd", "+8801712345678");

        Assert.Equal("me@example.edu.bd", facultyMember.ContactEmail);
        Assert.Equal(originalDepartmentId, facultyMember.DepartmentId);
    }

    [Fact]
    public void ChangeStatus_to_the_same_status_throws()
    {
        var facultyMember = FacultyMember.Onboard(Guid.NewGuid(), "EMP-003", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), Now);

        Assert.Throws<InvalidOperationException>(() => facultyMember.ChangeStatus(FacultyMemberStatus.Active, Now));
    }

    [Fact]
    public void ChangeStatus_to_a_different_status_raises_FacultyMemberStatusChanged()
    {
        var facultyMember = FacultyMember.Onboard(Guid.NewGuid(), "EMP-004", Guid.NewGuid(), Guid.NewGuid(), EmploymentType.FullTime, new DateOnly(2020, 1, 1), Now);

        facultyMember.ChangeStatus(FacultyMemberStatus.OnLeave, Now);

        Assert.Equal(FacultyMemberStatus.OnLeave, facultyMember.Status);
        Assert.Contains(facultyMember.DomainEvents, e => e is UMS.Modules.Faculty.Domain.Events.FacultyMemberStatusChanged);
    }
}
