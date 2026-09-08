using UMS.Modules.Reporting.Domain.RegulatoryReports;

namespace UMS.Modules.Reporting.UnitTests.RegulatoryReports;

public sealed class RegulatoryReportRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

    private static readonly RegulatoryReportDefinitionSnapshot Snapshot = new(
        Guid.NewGuid(),
        "Student Enrollment",
        RegulatoryReportCategory.StudentEnrollment,
        [new ReportFieldSelectionSnapshot("TotalEnrollments", "Total Enrollments", 0)],
        "{}",
        "[\"academic-dashboard\"]",
        RegulatoryReportFormat.Csv);

    [Fact]
    public void Create_starts_Pending_and_freezes_the_snapshot_as_json()
    {
        var definitionId = new RegulatoryReportDefinitionId(Snapshot.DefinitionId);
        var run = RegulatoryReportRun.Create(definitionId, Snapshot, "{}", RegulatoryReportFormat.Csv, Guid.NewGuid(), Now);

        Assert.Equal(RegulatoryReportRunStatus.Pending, run.Status);
        Assert.Contains("Student Enrollment", run.DefinitionSnapshotJson);
        Assert.Equal(Now, run.RequestedAt);
        Assert.False(run.IsTerminal);
    }

    [Fact]
    public void ComputeParametersHash_is_stable_for_the_same_definition_and_parameters()
    {
        var definitionId = new RegulatoryReportDefinitionId(Guid.NewGuid());

        var first = RegulatoryReportRun.ComputeParametersHash(definitionId, "{\"a\":1}");
        var second = RegulatoryReportRun.ComputeParametersHash(definitionId, "{\"a\":1}");
        var differentParams = RegulatoryReportRun.ComputeParametersHash(definitionId, "{\"a\":2}");

        Assert.Equal(first, second);
        Assert.NotEqual(first, differentParams);
    }

    [Fact]
    public void Start_transitions_Pending_to_Running_and_stamps_AsOf()
    {
        var run = CreatePendingRun();

        var result = run.Start(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegulatoryReportRunStatus.Running, run.Status);
        Assert.Equal(Now, run.AsOf);
        Assert.Equal(Now, run.StartedAt);
    }

    [Fact]
    public void Start_is_rejected_once_already_started_ADR_0014_idempotency()
    {
        var run = CreatePendingRun();
        run.Start(Now);

        var second = run.Start(Now.AddSeconds(1));

        Assert.True(second.IsFailure);
        Assert.Equal("regulatory_report_run.invalid_transition", second.Error!.Code);
        // The first Start's own AsOf is untouched by the rejected second attempt.
        Assert.Equal(Now, run.AsOf);
    }

    [Fact]
    public void CompleteWithInlineCsv_requires_Running_and_raises_RegulatoryReportRunCompleted()
    {
        var run = CreatePendingRun();
        run.Start(Now);

        var result = run.CompleteWithInlineCsv("field,value\n", Now.AddSeconds(5), Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(RegulatoryReportRunStatus.Completed, run.Status);
        Assert.Equal("field,value\n", run.ResultCsvContent);
        Assert.Null(run.ResultDocumentId);
        Assert.True(run.IsTerminal);
    }

    [Fact]
    public void CompleteWithInlineCsv_stamps_AsOf_with_the_execution_service_s_own_conservative_bound_never_the_Start_wall_clock()
    {
        var run = CreatePendingRun();
        run.Start(Now);

        run.CompleteWithInlineCsv("field,value\n", Now.AddSeconds(5), Now.AddDays(-2));

        Assert.Equal(Now.AddDays(-2), run.AsOf);
    }

    [Fact]
    public void CompleteWithGeneratedDocument_requires_Running()
    {
        var run = CreatePendingRun();

        var result = run.CompleteWithGeneratedDocument(Guid.NewGuid(), Now, Now);

        Assert.True(result.IsFailure);
        Assert.Equal("regulatory_report_run.invalid_transition", result.Error!.Code);
    }

    [Fact]
    public void CompleteWithGeneratedDocument_sets_the_Documents_owned_id_never_the_inline_CSV_field()
    {
        var run = CreatePendingRun();
        run.Start(Now);
        var documentId = Guid.NewGuid();

        run.CompleteWithGeneratedDocument(documentId, Now.AddSeconds(5), Now);

        Assert.Equal(documentId, run.ResultDocumentId);
        Assert.Null(run.ResultCsvContent);
    }

    [Fact]
    public void CompleteWithGeneratedDocument_accepts_a_null_data_as_of_when_no_source_was_ever_computed()
    {
        var run = CreatePendingRun();
        run.Start(Now);

        run.CompleteWithGeneratedDocument(Guid.NewGuid(), Now.AddSeconds(5), dataAsOf: null);

        Assert.Null(run.AsOf);
    }

    [Fact]
    public void Fail_is_allowed_from_any_non_terminal_state_and_never_touches_result_fields()
    {
        var run = CreatePendingRun();

        run.Fail("boom", Now);

        Assert.Equal(RegulatoryReportRunStatus.Failed, run.Status);
        Assert.Equal("boom", run.ErrorMessage);
        Assert.Null(run.ResultCsvContent);
        Assert.Null(run.ResultDocumentId);
        Assert.True(run.IsTerminal);
    }

    private static RegulatoryReportRun CreatePendingRun() =>
        RegulatoryReportRun.Create(new RegulatoryReportDefinitionId(Snapshot.DefinitionId), Snapshot, "{}", RegulatoryReportFormat.Csv, Guid.NewGuid(), Now);
}
