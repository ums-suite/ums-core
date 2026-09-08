using Microsoft.Extensions.Logging.Abstractions;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.DashboardMetrics;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Modules.Reporting.UnitTests.Fakes;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.UnitTests.RegulatoryReports;

/// <summary>RPT-12/RPT-13: the worker-invoked execution service - CSV building, the documented Excel gap, N/A handling for an unavailable source dashboard, and the conservative (minimum) data_as_of bound across multiple source dashboards.</summary>
public sealed class RegulatoryReportRunExecutionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_CSV_run_completes_inline_with_a_header_data_as_of_and_the_resolved_field_values()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var academic = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now.AddHours(-1));
        academic.RecordSuccessfulRefresh("{\"TotalEnrollments\":500}", Now.AddHours(-1));
        dashboardMetrics.Add(academic);

        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Csv);
        var service = CreateService(dashboardMetrics, out _, out var notifications);

        await service.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
        Assert.NotNull(run.ResultCsvContent);
        Assert.Contains("Total Enrollments", run.ResultCsvContent);
        Assert.Contains("500", run.ResultCsvContent);
        Assert.Equal(Now.AddHours(-1), run.AsOf);
        Assert.Single(notifications.Published);
        Assert.Equal("RegulatoryReportRunCompleted", notifications.Published[0].EventType);
    }

    [Fact]
    public async Task A_field_whose_source_dashboard_was_never_computed_renders_as_an_explicit_NA_marker()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Csv);
        var service = CreateService(dashboardMetrics, out _, out _);

        await service.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
        Assert.Contains("N/A", run.ResultCsvContent);
        Assert.Contains("not yet computed", run.ResultCsvContent);
        Assert.Null(run.AsOf);
    }

    [Fact]
    public async Task The_data_as_of_bound_is_the_OLDEST_of_multiple_referenced_source_dashboards()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var older = DashboardMetric.Create("financial-dashboard", "Financial Dashboard", Now.AddDays(-2));
        older.RecordSuccessfulRefresh("{\"TotalCollection\":1000}", Now.AddDays(-2));
        dashboardMetrics.Add(older);

        var newer = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now.AddHours(-1));
        newer.RecordSuccessfulRefresh("{\"TotalEnrollments\":500}", Now.AddHours(-1));
        dashboardMetrics.Add(newer);

        var run = CreateRun(
            ["academic-dashboard", "financial-dashboard"],
            [("TotalEnrollments", "Total Enrollments"), ("TotalCollection", "Total Collection")],
            RegulatoryReportFormat.Csv);
        var service = CreateService(dashboardMetrics, out _, out _);

        await service.ExecuteAsync(run);

        // A conservative, worst-case staleness bound - never the newer/optimistic value.
        Assert.Equal(Now.AddDays(-2), run.AsOf);
    }

    [Fact]
    public async Task An_Excel_format_run_deterministically_fails_with_a_named_gap_never_a_wrong_or_empty_file()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Excel);
        var service = CreateService(dashboardMetrics, out var documentRequester, out var notifications);

        await service.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Failed, run.Status);
        Assert.Contains("Excel", run.ErrorMessage);
        Assert.Contains("not implemented", run.ErrorMessage);
        Assert.Null(documentRequester.LastCommand);
        Assert.Equal("RegulatoryReportRunFailed", notifications.Published.Single().EventType);
    }

    [Fact]
    public async Task A_PDF_run_delegates_to_IDocumentGenerationRequester_and_records_the_returned_document_id()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var academic = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);
        academic.RecordSuccessfulRefresh("{\"TotalEnrollments\":500}", Now);
        dashboardMetrics.Add(academic);

        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Pdf);
        var service = CreateService(dashboardMetrics, out var documentRequester, out _);
        var expectedDocumentId = Guid.NewGuid();
        documentRequester.NextResult = new UMS.Shared.Documents.GeneratedDocumentSummary(expectedDocumentId, "Ready", null);

        await service.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
        Assert.Equal(expectedDocumentId, run.ResultDocumentId);
        Assert.NotNull(documentRequester.LastCommand);
        Assert.Equal("RegulatoryReport", documentRequester.LastCommand!.DocumentType);
        Assert.Equal("500", documentRequester.LastCommand.Fields["TotalEnrollments"]);
    }

    [Fact]
    public async Task A_PDF_generation_failure_fails_the_run_with_the_underlying_reason()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Pdf);
        var service = CreateService(dashboardMetrics, out var documentRequester, out _);
        documentRequester.NextResult = Error.Failure("document.generation_failed", "renderer unavailable");

        await service.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Failed, run.Status);
        Assert.Contains("renderer unavailable", run.ErrorMessage);
    }

    [Fact]
    public async Task A_run_already_past_Pending_is_skipped_silently_ADR_0014_idempotency()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Csv);
        var service = CreateService(dashboardMetrics, out _, out var notifications);

        await service.ExecuteAsync(run);
        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
        notifications.Published.Clear();

        // A second poll pass (e.g. a crashed-then-retried worker) must never re-execute a terminal run.
        await service.ExecuteAsync(run);

        Assert.Empty(notifications.Published);
    }

    [Fact]
    public async Task A_Notifications_outage_never_affects_the_run_s_own_already_persisted_outcome()
    {
        var dashboardMetrics = new FakeDashboardMetricRepository();
        var academic = DashboardMetric.Create("academic-dashboard", "Academic Dashboard", Now);
        academic.RecordSuccessfulRefresh("{\"TotalEnrollments\":500}", Now);
        dashboardMetrics.Add(academic);

        var run = CreateRun(["academic-dashboard"], [("TotalEnrollments", "Total Enrollments")], RegulatoryReportFormat.Csv);
        var service = CreateService(dashboardMetrics, out _, out var notifications);
        notifications.ThrowOnPublish = new InvalidOperationException("notifications down");

        await service.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
    }

    private static RegulatoryReportRun CreateRun(IReadOnlyList<string> sourceKeys, IReadOnlyList<(string FieldKey, string Label)> fields, RegulatoryReportFormat format)
    {
        var snapshot = new RegulatoryReportDefinitionSnapshot(
            Guid.NewGuid(),
            "Test Report",
            RegulatoryReportCategory.StudentEnrollment,
            fields.Select((f, i) => new ReportFieldSelectionSnapshot(f.FieldKey, f.Label, i)).ToList(),
            "{}",
            System.Text.Json.JsonSerializer.Serialize(sourceKeys),
            format);

        return RegulatoryReportRun.Create(new RegulatoryReportDefinitionId(snapshot.DefinitionId), snapshot, "{}", format, Guid.NewGuid(), Now);
    }

    private static RegulatoryReportRunExecutionService CreateService(
        FakeDashboardMetricRepository dashboardMetrics,
        out FakeDocumentGenerationRequester documentRequester,
        out FakeNotificationRequestPublisher notifications)
    {
        documentRequester = new FakeDocumentGenerationRequester();
        notifications = new FakeNotificationRequestPublisher();
        return new RegulatoryReportRunExecutionService(
            dashboardMetrics,
            documentRequester,
            notifications,
            new FakeUnitOfWork(),
            new FakeClock(Now),
            NullLogger<RegulatoryReportRunExecutionService>.Instance);
    }
}
