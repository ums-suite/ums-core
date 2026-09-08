using System.Text.Json;
using UMS.Modules.Reporting.Application.Abstractions;
using UMS.Modules.Reporting.Application.Common;
using UMS.Modules.Reporting.Domain.RegulatoryReports;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.Application.RegulatoryReports;

/// <summary>
/// RPT-12: <c>POST /regulatory-reports/{definitionId}/run</c> - always enqueues, never generates
/// inline (requirement-spec.md §4/design-decisions.md "Report Generation Always Async"). The
/// enqueue mechanism IS the run's own <see cref="RegulatoryReportRunStatus.Pending"/> row - a
/// <c>RegulatoryReportRunRelayWorker</c> (ADR-0014) polls for pending runs exactly the way
/// Documents' own bulk-generation job polling already works in this codebase, rather than a
/// redundant parallel outbox entry pointing at the same row.
///
/// <para>RPT-16: every run request is synchronously audited (who, when, with what parameters).</para>
/// </summary>
public sealed class RegulatoryReportRunService(
    IRegulatoryReportDefinitionRepository definitions,
    IRegulatoryReportRunRepository runs,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<EnqueueRegulatoryReportRunResult>> EnqueueAsync(
        Guid definitionId,
        string parametersJson,
        RegulatoryReportFormat format,
        AuditContext audit,
        CancellationToken cancellationToken = default)
    {
        var definition = await definitions.GetByIdAsync(new RegulatoryReportDefinitionId(definitionId), cancellationToken).ConfigureAwait(false);
        if (definition is null)
        {
            return Error.NotFound("regulatory_report_definition.not_found", $"RegulatoryReportDefinition '{definitionId}' was not found.");
        }

        if (!definition.IsActive)
        {
            return Error.Conflict("regulatory_report_definition.inactive", $"RegulatoryReportDefinition '{definitionId}' is no longer active.");
        }

        if ((definition.SupportedFormats & format) != format || format == RegulatoryReportFormat.None)
        {
            return Error.Validation("regulatory_report_run.unsupported_format", $"RegulatoryReportDefinition '{definitionId}' does not support the requested format '{format}'.");
        }

        var normalizedParameters = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
        var snapshot = definition.CaptureSnapshot();
        var run = RegulatoryReportRun.Create(definition.Id, snapshot, normalizedParameters, format, audit.ActorUserId, clock.UtcNow);

        // RPT-15/edge-cases.md "Two concurrent report-generation requests for the identical
        // (definition, parameters) tuple": a non-blocking courtesy notice only - never a dedup or
        // a block (design-decisions.md "No Hard Dedup in v1").
        var inFlight = await runs.FindInFlightByParametersHashAsync(run.ParametersHash, cancellationToken).ConfigureAwait(false);

        runs.Add(run);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "RegulatoryReportRun",
            run.Id.Value.ToString(),
            "Requested",
            beforeValueJson: null,
            afterValueJson: JsonSerializer.Serialize(new { run.DefinitionId.Value, run.ParametersJson, Format = run.Format.ToString() }));

        var commit = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (commit.IsFailure)
        {
            return commit.Error!;
        }

        return new EnqueueRegulatoryReportRunResult(run.Id.Value, inFlight?.Id.Value);
    }

    public async Task<Result<RegulatoryReportRunStatusDto>> GetStatusAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await runs.GetByIdAsync(new RegulatoryReportRunId(runId), cancellationToken).ConfigureAwait(false);
        return run is null
            ? Error.NotFound("regulatory_report_run.not_found", $"RegulatoryReportRun '{runId}' was not found.")
            : ToStatusDto(run);
    }

    internal static RegulatoryReportRunStatusDto ToStatusDto(RegulatoryReportRun run) => new(
        run.Id.Value,
        run.DefinitionId.Value,
        run.Status.ToString(),
        run.Format.ToString(),
        run.RequestedAt,
        run.AsOf,
        run.CompletedAt,
        run.ResultDocumentId,
        run.ResultCsvContent,
        run.ErrorMessage);
}

/// <summary><see cref="InFlightDuplicateRunId"/> is the courtesy notice RPT-15 requires - non-null only when another still-non-terminal run shares this exact (definition, parameters) tuple; never blocks this new run from proceeding.</summary>
public sealed record EnqueueRegulatoryReportRunResult(Guid RunId, Guid? InFlightDuplicateRunId);

/// <summary><see cref="ResultCsvContent"/> is populated only for a completed CSV-format run (see <c>RegulatoryReportRun.ResultCsvContent</c>'s own remarks on this build's documented inline-storage mechanism); <see cref="ResultDocumentId"/> only for a completed PDF-format run.</summary>
public sealed record RegulatoryReportRunStatusDto(
    Guid RunId,
    Guid DefinitionId,
    string Status,
    string Format,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DataAsOf,
    DateTimeOffset? CompletedAt,
    Guid? ResultDocumentId,
    string? ResultCsvContent,
    string? ErrorMessage);
