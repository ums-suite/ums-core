using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Shared.Documents;

namespace UMS.Modules.Reporting.Application.RegulatoryReports;

/// <summary>
/// RPT-12/RPT-13: the worker-invoked execution of one <see cref="RegulatoryReportRun"/> - called
/// exclusively by <c>UMS.Workers.Reporting.RegulatoryReportRunRelayWorker</c>, never inline within
/// an HTTP request (requirement-spec.md §4 "Report generation is always async").
///
/// <para>
/// <b>Documented read-consistency mechanism (a deliberate divergence from edge-cases.md's literal
/// per-run live-source-module pinning):</b> field values are drawn from Reporting's OWN already-
/// computed <see cref="Domain.DashboardMetrics.DashboardMetric"/> projections (named by the
/// definition's own <c>SourceQueryReferencesJson</c>, e.g. <c>["academic-dashboard"]</c>), never a
/// fresh set of live cross-module calls issued at run time. Adding a true point-in-time <c>asOf</c>
/// parameter to all six new reporting-query contracts (plus every other source module's own query
/// contract) so a report run could pin a single live instant across eleven modules is a
/// disproportionate undertaking for this base flow - seeding it instead from the SAME already-
/// aggregated projections the six dashboards already serve keeps the mechanism internally
/// consistent with the "never a live cross-module join" invariant, and gives every run a real,
/// honest staleness bound: <see cref="RegulatoryReportRun.AsOf"/> is stamped as the OLDEST
/// (minimum) <c>data_as_of</c> among the dashboards a definition draws from - a conservative,
/// worst-case bound, never an optimistic best-case one. This is a genuine, named gap versus
/// edge-cases.md's literal resolution, not a silent narrowing.
/// </para>
///
/// <para>
/// edge-cases.md "date range predates tracked history": generalized here to "referenced source
/// dashboard not yet computed" - any field this run cannot resolve from an already-computed
/// dashboard is rendered as an explicit <c>N/A</c> marker (never a fabricated zero), and the
/// generated file's own header carries a note when any referenced source was unavailable.
/// </para>
/// </summary>
public sealed class RegulatoryReportRunExecutionService(
    IRegulatoryReportRunRepository runs,
    IDashboardMetricRepository dashboardMetrics,
    IDocumentGenerationRequester documentGenerationRequester,
    INotificationRequestPublisher notifications,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<RegulatoryReportRunExecutionService> logger)
{
    public async Task ExecuteAsync(RegulatoryReportRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);

        var started = run.Start(clock.UtcNow);
        if (started.IsFailure)
        {
            // Already picked up (or terminal) by a previous, since-crashed attempt at this same
            // poll pass - ADR-0014 idempotency: skip silently rather than double-execute.
            logger.LogInformation("RegulatoryReportRun {RunId}: skipped - not in a startable state ({Status}).", run.Id, run.Status);
            return;
        }

        // Persist the Running transition before the (potentially slower) generation work, so a
        // crash mid-generation leaves a visibly Running - not silently Pending - row behind.
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var snapshot = JsonSerializer.Deserialize<RegulatoryReportDefinitionSnapshot>(run.DefinitionSnapshotJson)
                ?? throw new InvalidOperationException("Empty RegulatoryReportDefinition snapshot.");

            if (run.Format == RegulatoryReportFormat.Excel)
            {
                // Mechanism #5(c): a documented, deterministic gap - never a silently wrong/empty file.
                run.Fail("Excel-format regulatory report generation is not implemented in this build (RPT-13, a documented gap) - request PDF or CSV instead.", clock.UtcNow);
            }
            else
            {
                var sourceKeys = JsonSerializer.Deserialize<List<string>>(snapshot.SourceQueryReferencesJson) ?? [];
                var (values, dataAsOf, anySourceMissing) = await GatherFieldValuesAsync(sourceKeys, snapshot.FieldSelections, cancellationToken).ConfigureAwait(false);

                if (run.Format == RegulatoryReportFormat.Csv)
                {
                    var csv = BuildCsv(snapshot, values, dataAsOf, anySourceMissing);
                    run.CompleteWithInlineCsv(csv, clock.UtcNow);
                }
                else
                {
                    var fields = BuildDocumentFields(snapshot, values, dataAsOf, anySourceMissing);
                    var generationResult = await documentGenerationRequester.RequestAsync(
                        new RequestDocumentGenerationCommand(
                            run.RequestedByUserId,
                            "RegulatoryReport",
                            run.Id.Value,
                            fields,
                            LanguageCode: null,
                            run.RequestedByUserId,
                            run.Id.Value.ToString()),
                        cancellationToken).ConfigureAwait(false);

                    if (generationResult.IsFailure)
                    {
                        run.Fail($"PDF generation failed: {generationResult.Error!.Message}", clock.UtcNow);
                    }
                    else
                    {
                        run.CompleteWithGeneratedDocument(generationResult.Value.DocumentId, clock.UtcNow);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "RegulatoryReportRun {RunId}: execution failed unexpectedly.", run.Id);
            run.Fail(ex.Message, clock.UtcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await PublishOutcomeNotificationAsync(run, cancellationToken).ConfigureAwait(false);
    }

    private async Task PublishOutcomeNotificationAsync(RegulatoryReportRun run, CancellationToken cancellationToken)
    {
        try
        {
            if (run.Status == RegulatoryReportRunStatus.Completed)
            {
                await notifications.PublishAsync(
                    new ReportingNotificationRequest(
                        "RegulatoryReportRunCompleted",
                        run.Id.Value.ToString(),
                        run.RequestedByUserId,
                        new Dictionary<string, string?> { ["definitionId"] = run.DefinitionId.Value.ToString() }),
                    cancellationToken).ConfigureAwait(false);
            }
            else if (run.Status == RegulatoryReportRunStatus.Failed)
            {
                await notifications.PublishAsync(
                    new ReportingNotificationRequest(
                        "RegulatoryReportRunFailed",
                        run.Id.Value.ToString(),
                        run.RequestedByUserId,
                        new Dictionary<string, string?> { ["error"] = run.ErrorMessage }),
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best-effort, mirrors every other module's own "notification outage never re-queues
            // an otherwise-resolved write" posture (e.g. Documents' DocumentGenerationRetryRelayWorker).
            logger.LogWarning(ex, "RegulatoryReportRun {RunId}: outcome notification publish failed - the run's own status is unaffected.", run.Id);
        }
    }

    private async Task<(Dictionary<string, string> Values, DateTimeOffset? DataAsOf, bool AnySourceMissing)> GatherFieldValuesAsync(
        IReadOnlyList<string> sourceKeys,
        IReadOnlyList<ReportFieldSelectionSnapshot> fields,
        CancellationToken cancellationToken)
    {
        var metrics = await dashboardMetrics.GetByKeysAsync(sourceKeys, cancellationToken).ConfigureAwait(false);
        var combined = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        DateTimeOffset? minAsOf = null;
        var anySourceMissing = false;

        foreach (var key in sourceKeys)
        {
            var metric = metrics.FirstOrDefault(m => m.Id == key);
            if (metric is null || !metric.HasEverBeenComputed)
            {
                anySourceMissing = true;
                continue;
            }

            if (minAsOf is null || metric.DataAsOf < minAsOf)
            {
                minAsOf = metric.DataAsOf;
            }

            var payload = JsonSerializer.Deserialize<JsonElement>(metric.PayloadJson!);
            foreach (var property in payload.EnumerateObject())
            {
                combined[property.Name] = property.Value;
            }
        }

        var values = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            values[field.FieldKey] = combined.TryGetValue(field.FieldKey, out var element)
                ? FormatJsonElement(element)
                : "N/A (not available in current data)";
        }

        return (values, minAsOf, anySourceMissing);
    }

    private static string FormatJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null => string.Empty,
        _ => element.GetRawText(),
    };

    private static string BuildCsv(RegulatoryReportDefinitionSnapshot snapshot, IReadOnlyDictionary<string, string> values, DateTimeOffset? dataAsOf, bool anySourceMissing)
    {
        var builder = new StringBuilder();
        builder.Append("# Report: ").Append(EscapeCsv(snapshot.Name)).Append('\n');
        builder.Append("# data_as_of: ").Append(dataAsOf?.ToString("O") ?? "not yet computed").Append('\n');
        if (anySourceMissing)
        {
            builder.Append("# Note: one or more referenced source dashboards have not yet been computed - affected fields show N/A.\n");
        }

        var orderedFields = snapshot.FieldSelections.OrderBy(f => f.Ordinal).ToList();
        builder.Append(string.Join(',', orderedFields.Select(f => EscapeCsv(f.Label)))).Append('\n');
        builder.Append(string.Join(',', orderedFields.Select(f => EscapeCsv(values.GetValueOrDefault(f.FieldKey, "N/A"))))).Append('\n');
        return builder.ToString();
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private static IReadOnlyDictionary<string, string> BuildDocumentFields(RegulatoryReportDefinitionSnapshot snapshot, IReadOnlyDictionary<string, string> values, DateTimeOffset? dataAsOf, bool anySourceMissing)
    {
        var fields = new Dictionary<string, string>
        {
            ["reportName"] = snapshot.Name,
            ["dataAsOf"] = dataAsOf?.ToString("O") ?? "not yet computed",
            ["staleDataNote"] = anySourceMissing ? "One or more referenced source dashboards have not yet been computed." : string.Empty,
        };

        foreach (var field in snapshot.FieldSelections.OrderBy(f => f.Ordinal))
        {
            fields[field.FieldKey] = values.GetValueOrDefault(field.FieldKey, "N/A");
        }

        return fields;
    }
}
