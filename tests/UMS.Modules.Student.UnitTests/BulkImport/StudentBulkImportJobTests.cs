using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.UnitTests.BulkImport;

/// <summary>STU-15/STU-16 (requirement-spec.md student §2 Bulk Import; design-decisions.md "Bulk-Import Concurrency &amp; Field-Scoping Design").</summary>
public sealed class StudentBulkImportJobTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void Create_with_zero_rows_fails()
    {
        var result = StudentBulkImportJob.Create(Guid.NewGuid(), 0, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.rows_required", result.Error!.Code);
    }

    [Fact]
    public void Create_starts_in_Uploaded_status()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 5, Now).Value;

        Assert.Equal(StudentBulkImportJobStatus.Uploaded, job.Status);
        Assert.Equal(5, job.TotalRows);
    }

    [Fact]
    public void Approve_before_Validate_fails()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 5, Now).Value;

        var result = job.Approve(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.not_awaiting_approval", result.Error!.Code);
    }

    [Fact]
    public void Approve_with_zero_valid_rows_fails()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 3, Now).Value;
        job.RecordValidation(validCount: 0, invalidCount: 3);

        var result = job.Approve(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.no_valid_rows", result.Error!.Code);
    }

    [Fact]
    public void Approve_after_Validate_with_valid_rows_succeeds()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 3, Now).Value;
        job.RecordValidation(validCount: 2, invalidCount: 1);

        var result = job.Approve(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudentBulkImportJobStatus.Approved, job.Status);
        Assert.NotNull(job.ApprovedAt);
    }

    [Fact]
    public void MarkProcessing_on_a_terminal_job_fails()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 1, Now).Value;
        job.RecordValidation(1, 0);
        job.Approve(Now);
        job.MarkProcessing();
        job.RecordRowOutcome(succeeded: true);
        job.Complete(Now);

        var result = job.MarkProcessing();

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.already_terminal", result.Error!.Code);
    }

    [Fact]
    public void Complete_before_all_valid_rows_resolved_fails()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 2, Now).Value;
        job.RecordValidation(2, 0);
        job.Approve(Now);
        job.MarkProcessing();
        job.RecordRowOutcome(succeeded: true);

        var result = job.Complete(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("student_bulk_import_job.incomplete", result.Error!.Code);
    }

    [Fact]
    public void Complete_with_zero_failures_resolves_to_Completed()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 2, Now).Value;
        job.RecordValidation(2, 0);
        job.Approve(Now);
        job.MarkProcessing();
        job.RecordRowOutcome(succeeded: true);
        job.RecordRowOutcome(succeeded: true);

        var result = job.Complete(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudentBulkImportJobStatus.Completed, job.Status);
    }

    [Fact]
    public void Complete_with_at_least_one_failure_resolves_to_CompletedWithErrors()
    {
        var job = StudentBulkImportJob.Create(Guid.NewGuid(), 2, Now).Value;
        job.RecordValidation(2, 0);
        job.Approve(Now);
        job.MarkProcessing();
        job.RecordRowOutcome(succeeded: true);
        job.RecordRowOutcome(succeeded: false);

        var result = job.Complete(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(StudentBulkImportJobStatus.CompletedWithErrors, job.Status);
    }
}
