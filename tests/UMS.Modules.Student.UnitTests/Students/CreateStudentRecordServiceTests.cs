using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.UnitTests.TestDoubles;

namespace UMS.Modules.Student.UnitTests.Students;

/// <summary>STU-1: idempotency and validation for the internal-only <c>CreateStudentRecord</c> command (requirement-spec.md student §2/§4/§8).</summary>
public sealed class CreateStudentRecordServiceTests
{
    private static AuditContext SystemAudit() => AuditContext.ForSystem(Guid.NewGuid().ToString());

    private static CreateStudentRecordRequest ValidRequest(Guid? originatingApplicationId = null) => new(
        originatingApplicationId ?? Guid.NewGuid(),
        2026,
        "CSE",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Rahim",
        "Uddin",
        null,
        null,
        $"student-{Guid.NewGuid():N}@example.edu.bd",
        null,
        new DateOnly(2005, 1, 1),
        "1234567890");

    private static (CreateStudentRecordService Service, FakeStudentRepository Students, FakeUserProvisioningPort Provisioning, FakeDocumentGenerationPort Documents, FakeNotificationRequestPublisher Notifications) CreateService()
    {
        var students = new FakeStudentRepository();
        var provisioning = new FakeUserProvisioningPort();
        var documents = new FakeDocumentGenerationPort();
        var notifications = new FakeNotificationRequestPublisher();
        var service = new CreateStudentRecordService(
            students,
            new FakeOrganizationDepartmentExistenceChecker(),
            new FakeProgramExistenceChecker(),
            new FakeStudentNumberSequence(),
            provisioning,
            documents,
            notifications,
            new FakeUnitOfWork(),
            new FakeAuditRecorder(),
            new FakeClock(),
            NullLogger<CreateStudentRecordService>.Instance);
        return (service, students, provisioning, documents, notifications);
    }

    [Fact]
    public async Task CreateAsync_generates_a_StudentNumber_and_returns_Enrolled_status()
    {
        var (service, _, _, _, _) = CreateService();

        var result = await service.CreateAsync(ValidRequest(), SystemAudit());

        Assert.True(result.IsSuccess);
        Assert.Equal("2026CSE00001", result.Value.StudentNumber);
        Assert.Equal("Enrolled", result.Value.Status);
    }

    [Fact]
    public async Task CreateAsync_retried_with_the_same_OriginatingApplicationId_returns_the_existing_Student_not_a_duplicate()
    {
        var (service, _, _, _, _) = CreateService();
        var applicationId = Guid.NewGuid();

        var first = await service.CreateAsync(ValidRequest(applicationId), SystemAudit());
        var second = await service.CreateAsync(ValidRequest(applicationId), SystemAudit());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(first.Value.StudentNumber, second.Value.StudentNumber);
    }

    [Fact]
    public async Task CreateAsync_retried_does_not_double_fire_side_effects()
    {
        var (service, _, provisioning, documents, notifications) = CreateService();
        var applicationId = Guid.NewGuid();

        await service.CreateAsync(ValidRequest(applicationId), SystemAudit());
        await service.CreateAsync(ValidRequest(applicationId), SystemAudit());

        Assert.Single(provisioning.Requests);
        Assert.Single(documents.Requests);
        Assert.Single(notifications.Published);
    }

    [Fact]
    public async Task CreateAsync_on_first_success_provisions_Identity_requests_an_IdCard_and_publishes_a_welcome_notification()
    {
        var (service, _, provisioning, documents, notifications) = CreateService();

        var result = await service.CreateAsync(ValidRequest(), SystemAudit());

        Assert.True(result.IsSuccess);
        Assert.Single(provisioning.Requests);
        Assert.Single(documents.Requests);
        Assert.Single(notifications.Published);
        Assert.NotNull(result.Value.IdentityUserId);
        Assert.NotNull(result.Value.IdCardDocumentId);
    }

    [Fact]
    public async Task CreateAsync_when_Identity_provisioning_fails_still_creates_the_Student_with_a_null_IdentityUserId()
    {
        var students = new FakeStudentRepository();
        var service = new CreateStudentRecordService(
            students,
            new FakeOrganizationDepartmentExistenceChecker(),
            new FakeProgramExistenceChecker(),
            new FakeStudentNumberSequence(),
            new FakeUserProvisioningPort(succeeds: false),
            new FakeDocumentGenerationPort(),
            new FakeNotificationRequestPublisher(),
            new FakeUnitOfWork(),
            new FakeAuditRecorder(),
            new FakeClock(),
            NullLogger<CreateStudentRecordService>.Instance);

        var result = await service.CreateAsync(ValidRequest(), SystemAudit());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.IdentityUserId);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_invalid_faculty_code()
    {
        var (service, _, _, _, _) = CreateService();
        var request = ValidRequest() with { FacultyCode = "not-valid!" };

        var result = await service.CreateAsync(request, SystemAudit());

        Assert.True(result.IsFailure);
        Assert.Equal("student.invalid_faculty_code", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_nonexistent_Department()
    {
        var students = new FakeStudentRepository();
        var service = new CreateStudentRecordService(
            students,
            new FakeOrganizationDepartmentExistenceChecker(exists: false),
            new FakeProgramExistenceChecker(),
            new FakeStudentNumberSequence(),
            new FakeUserProvisioningPort(),
            new FakeDocumentGenerationPort(),
            new FakeNotificationRequestPublisher(),
            new FakeUnitOfWork(),
            new FakeAuditRecorder(),
            new FakeClock(),
            NullLogger<CreateStudentRecordService>.Instance);

        var result = await service.CreateAsync(ValidRequest(), SystemAudit());

        Assert.True(result.IsFailure);
        Assert.Equal("department.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_two_different_applications_in_the_same_admissionYear_and_facultyCode_get_distinct_sequential_StudentNumbers()
    {
        var (service, _, _, _, _) = CreateService();

        var first = await service.CreateAsync(ValidRequest(), SystemAudit());
        var second = await service.CreateAsync(ValidRequest(), SystemAudit());

        Assert.Equal("2026CSE00001", first.Value.StudentNumber);
        Assert.Equal("2026CSE00002", second.Value.StudentNumber);
    }
}
