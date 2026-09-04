using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Guardians;
using UMS.Modules.Student.Domain.Students;
using UMS.Modules.Student.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.UnitTests.Guardians;

/// <summary>Guardian/GuardianAccessGrant scaffolding (docs/ddd/ubiquitous-language.md) - application-layer ownership checks.</summary>
public sealed class GuardianServiceTests
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

    private static (GuardianService Service, FakeStudentRepository Students) CreateService()
    {
        var students = new FakeStudentRepository();
        var service = new GuardianService(students, new FakeUnitOfWork(), new FakeAuditRecorder(), new FakeClock());
        return (service, students);
    }

    [Fact]
    public async Task LinkGuardianAsync_when_no_Student_is_linked_to_the_caller_returns_NotFound()
    {
        var (service, _) = CreateService();

        var result = await service.LinkGuardianAsync(Guid.NewGuid(), new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("student.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task LinkGuardianAsync_succeeds_for_the_owning_Student()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(userId));

        var result = await service.LinkGuardianAsync(userId, new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(userId));

        Assert.True(result.IsSuccess);
        Assert.Equal("Karim", result.Value.Name);
        Assert.Empty(result.Value.ActiveAccessGrants);
    }

    [Fact]
    public async Task LinkGuardianAsync_with_neither_contact_email_nor_phone_is_a_validation_error()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(userId));

        var result = await service.LinkGuardianAsync(userId, new LinkGuardianRequest("Karim", "Father", null, null), Audit(userId));

        Assert.True(result.IsFailure);
        Assert.Equal("guardian.invalid", result.Error!.Code);
    }

    [Fact]
    public async Task GrantAccessAsync_then_ListOwnGuardiansAsync_reflects_only_the_granted_category()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(userId));
        var linked = await service.LinkGuardianAsync(userId, new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(userId));

        var granted = await service.GrantAccessAsync(userId, linked.Value.Id, new GrantGuardianAccessRequest("Fees"), Audit(userId));

        Assert.True(granted.IsSuccess);
        var activeGrant = Assert.Single(granted.Value.ActiveAccessGrants);
        Assert.Equal("Fees", activeGrant.Category);

        var list = await service.ListOwnGuardiansAsync(userId);
        Assert.True(list.IsSuccess);
        var onlyGuardian = Assert.Single(list.Value);
        Assert.Single(onlyGuardian.ActiveAccessGrants);
    }

    [Fact]
    public async Task GrantAccessAsync_with_an_invalid_category_is_a_validation_error()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(userId));
        var linked = await service.LinkGuardianAsync(userId, new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(userId));

        var result = await service.GrantAccessAsync(userId, linked.Value.Id, new GrantGuardianAccessRequest("NotACategory"), Audit(userId));

        Assert.True(result.IsFailure);
        Assert.Equal("guardian.invalid_category", result.Error!.Code);
    }

    [Fact]
    public async Task RevokeAccessAsync_removes_the_category_from_the_active_grant_list()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(userId));
        var linked = await service.LinkGuardianAsync(userId, new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(userId));
        await service.GrantAccessAsync(userId, linked.Value.Id, new GrantGuardianAccessRequest("Attendance"), Audit(userId));

        var revoked = await service.RevokeAccessAsync(userId, linked.Value.Id, "Attendance", Audit(userId));

        Assert.True(revoked.IsSuccess);
        Assert.Empty(revoked.Value.ActiveAccessGrants);
    }

    [Fact]
    public async Task RevokeAccessAsync_for_a_category_never_granted_is_a_conflict()
    {
        var (service, students) = CreateService();
        var userId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(userId));
        var linked = await service.LinkGuardianAsync(userId, new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(userId));

        var result = await service.RevokeAccessAsync(userId, linked.Value.Id, "Grades", Audit(userId));

        Assert.True(result.IsFailure);
        Assert.Equal("guardian.revoke_invalid", result.Error!.Code);
    }

    [Fact]
    public async Task GrantAccessAsync_by_someone_other_than_the_owning_Student_returns_NotFound_for_their_own_missing_profile()
    {
        var (service, students) = CreateService();
        var ownerUserId = Guid.NewGuid();
        students.Seed(CreateEnrolledStudentWithUser(ownerUserId));
        var linked = await service.LinkGuardianAsync(ownerUserId, new LinkGuardianRequest("Karim", "Father", "karim@example.edu.bd", null), Audit(ownerUserId));

        // A different caller with no Student profile of their own cannot reach ANY Student's
        // Guardian data - GuardianService always resolves "the caller's own Student" first, never
        // an id supplied by the caller (Student-owned only, no admin/HR variant).
        var result = await service.GrantAccessAsync(Guid.NewGuid(), linked.Value.Id, new GrantGuardianAccessRequest("Fees"), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("student.not_found", result.Error!.Code);
    }
}
