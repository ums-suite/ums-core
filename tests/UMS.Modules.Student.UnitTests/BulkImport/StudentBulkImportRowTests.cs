using UMS.Modules.Student.Domain.BulkImport;

namespace UMS.Modules.Student.UnitTests.BulkImport;

public sealed class StudentBulkImportRowTests
{
    [Fact]
    public void CreateValid_starts_in_Valid_status_with_no_error()
    {
        var jobId = StudentBulkImportJobId.New();
        var row = StudentBulkImportRow.CreateValid(jobId, 1, "{}");

        Assert.Equal(StudentBulkImportRowStatus.Valid, row.Status);
        Assert.Null(row.ErrorMessage);
        Assert.Equal(1, row.RowNumber);
    }

    [Fact]
    public void CreateInvalid_starts_in_Invalid_status_with_the_validation_message()
    {
        var jobId = StudentBulkImportJobId.New();
        var row = StudentBulkImportRow.CreateInvalid(jobId, 2, "{}", "Missing required field(s): Email.");

        Assert.Equal(StudentBulkImportRowStatus.Invalid, row.Status);
        Assert.Equal("Missing required field(s): Email.", row.ErrorMessage);
    }

    [Fact]
    public void MarkSucceeded_sets_the_result_student_id_and_clears_any_error()
    {
        var row = StudentBulkImportRow.CreateValid(StudentBulkImportJobId.New(), 1, "{}");
        row.MarkProcessing();
        var studentId = Guid.NewGuid();

        row.MarkSucceeded(studentId);

        Assert.Equal(StudentBulkImportRowStatus.Succeeded, row.Status);
        Assert.Equal(studentId, row.ResultStudentId);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_is_terminal_no_retry_loop()
    {
        var row = StudentBulkImportRow.CreateValid(StudentBulkImportJobId.New(), 1, "{}");
        row.MarkProcessing();

        row.MarkFailed("version conflict");

        Assert.Equal(StudentBulkImportRowStatus.Failed, row.Status);
        Assert.Equal("version conflict", row.ErrorMessage);
    }
}
