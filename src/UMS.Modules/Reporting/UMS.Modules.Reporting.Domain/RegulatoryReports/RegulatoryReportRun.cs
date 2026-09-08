using System.Text.Json;
using UMS.Modules.Reporting.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>
/// requirement-spec.md §3: "One async execution of a RegulatoryReportDefinition against a
/// parameter set; tracks status and the resulting GeneratedDocument reference." Never produced
/// synchronously (requirement-spec.md §4 "Report generation is always async").
///
/// <para>
/// <see cref="DefinitionSnapshotJson"/> is the design-decisions.md "Snapshot-by-Reference" mechanism
/// made concrete: captured once, at <see cref="Create"/> time, from the live
/// <see cref="RegulatoryReportDefinition.CaptureSnapshot"/> - every later execution step
/// (<see cref="Start"/>, <see cref="CompleteWithGeneratedDocument"/>,
/// <see cref="CompleteWithInlineCsv"/>, <see cref="Fail"/>) reads/writes only this run's own state,
/// never the live definition row. A later edit to that definition therefore cannot alter this run,
/// in flight or after completion.
/// </para>
/// </summary>
public sealed class RegulatoryReportRun : AggregateRoot<RegulatoryReportRunId>
{
    private RegulatoryReportRun()
    {
    }

    private RegulatoryReportRun(
        RegulatoryReportRunId id,
        RegulatoryReportDefinitionId definitionId,
        string definitionSnapshotJson,
        string parametersJson,
        string parametersHash,
        RegulatoryReportFormat format,
        Guid requestedByUserId,
        DateTimeOffset now)
    {
        Id = id;
        DefinitionId = definitionId;
        DefinitionSnapshotJson = definitionSnapshotJson;
        ParametersJson = parametersJson;
        ParametersHash = parametersHash;
        Format = format;
        RequestedByUserId = requestedByUserId;
        Status = RegulatoryReportRunStatus.Pending;
        RequestedAt = now;
    }

    public RegulatoryReportDefinitionId DefinitionId { get; private set; }

    public string DefinitionSnapshotJson { get; private set; } = string.Empty;

    public string ParametersJson { get; private set; } = "{}";

    /// <summary>RPT-15/edge-cases.md "Two concurrent report-generation requests for the identical (definition, parameters) tuple": a stable hash of <see cref="DefinitionId"/> + <see cref="ParametersJson"/>, used only to detect and surface an in-flight duplicate as a courtesy notice - never to block or dedup (design-decisions.md "No Hard Dedup in v1").</summary>
    public string ParametersHash { get; private set; } = string.Empty;

    public RegulatoryReportFormat Format { get; private set; }

    public RegulatoryReportRunStatus Status { get; private set; }

    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>design-decisions.md "Snapshot-Timestamp Pinning": the single logical instant this run's own aggregation began - the same value stamped as this run's own <c>data_as_of</c> once complete.</summary>
    public DateTimeOffset? AsOf { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Set only for <see cref="RegulatoryReportFormat.Pdf"/> - the Documents-owned <c>GeneratedDocument</c> id (requirement-spec.md §7 "PDF ... generation/storage ... delegated to Documents").</summary>
    public Guid? ResultDocumentId { get; private set; }

    /// <summary>
    /// Set only for <see cref="RegulatoryReportFormat.Csv"/> - the generated CSV content, stored
    /// inline in Reporting's own schema. Documented first-pass mechanism (mechanism #5 of this
    /// build's design guidance): Documents' <c>IDocumentGenerationRequester</c> only accepts
    /// template-merge-field generation (PDF), not arbitrary raw bytes, so CSV cannot be routed
    /// through it without a Documents-side change out of scope for this flow - Reporting owns no
    /// object storage of its own, so the content is persisted directly on this row instead of a
    /// file reference to nowhere.
    /// </summary>
    public string? ResultCsvContent { get; private set; }

    public string? ErrorMessage { get; private set; }

    public bool IsTerminal => Status is RegulatoryReportRunStatus.Completed or RegulatoryReportRunStatus.Failed;

    public static RegulatoryReportRun Create(
        RegulatoryReportDefinitionId definitionId,
        RegulatoryReportDefinitionSnapshot snapshot,
        string parametersJson,
        RegulatoryReportFormat format,
        Guid requestedByUserId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var snapshotJson = JsonSerializer.Serialize(snapshot);
        var normalizedParameters = string.IsNullOrWhiteSpace(parametersJson) ? "{}" : parametersJson;
        var hash = ComputeParametersHash(definitionId, normalizedParameters);

        return new RegulatoryReportRun(
            RegulatoryReportRunId.New(),
            definitionId,
            snapshotJson,
            normalizedParameters,
            hash,
            format,
            requestedByUserId,
            now);
    }

    public static string ComputeParametersHash(RegulatoryReportDefinitionId definitionId, string parametersJson)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{definitionId.Value:N}|{parametersJson}");
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    public Result Start(DateTimeOffset asOf)
    {
        if (Status != RegulatoryReportRunStatus.Pending)
        {
            return Error.Conflict("regulatory_report_run.invalid_transition", $"Cannot start a run in status '{Status}'.");
        }

        Status = RegulatoryReportRunStatus.Running;
        AsOf = asOf;
        StartedAt = asOf;
        return Result.Success();
    }

    public Result CompleteWithGeneratedDocument(Guid generatedDocumentId, DateTimeOffset completedAt)
    {
        if (Status != RegulatoryReportRunStatus.Running)
        {
            return Error.Conflict("regulatory_report_run.invalid_transition", $"Cannot complete a run in status '{Status}'.");
        }

        Status = RegulatoryReportRunStatus.Completed;
        ResultDocumentId = generatedDocumentId;
        CompletedAt = completedAt;
        Raise(new RegulatoryReportRunCompleted(Id.Value, DefinitionId.Value, RequestedByUserId, completedAt));
        return Result.Success();
    }

    public Result CompleteWithInlineCsv(string csvContent, DateTimeOffset completedAt)
    {
        if (Status != RegulatoryReportRunStatus.Running)
        {
            return Error.Conflict("regulatory_report_run.invalid_transition", $"Cannot complete a run in status '{Status}'.");
        }

        Status = RegulatoryReportRunStatus.Completed;
        ResultCsvContent = csvContent;
        CompletedAt = completedAt;
        Raise(new RegulatoryReportRunCompleted(Id.Value, DefinitionId.Value, RequestedByUserId, completedAt));
        return Result.Success();
    }

    public void Fail(string errorMessage, DateTimeOffset failedAt)
    {
        Status = RegulatoryReportRunStatus.Failed;
        ErrorMessage = errorMessage;
        CompletedAt = failedAt;
        Raise(new RegulatoryReportRunFailed(Id.Value, DefinitionId.Value, RequestedByUserId, errorMessage, failedAt));
    }
}
