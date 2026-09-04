using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.Domain.Students;
using UMS.Modules.Student.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.UnitTests.Students;

/// <summary>STU-8 (requirement-spec.md student §2/§4/§8; design-decisions.md, "Status-Change Transactional Boundary").</summary>
public sealed class StudentStatusServiceTests
{
    private static AuditContext Audit(Guid actorUserId) => new(actorUserId, "127.0.0.1", Guid.NewGuid().ToString());

    private static Domain.Students.Student CreateEnrolledStudent()
    {
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 1);
        var name = PersonName.Create("Rahim", "Uddin").Value;
        var email = Email.Create("rahim@example.edu.bd").Value;
        return Domain.Students.Student.Enroll(Guid.NewGuid(), studentNumber, Guid.NewGuid(), Guid.NewGuid(), name, email, mobile: null, new DateOnly(2005, 1, 1), "1234567890", DateTimeOffset.UtcNow);
    }

    private static (StudentStatusService Service, FakeStudentRepository Students, FakeAuditRecorder AuditRecorder) CreateService()
    {
        var students = new FakeStudentRepository();
        var auditRecorder = new FakeAuditRecorder();
        var service = new StudentStatusService(students, new FakeUnitOfWork(), auditRecorder, new FakeClock());
        return (service, students, auditRecorder);
    }

    [Fact]
    public async Task ChangeStatusAsync_with_an_unknown_status_string_is_a_validation_error()
    {
        var (service, students, _) = CreateService();
        var student = CreateEnrolledStudent();
        students.Seed(student);

        var result = await service.ChangeStatusAsync(student.Id.Value, Guid.NewGuid(), new ChangeStudentStatusRequest("NotARealStatus", null, student.Version), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("student.invalid_status", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_for_a_nonexistent_Student_returns_NotFound()
    {
        var (service, _, _) = CreateService();

        var result = await service.ChangeStatusAsync(Guid.NewGuid(), Guid.NewGuid(), new ChangeStudentStatusRequest("Active", null, 0), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("student.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ChangeStatusAsync_rejects_an_illegal_transition()
    {
        var (service, students, _) = CreateService();
        var student = CreateEnrolledStudent();
        students.Seed(student);

        var result = await service.ChangeStatusAsync(student.Id.Value, Guid.NewGuid(), new ChangeStudentStatusRequest("Graduated", null, student.Version), Audit(Guid.NewGuid()));

        Assert.True(result.IsFailure);
        Assert.Equal("student.invalid_transition", result.Error!.Code);
    }

    /// <summary>edge-cases.md "Two admins issue conflicting status changes" residual note: "both admins' attempted actions are independently audited ... even though only one is ever applied" - here, a rejected illegal-transition attempt still gets its own Audit entry.</summary>
    [Fact]
    public async Task ChangeStatusAsync_rejecting_an_illegal_transition_still_writes_a_rejected_attempt_Audit_entry()
    {
        var (service, students, auditRecorder) = CreateService();
        var student = CreateEnrolledStudent();
        students.Seed(student);

        await service.ChangeStatusAsync(student.Id.Value, Guid.NewGuid(), new ChangeStudentStatusRequest("Graduated", null, student.Version), Audit(Guid.NewGuid()));

        var auditEntry = Assert.Single(auditRecorder.RecordedEntries);
        Assert.Equal("status_change_rejected", auditEntry.Action);
        Assert.Equal("Student", auditEntry.EntityType);
    }

    [Fact]
    public async Task ChangeStatusAsync_Enrolled_to_Active_succeeds_and_records_an_Audit_entry()
    {
        var (service, students, auditRecorder) = CreateService();
        var student = CreateEnrolledStudent();
        students.Seed(student);

        var result = await service.ChangeStatusAsync(student.Id.Value, Guid.NewGuid(), new ChangeStudentStatusRequest("Active", "activated", student.Version), Audit(Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
        var auditEntry = Assert.Single(auditRecorder.RecordedEntries);
        Assert.Equal("status_change", auditEntry.Action);
        Assert.Equal("Student", auditEntry.EntityType);
    }

    [Fact]
    public async Task ChangeStatusAsync_Suspended_to_Active_reinstatement_succeeds()
    {
        var (service, students, _) = CreateService();
        var student = CreateEnrolledStudent();
        student.ChangeStatus(StudentStatus.Active, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        student.ChangeStatus(StudentStatus.Suspended, "disciplinary", Guid.NewGuid(), DateTimeOffset.UtcNow);
        students.Seed(student);

        var result = await service.ChangeStatusAsync(student.Id.Value, Guid.NewGuid(), new ChangeStudentStatusRequest("Active", "reinstated", student.Version), Audit(Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", result.Value.Status);
    }
}
