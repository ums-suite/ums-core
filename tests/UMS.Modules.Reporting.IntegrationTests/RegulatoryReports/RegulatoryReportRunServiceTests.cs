using Microsoft.Extensions.DependencyInjection;
using UMS.Modules.Reporting.Application.Common;
using UMS.Modules.Reporting.Application.RegulatoryReports;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Modules.Reporting.IntegrationTests.Infrastructure;

namespace UMS.Modules.Reporting.IntegrationTests.RegulatoryReports;

/// <summary>RPT-12/RPT-15/RPT-16: enqueueing a run is fully synchronous (Pending row + Audit entry) and returns immediately - the actual generation happens only via <c>RegulatoryReportRunExecutionService</c>, exercised separately.</summary>
[Collection(ReportingTestCollectionDefinition.Name)]
public sealed class RegulatoryReportRunServiceTests(ReportingServiceFixture fixture)
{
    private static AuditContext NewAuditContext() => new(Guid.NewGuid(), "127.0.0.1", Guid.NewGuid().ToString());

    private async Task<Guid> CreateActiveDefinitionAsync(RegulatoryReportFormat supportedFormats)
    {
        using var scope = fixture.Services.CreateScope();
        var definitions = scope.ServiceProvider.GetRequiredService<RegulatoryReportDefinitionService>();
        var created = await definitions.CreateAsync(
            new CreateRegulatoryReportDefinitionRequest(
                $"Run Test {Guid.NewGuid():N}",
                RegulatoryReportCategory.StudentEnrollment,
                [new FieldSelectionRequest("TotalEnrollments", "Total Enrollments")],
                "{}",
                "[\"academic-dashboard\"]",
                supportedFormats),
            NewAuditContext());

        Assert.True(created.IsSuccess);
        return created.Value.Id;
    }

    [Fact]
    public async Task Enqueueing_a_run_always_returns_a_run_id_immediately_as_Pending()
    {
        var definitionId = await CreateActiveDefinitionAsync(RegulatoryReportFormat.Csv);

        using var scope = fixture.Services.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunService>();

        var enqueued = await runService.EnqueueAsync(definitionId, "{}", RegulatoryReportFormat.Csv, NewAuditContext());

        Assert.True(enqueued.IsSuccess);
        Assert.Null(enqueued.Value.InFlightDuplicateRunId);

        var status = await runService.GetStatusAsync(enqueued.Value.RunId);
        Assert.True(status.IsSuccess);
        Assert.Equal("Pending", status.Value.Status);
        Assert.Contains(fixture.AuditRecorder.Recorded, r => r.EntityType == "RegulatoryReportRun" && r.Action == "Requested");
    }

    [Fact]
    public async Task Requesting_an_unsupported_format_is_rejected_before_any_row_is_created()
    {
        var definitionId = await CreateActiveDefinitionAsync(RegulatoryReportFormat.Csv);

        using var scope = fixture.Services.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunService>();

        var result = await runService.EnqueueAsync(definitionId, "{}", RegulatoryReportFormat.Pdf, NewAuditContext());

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_run.unsupported_format", result.Error!.Code);
    }

    [Fact]
    public async Task A_second_run_with_identical_parameters_surfaces_a_courtesy_notice_but_is_never_blocked()
    {
        var definitionId = await CreateActiveDefinitionAsync(RegulatoryReportFormat.Csv);

        using var scope = fixture.Services.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunService>();

        var first = await runService.EnqueueAsync(definitionId, "{\"x\":1}", RegulatoryReportFormat.Csv, NewAuditContext());
        Assert.True(first.IsSuccess);

        // RPT-15/design-decisions.md "No Hard Dedup in v1": a second identical-parameter request
        // still succeeds and gets its OWN run id - the first run's id is surfaced only as an
        // informational courtesy notice.
        var second = await runService.EnqueueAsync(definitionId, "{\"x\":1}", RegulatoryReportFormat.Csv, NewAuditContext());

        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value.RunId, second.Value.RunId);
        Assert.Equal(first.Value.RunId, second.Value.InFlightDuplicateRunId);
    }

    [Fact]
    public async Task GetStatus_for_an_unknown_run_is_NotFound()
    {
        using var scope = fixture.Services.CreateScope();
        var runService = scope.ServiceProvider.GetRequiredService<RegulatoryReportRunService>();

        var result = await runService.GetStatusAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_run.not_found", result.Error!.Code);
    }
}
