using UMS.Modules.Student.Application.BulkImport;
using UMS.Modules.Student.UnitTests.TestDoubles;

namespace UMS.Modules.Student.UnitTests.BulkImport;

/// <summary>STU-15/STU-16: the synchronous Upload/Validate/Preview-errors/Approve steps and the report query (requirement-spec.md student §2 Bulk Import, §6).</summary>
public sealed class StudentBulkImportServiceTests
{
    private static StudentBulkImportRowInput ValidCreateRow() => new(
        Guid.NewGuid(), null, null, 2026, "CSE", Guid.NewGuid(), Guid.NewGuid(), "Rahim", "Uddin", null, null,
        $"student-{Guid.NewGuid():N}@example.edu.bd", null, new DateOnly(2005, 1, 1), null, null, null, null);

    private static StudentBulkImportRowInput InvalidCreateRow() => new(
        null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    private static StudentBulkImportRowInput ValidUpdateRow(string studentNumber, uint expectedVersion) => new(
        null, studentNumber, expectedVersion, null, null, null, null, null, null, null, null, null, null, null, null, "new@example.edu.bd", null, null);

    private static StudentBulkImportRowInput InvalidUpdateRowMissingVersion(string studentNumber) => new(
        null, studentNumber, null, null, null, null, null, null, null, null, null, null, null, null, null, "new@example.edu.bd", null, null);

    private static (StudentBulkImportService Service, FakeStudentBulkImportJobRepository Jobs, FakeStudentBulkImportRowRepository Rows) CreateService()
    {
        var jobs = new FakeStudentBulkImportJobRepository();
        var rows = new FakeStudentBulkImportRowRepository();
        var service = new StudentBulkImportService(jobs, rows, new FakeUnitOfWork(), new FakeClock());
        return (service, jobs, rows);
    }

    [Fact]
    public async Task UploadAsync_with_no_rows_fails()
    {
        var (service, _, _) = CreateService();

        var result = await service.UploadAsync(Guid.NewGuid(), new UploadStudentBulkImportRequest([]));

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import.rows_required", result.Error!.Code);
    }

    [Fact]
    public async Task UploadAsync_classifies_valid_and_invalid_rows_and_the_job_stays_in_Validated()
    {
        var (service, _, _) = CreateService();
        var rows = new[] { ValidCreateRow(), InvalidCreateRow(), ValidUpdateRow("CSE-2026-1", 5), InvalidUpdateRowMissingVersion("CSE-2026-2") };

        var result = await service.UploadAsync(Guid.NewGuid(), new UploadStudentBulkImportRequest(rows));

        Assert.True(result.IsSuccess);
        Assert.Equal("Validated", result.Value.Status);
        Assert.Equal(4, result.Value.TotalRows);
        Assert.Equal(2, result.Value.ValidRowCount);
        Assert.Equal(2, result.Value.InvalidRowCount);
    }

    /// <summary>requirement-spec.md §9 decision 4: an invalid row never rejects the whole batch - "Preview errors" surfaces it, valid rows still get approved/processed.</summary>
    [Fact]
    public async Task GetReportAsync_surfaces_every_row_including_its_own_validation_error()
    {
        var (service, _, _) = CreateService();
        var upload = await service.UploadAsync(Guid.NewGuid(), new UploadStudentBulkImportRequest([ValidCreateRow(), InvalidCreateRow()]));

        var report = await service.GetReportAsync(upload.Value.Id);

        Assert.True(report.IsSuccess);
        Assert.Equal(2, report.Value.Rows.Count);
        Assert.Contains(report.Value.Rows, r => r.Status == "Valid" && r.ErrorMessage is null);
        Assert.Contains(report.Value.Rows, r => r.Status == "Invalid" && r.ErrorMessage is not null);
    }

    [Fact]
    public async Task ApproveAsync_before_Upload_completes_returns_NotFound_for_an_unknown_job()
    {
        var (service, _, _) = CreateService();

        var result = await service.ApproveAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ApproveAsync_with_only_invalid_rows_fails()
    {
        var (service, _, _) = CreateService();
        var upload = await service.UploadAsync(Guid.NewGuid(), new UploadStudentBulkImportRequest([InvalidCreateRow()]));

        var result = await service.ApproveAsync(upload.Value.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.no_valid_rows", result.Error!.Code);
    }

    [Fact]
    public async Task ApproveAsync_with_at_least_one_valid_row_succeeds()
    {
        var (service, _, _) = CreateService();
        var upload = await service.UploadAsync(Guid.NewGuid(), new UploadStudentBulkImportRequest([ValidCreateRow(), InvalidCreateRow()]));

        var result = await service.ApproveAsync(upload.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal("Approved", result.Value.Status);
    }
}
