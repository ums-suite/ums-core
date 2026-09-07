using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Student.Application.BulkImport;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.Domain.BulkImport;
using UMS.Modules.Student.Domain.Students;
using UMS.Modules.Student.UnitTests.TestDoubles;
using UMS.Shared.Domain;

namespace UMS.Modules.Student.UnitTests.BulkImport;

/// <summary>
/// STU-15: the async processing half (requirement-spec.md student §2 Bulk Import; design-decisions.md
/// "Bulk-Import Concurrency &amp; Field-Scoping Design"). Genuine concurrent-write races against
/// <c>PUT /students/me</c> are covered for real in the integration suite (design-decisions.md's own
/// layered-verification convention, mirroring <c>StudentStatusServiceTests</c>'s remarks) - the
/// <see cref="FakeUnitOfWork"/> used here never throws <c>ConcurrencyConflictException</c>.
/// </summary>
public sealed class StudentBulkImportProcessingServiceTests
{
    private static StudentBulkImportRowInput ValidCreateRow(Guid departmentId, Guid programId) => new(
        Guid.NewGuid(), null, null, 2026, "CSE", departmentId, programId, "Rahim", "Uddin", null, null,
        $"student-{Guid.NewGuid():N}@example.edu.bd", null, new DateOnly(2005, 1, 1), null, null, null, null);

    private static (StudentBulkImportProcessingService Service, FakeStudentRepository Students, FakeStudentBulkImportJobRepository Jobs, FakeStudentBulkImportRowRepository Rows) CreateService()
    {
        var students = new FakeStudentRepository();
        var jobs = new FakeStudentBulkImportJobRepository();
        var rows = new FakeStudentBulkImportRowRepository();
        var unitOfWork = new FakeUnitOfWork();

        var createStudentRecordService = new CreateStudentRecordService(
            students,
            new FakeOrganizationDepartmentExistenceChecker(),
            new FakeProgramExistenceChecker(),
            new FakeStudentNumberSequence(),
            new FakeUserProvisioningPort(),
            new FakeDocumentGenerationPort(),
            new FakeNotificationRequestPublisher(),
            unitOfWork,
            new FakeAuditRecorder(),
            new FakeClock(),
            NullLogger<CreateStudentRecordService>.Instance);

        var service = new StudentBulkImportProcessingService(
            jobs,
            rows,
            students,
            createStudentRecordService,
            unitOfWork,
            new FakeClock(),
            NullLogger<StudentBulkImportProcessingService>.Instance);

        return (service, students, jobs, rows);
    }

    [Fact]
    public async Task ProcessNextBatchAsync_processes_a_CREATE_row_and_completes_the_job()
    {
        var (service, students, jobs, rows) = CreateService();
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 1, DateTimeOffset.UtcNow).Value;
        job.RecordValidation(1, 0);
        job.Approve(DateTimeOffset.UtcNow);
        jobs.Add(job);
        var input = ValidCreateRow(Guid.NewGuid(), Guid.NewGuid());
        rows.AddRange([StudentBulkImportRow.CreateValid(job.Id, 1, System.Text.Json.JsonSerializer.Serialize(input))]);

        var result = await service.ProcessNextBatchAsync(job.Id.Value, 10);
        Assert.True(result.IsSuccess);
        // Empty-batch pass completes the job once every valid row has resolved.
        result = await service.ProcessNextBatchAsync(job.Id.Value, 10);

        Assert.True(result.IsSuccess);
        var reloadedJob = await jobs.GetByIdAsync(job.Id);
        Assert.Equal(StudentBulkImportJobStatus.Completed, reloadedJob!.Status);
        Assert.Equal(1, reloadedJob.SucceededCount);
        var reloadedRow = (await rows.ListByJobAsync(job.Id)).Single();
        Assert.Equal(StudentBulkImportRowStatus.Succeeded, reloadedRow.Status);
        Assert.NotNull(reloadedRow.ResultStudentId);
        Assert.NotNull(await students.GetByIdAsync(new StudentId(reloadedRow.ResultStudentId!.Value)));
    }

    [Fact]
    public async Task ProcessNextBatchAsync_processes_an_UPDATE_row_field_scoped_to_contact_info_only()
    {
        var (service, students, jobs, rows) = CreateService();
        var studentNumber = StudentNumber.FromIssuedSequence(2026, "CSE", 1);
        var originalName = PersonName.Create("Karim", "Hossain").Value;
        var existingStudent = Domain.Students.Student.Enroll(Guid.NewGuid(), studentNumber, Guid.NewGuid(), Guid.NewGuid(), originalName, Email.Create("karim@example.edu.bd").Value, null, new DateOnly(2005, 1, 1), "1234567890", DateTimeOffset.UtcNow);
        students.Seed(existingStudent);

        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 1, DateTimeOffset.UtcNow).Value;
        job.RecordValidation(1, 0);
        job.Approve(DateTimeOffset.UtcNow);
        jobs.Add(job);

        var input = new StudentBulkImportRowInput(null, studentNumber.Value, existingStudent.Version, null, null, null, null, null, null, null, null, null, null, null, null, "updated@example.edu.bd", "+8801812345678", null);
        rows.AddRange([StudentBulkImportRow.CreateValid(job.Id, 1, System.Text.Json.JsonSerializer.Serialize(input))]);

        await service.ProcessNextBatchAsync(job.Id.Value, 10);
        await service.ProcessNextBatchAsync(job.Id.Value, 10);

        var updated = await students.GetByIdAsync(existingStudent.Id);
        Assert.Equal("updated@example.edu.bd", updated!.ContactEmail);
        Assert.Equal("+8801812345678", updated.ContactPhone);
        // Field-scoped write - identity-bearing fields are never touched by an import row.
        Assert.Equal(originalName, updated.Name);
        var reloadedRow = (await rows.ListByJobAsync(job.Id)).Single();
        Assert.Equal(StudentBulkImportRowStatus.Succeeded, reloadedRow.Status);
        Assert.Equal(existingStudent.Id.Value, reloadedRow.ResultStudentId);
    }

    [Fact]
    public async Task ProcessNextBatchAsync_an_UPDATE_row_for_a_nonexistent_StudentNumber_fails_the_row_without_failing_the_batch()
    {
        var (service, _, jobs, rows) = CreateService();
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 1, DateTimeOffset.UtcNow).Value;
        job.RecordValidation(1, 0);
        job.Approve(DateTimeOffset.UtcNow);
        jobs.Add(job);

        var input = new StudentBulkImportRowInput(null, "NOSUCHNUMBER", 0, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        rows.AddRange([StudentBulkImportRow.CreateValid(job.Id, 1, System.Text.Json.JsonSerializer.Serialize(input))]);

        await service.ProcessNextBatchAsync(job.Id.Value, 10);
        var result = await service.ProcessNextBatchAsync(job.Id.Value, 10);

        Assert.True(result.IsSuccess);
        var reloadedJob = await jobs.GetByIdAsync(job.Id);
        Assert.Equal(StudentBulkImportJobStatus.CompletedWithErrors, reloadedJob!.Status);
        Assert.Equal(1, reloadedJob.FailedCount);
        var reloadedRow = (await rows.ListByJobAsync(job.Id)).Single();
        Assert.Equal(StudentBulkImportRowStatus.Failed, reloadedRow.Status);
        Assert.NotNull(reloadedRow.ErrorMessage);
    }
}
