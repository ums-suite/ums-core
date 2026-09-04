using UMS.Modules.Student.Domain.Students;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.UnitTests.Students;

public sealed class StudentTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static Domain.Students.Student CreateEnrolled()
    {
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 1);
        var name = PersonName.Create("Rahim", "Uddin").Value;
        var email = Email.Create("rahim@example.edu.bd").Value;
        return Domain.Students.Student.Enroll(Guid.NewGuid(), studentNumber, Guid.NewGuid(), Guid.NewGuid(), name, email, mobile: null, new DateOnly(2005, 1, 1), "1234567890", Now);
    }

    [Fact]
    public void Enroll_creates_a_Student_in_Enrolled_status_with_an_initial_history_entry()
    {
        var student = CreateEnrolled();

        Assert.Equal(StudentStatus.Enrolled, student.Status);
        Assert.Single(student.StatusHistory);
        Assert.Null(student.StatusHistory.Single().FromStatus);
        Assert.Equal(StudentStatus.Enrolled, student.StatusHistory.Single().ToStatus);
    }

    [Fact]
    public void Enroll_raises_StudentRecordCreated()
    {
        var student = CreateEnrolled();

        var domainEvent = Assert.Single(student.DomainEvents);
        Assert.IsType<UMS.Modules.Student.Domain.Events.StudentRecordCreated>(domainEvent);
    }

    [Theory]
    [InlineData(StudentStatus.Enrolled, StudentStatus.Active, true)]
    [InlineData(StudentStatus.Active, StudentStatus.Graduated, true)]
    [InlineData(StudentStatus.Active, StudentStatus.Suspended, true)]
    [InlineData(StudentStatus.Active, StudentStatus.Transferred, true)]
    [InlineData(StudentStatus.Suspended, StudentStatus.Active, true)]
    [InlineData(StudentStatus.Enrolled, StudentStatus.Graduated, false)]
    [InlineData(StudentStatus.Enrolled, StudentStatus.Suspended, false)]
    [InlineData(StudentStatus.Active, StudentStatus.Enrolled, false)]
    [InlineData(StudentStatus.Graduated, StudentStatus.Active, false)]
    [InlineData(StudentStatus.Transferred, StudentStatus.Active, false)]
    [InlineData(StudentStatus.Suspended, StudentStatus.Graduated, false)]
    public void ChangeStatus_permits_only_the_documented_legal_transitions(StudentStatus from, StudentStatus to, bool expectedLegal)
    {
        var student = CreateEnrolled();

        // Drive the aggregate to the "from" state via legal transitions only.
        if (from != StudentStatus.Enrolled)
        {
            student.ChangeStatus(StudentStatus.Active, null, Guid.NewGuid(), Now);
            if (from != StudentStatus.Active)
            {
                student.ChangeStatus(from, "test", Guid.NewGuid(), Now);
            }
        }

        if (expectedLegal)
        {
            student.ChangeStatus(to, "reason", Guid.NewGuid(), Now);
            Assert.Equal(to, student.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => student.ChangeStatus(to, "reason", Guid.NewGuid(), Now));
        }
    }

    [Fact]
    public void ChangeStatus_appends_a_new_StatusHistory_entry_without_editing_prior_ones()
    {
        var student = CreateEnrolled();

        student.ChangeStatus(StudentStatus.Active, "activated", Guid.NewGuid(), Now);
        student.ChangeStatus(StudentStatus.Suspended, "disciplinary", Guid.NewGuid(), Now);

        Assert.Equal(3, student.StatusHistory.Count);
        Assert.Equal(StudentStatus.Enrolled, student.StatusHistory.ElementAt(0).ToStatus);
        Assert.Equal(StudentStatus.Active, student.StatusHistory.ElementAt(1).ToStatus);
        Assert.Equal(StudentStatus.Suspended, student.StatusHistory.ElementAt(2).ToStatus);
    }

    [Fact]
    public void ChangeStatus_to_Suspended_raises_StudentSuspended_in_addition_to_StudentStatusChanged()
    {
        var student = CreateEnrolled();
        student.ClearDomainEvents();

        student.ChangeStatus(StudentStatus.Active, null, Guid.NewGuid(), Now);
        student.ClearDomainEvents();
        student.ChangeStatus(StudentStatus.Suspended, "reason", Guid.NewGuid(), Now);

        Assert.Contains(student.DomainEvents, e => e is UMS.Modules.Student.Domain.Events.StudentStatusChanged);
        Assert.Contains(student.DomainEvents, e => e is UMS.Modules.Student.Domain.Events.StudentSuspended);
    }

    [Fact]
    public void UpdateSelfServiceProfile_never_touches_identity_bearing_fields()
    {
        var student = CreateEnrolled();
        var originalName = student.Name;
        var originalDob = student.DateOfBirth;
        var originalNationalId = student.NationalId;

        student.UpdateSelfServiceProfile("new@example.edu.bd", "+8801812345678", "https://example.com/photo.jpg");

        Assert.Equal(originalName, student.Name);
        Assert.Equal(originalDob, student.DateOfBirth);
        Assert.Equal(originalNationalId, student.NationalId);
        Assert.Equal("new@example.edu.bd", student.ContactEmail);
        Assert.Equal("+8801812345678", student.ContactPhone);
        Assert.Equal("https://example.com/photo.jpg", student.PhotoUrl);
    }

    [Fact]
    public void LinkGuardian_then_GrantGuardianAccess_then_RevokeGuardianAccess_round_trips()
    {
        var student = CreateEnrolled();

        var guardian = student.LinkGuardian("Karim Uddin", "Father", "karim@example.edu.bd", null, Now);
        Assert.Single(student.Guardians);

        student.GrantGuardianAccess(guardian.Id.Value, UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Fees, Now);
        Assert.True(student.GuardianAccessGrants.Single(g => g.GuardianId == guardian.Id.Value).IsActive);

        student.RevokeGuardianAccess(guardian.Id.Value, UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Fees, Now);
        Assert.False(student.GuardianAccessGrants.Single(g => g.GuardianId == guardian.Id.Value).IsActive);
    }

    [Fact]
    public void GrantGuardianAccess_is_category_scoped_only_the_granted_category_is_active()
    {
        var student = CreateEnrolled();
        var guardian = student.LinkGuardian("Karim Uddin", "Father", "karim@example.edu.bd", null, Now);

        student.GrantGuardianAccess(guardian.Id.Value, UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Fees, Now);

        Assert.Contains(student.GuardianAccessGrants, g => g.Category == UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Fees && g.IsActive);
        Assert.DoesNotContain(student.GuardianAccessGrants, g => g.Category == UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Attendance);
        Assert.DoesNotContain(student.GuardianAccessGrants, g => g.Category == UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Grades);
    }

    [Fact]
    public void GrantGuardianAccess_twice_for_the_same_category_throws()
    {
        var student = CreateEnrolled();
        var guardian = student.LinkGuardian("Karim Uddin", "Father", "karim@example.edu.bd", null, Now);
        student.GrantGuardianAccess(guardian.Id.Value, UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Grades, Now);

        Assert.Throws<InvalidOperationException>(() => student.GrantGuardianAccess(guardian.Id.Value, UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Grades, Now));
    }

    [Fact]
    public void RevokeGuardianAccess_for_a_category_never_granted_throws()
    {
        var student = CreateEnrolled();
        var guardian = student.LinkGuardian("Karim Uddin", "Father", "karim@example.edu.bd", null, Now);

        Assert.Throws<InvalidOperationException>(() => student.RevokeGuardianAccess(guardian.Id.Value, UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Attendance, Now));
    }

    [Fact]
    public void GrantGuardianAccess_for_an_unknown_Guardian_throws()
    {
        var student = CreateEnrolled();

        Assert.Throws<InvalidOperationException>(() => student.GrantGuardianAccess(Guid.NewGuid(), UMS.Modules.Student.Domain.Guardians.GuardianAccessCategory.Fees, Now));
    }
}
