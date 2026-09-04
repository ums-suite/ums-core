using UMS.Modules.Audit.Domain.Exports;

namespace UMS.Modules.Audit.UnitTests.Exports;

public class AuditExportRequestTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-04T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public void Create_starts_in_pending_status()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);

        Assert.Equal(ExportStatus.Pending, request.Status);
    }

    [Fact]
    public void MarkProcessing_from_pending_succeeds()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);

        var result = request.MarkProcessing();

        Assert.True(result.IsSuccess);
        Assert.Equal(ExportStatus.Processing, request.Status);
    }

    [Fact]
    public void MarkProcessing_twice_fails_the_second_time()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);
        request.MarkProcessing();

        var result = request.MarkProcessing();

        Assert.True(result.IsFailure);
        Assert.Equal("audit_export.invalid_transition", result.Error!.Code);
    }

    [Fact]
    public void MarkCompleted_before_processing_fails()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);

        var result = request.MarkCompleted("audit-exports/x.csv", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_export.invalid_transition", result.Error!.Code);
    }

    [Fact]
    public void MarkCompleted_after_processing_succeeds_and_records_the_object_key()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);
        request.MarkProcessing();

        var result = request.MarkCompleted("audit-exports/x.csv", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExportStatus.Completed, request.Status);
        Assert.Equal("audit-exports/x.csv", request.ResultObjectKey);
        Assert.Equal(Now, request.CompletedAt);
    }

    [Fact]
    public void MarkFailed_after_completed_fails_a_terminal_status_cannot_be_overwritten()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);
        request.MarkProcessing();
        request.MarkCompleted("audit-exports/x.csv", Now);

        var result = request.MarkFailed("boom", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_export.invalid_transition", result.Error!.Code);
    }

    [Fact]
    public void MarkFailed_from_pending_or_processing_succeeds()
    {
        var request = AuditExportRequest.Create(Guid.NewGuid(), "{}", ExportFormat.Csv, Now);

        var result = request.MarkFailed("boom", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(ExportStatus.Failed, request.Status);
        Assert.Equal("boom", request.ErrorMessage);
    }
}
