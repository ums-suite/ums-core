using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.Domain.Students;
using UMS.Modules.Student.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.UnitTests.Students;

/// <summary>STU-5/STU-6/STU-7 (requirement-spec.md student §6).</summary>
public sealed class StudentProfileServiceTests
{
    private static AuditContext Audit(Guid actorUserId) => new(actorUserId, "127.0.0.1", Guid.NewGuid().ToString());

    private static Domain.Students.Student CreateEnrolledStudentWithUser(Guid userId)
    {
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 1);
        var name = PersonName.Create("Rahim", "Uddin").Value;
        var email = Email.Create("rahim@example.edu.bd").Value;
        var student = Domain.Students.Student.Enroll(Guid.NewGuid(), studentNumber, Guid.NewGuid(), Guid.NewGuid(), name, email, mobile: null, new DateOnly(2005, 1, 1), "1234567890", DateTimeOffset.UtcNow);
        student.SetIdentityUser(userId);
        return student;
    }

    private static (StudentProfileService Service, FakeStudentRepository Students) CreateService()
    {
        var students = new FakeStudentRepository();
        var service = new StudentProfileService(students, new FakeUnitOfWork(), new FakeAuditRecorder());
        return (service, students);
    }

    [Fact]
    public async Task GetOwnProfileAsync_when_no_Student_is_linked_to_the_caller_returns_NotFound()
    {
        var (service, _) = CreateService();

        var result = await service.GetOwnProfileAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("student.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task GetOwnProfileAsync_returns_the_caller_own_Student()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudentWithUser(userId);
        students.Seed(student);

        var result = await service.GetOwnProfileAsync(userId);

        Assert.True(result.IsSuccess);
        Assert.Equal(student.Id.Value, result.Value.Id);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_updates_contact_info_and_photo_only()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        var student = CreateEnrolledStudentWithUser(userId);
        students.Seed(student);

        var result = await service.UpdateOwnProfileAsync(userId, new UpdateSelfServiceProfileRequest("new@example.edu.bd", "+8801812345678", "https://example.com/p.jpg", student.Version), Audit(userId));

        Assert.True(result.IsSuccess);
        Assert.Equal("new@example.edu.bd", result.Value.ContactEmail);
        Assert.Equal("Rahim", result.Value.GivenName);
        Assert.Equal(new DateOnly(2005, 1, 1), result.Value.DateOfBirth);
    }

    [Fact]
    public async Task UpdateOwnProfileAsync_when_no_Student_is_linked_returns_NotFound()
    {
        var (service, _) = CreateService();

        var result = await service.UpdateOwnProfileAsync(Guid.NewGuid(), new UpdateSelfServiceProfileRequest(null, null, null, 0), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("student.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task GetByIdAsync_for_a_nonexistent_Student_returns_NotFound()
    {
        var (service, _) = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("student.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task GetByIdAsync_returns_the_requested_Student()
    {
        var (service, students) = CreateService();
        var student = CreateEnrolledStudentWithUser(Guid.NewGuid());
        students.Seed(student);

        var result = await service.GetByIdAsync(student.Id.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(student.Id.Value, result.Value.Id);
    }
}
