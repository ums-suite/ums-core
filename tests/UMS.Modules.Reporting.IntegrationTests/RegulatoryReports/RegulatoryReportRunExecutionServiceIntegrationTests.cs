using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Modules.Reporting.IntegrationTests.Infrastructure;

namespace UMS.Modules.Reporting.IntegrationTests.RegulatoryReports;

/// <summary>
/// RPT-12/RPT-13 end to end, mirroring exactly what <c>UMS.Workers.Reporting.RegulatoryReportRunRelayWorker</c>
/// does on every poll pass: fetch a real <c>Pending</c> row via <see cref="IRegulatoryReportRunRepository.GetPendingAsync"/>,
/// drive it through <see cref="RegulatoryReportRunExecutionService"/>, and persist the outcome to a
/// real Postgres row.
/// </summary>
[Collection(ReportingTestCollectionDefinition.Name)]
public sealed class RegulatoryReportRunExecutionServiceIntegrationTests(ReportingServiceFixture fixture)
{
    private static AuditContext NewAuditContext() => new(Guid.NewGuid(), "127.0.0.1", Guid.NewGuid().ToString());

    private async Task<Guid> EnqueueRunAsync(RegulatoryReportFormat format, string sourceQueryReferencesJson = "[\"academic-dashboard\"]")
    {
        using var scope = fixture.Services.CreateScope();
        var definitions = scope.ServiceProvider.GetRequiredService<RegulatoryReportDefinitionService>();
        var created = await definitions.CreateAsync(
            new CreateRegulatoryReportDefinitionRequest(
                $"Execution Test {Guid.NewGuid():N}",
                RegulatoryReportCategory.StudentEnrollment,
                [new FieldSelectionRequest("TotalEnrollments", "Total Enrollments")],
                "{}",
                sourceQueryReferencesJson,
                RegulatoryReportFormat.Pdf | RegulatoryReportFormat.Csv | RegulatoryReportFormat.Excel),
            NewAuditContext());
        Assert.True(created.IsSuccess);

        var runService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunService>();
        var enqueued = await runService.EnqueueAsync(created.Value.Id, "{}", format, NewAuditContext());
        Assert.True(enqueued.IsSuccess);
        return enqueued.Value.RunId;
    }

    [Fact]
    public async Task A_Pending_CSV_run_is_picked_up_and_completes_with_inline_content()
    {
        // A real, already-computed source dashboard row for the run to draw from.
        using (var seedScope = fixture.Services.CreateScope())
        {
            var academicRefresh = seedScope.ServiceProvider.GetRequiredService<UMS.Modules.Reporting.Application.DashboardMetrics.AcademicDashboardRefreshService>();
            await academicRefresh.RunAsync();
        }

        var runId = await EnqueueRunAsync(RegulatoryReportFormat.Csv);

        using var scope = fixture.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRegulatoryReportRunRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunExecutionService>();

        var pending = await runs.GetPendingAsync(10);
        var run = Assert.Single(pending, r => r.Id.Value == runId);

        await executionService.ExecuteAsync(run);

        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
        Assert.NotNull(run.ResultCsvContent);
        Assert.Contains(fixture.NotificationIntake.Submitted, s => s.EventType == "RegulatoryReportRunCompleted");

        // Persisted, not just held in memory - a fresh read confirms the completed status survived.
        var reread = await runs.GetByIdAsync(run.Id);
        Assert.Equal(RegulatoryReportRunStatus.Completed, reread!.Status);
    }

    [Fact]
    public async Task A_Pending_PDF_run_delegates_to_Documents_and_completes_with_a_document_id()
    {
        using (var seedScope = fixture.Services.CreateScope())
        {
            var academicRefresh = seedScope.ServiceProvider.GetRequiredService<UMS.Modules.Reporting.Application.DashboardMetrics.AcademicDashboardRefreshService>();
            await academicRefresh.RunAsync();
        }

        var runId = await EnqueueRunAsync(RegulatoryReportFormat.Pdf);

        using var scope = fixture.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRegulatoryReportRunRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunExecutionService>();

        var run = await runs.GetByIdAsync(new RegulatoryReportRunId(runId));
        await executionService.ExecuteAsync(run!);

        Assert.Equal(RegulatoryReportRunStatus.Completed, run!.Status);
        Assert.NotNull(run.ResultDocumentId);
        Assert.Equal("RegulatoryReport", fixture.DocumentGenerationRequester.LastCommand!.DocumentType);
    }

    [Fact]
    public async Task A_Pending_Excel_run_fails_deterministically_with_the_documented_gap_message()
    {
        var runId = await EnqueueRunAsync(RegulatoryReportFormat.Excel);

        using var scope = fixture.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRegulatoryReportRunRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunExecutionService>();

        var run = await runs.GetByIdAsync(new RegulatoryReportRunId(runId));
        await executionService.ExecuteAsync(run!);

        Assert.Equal(RegulatoryReportRunStatus.Failed, run!.Status);
        Assert.Contains("Excel", run.ErrorMessage);
        Assert.Contains(fixture.NotificationIntake.Submitted, s => s.EventType == "RegulatoryReportRunFailed");
    }

    [Fact]
    public async Task GetPendingAsync_never_returns_an_already_terminal_run()
    {
        var runId = await EnqueueRunAsync(RegulatoryReportFormat.Csv);

        using var scope = fixture.Services.CreateScope();
        var runs = scope.ServiceProvider.GetRequiredService<IRegulatoryReportRunRepository>();
        var executionService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunExecutionService>();

        var run = await runs.GetByIdAsync(new RegulatoryReportRunId(runId));
        await executionService.ExecuteAsync(run!);
        Assert.True(run!.IsTerminal);

        var pendingAfter = await runs.GetPendingAsync(50);
        Assert.DoesNotContain(pendingAfter, r => r.Id.Value == runId);
    }
}
