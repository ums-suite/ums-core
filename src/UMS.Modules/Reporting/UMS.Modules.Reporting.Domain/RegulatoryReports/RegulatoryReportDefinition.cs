using UMS.Modules.Reporting.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>
/// requirement-spec.md §2.3/§3: "a configurable export shape (fields, filters, format) so a new
/// reporting authority's format doesn't require a code change." Configures field selection
/// (<see cref="FieldSelections"/>), the accepted filter/parameter shape
/// (<see cref="FiltersJson"/> - a JSON object naming each accepted parameter and its type, e.g.
/// <c>{"academicSessionId":"guid","departmentId":"guid?","dateFrom":"date","dateTo":"date"}</c>),
/// the source metrics/queries it draws from (<see cref="SourceQueryReferencesJson"/> - a JSON array
/// of free-form strings naming which dashboard/reporting-query method a run resolves against, e.g.
/// <c>["academic.dashboard","admission.dashboard"]</c>), and output format(s)
/// (<see cref="SupportedFormats"/>).
///
/// <para>
/// <b>Invariant this aggregate exists to make structurally true (requirement-spec.md §4/§8, design-
/// decisions.md "RegulatoryReportDefinition Snapshot-by-Reference"):</b> a live edit here never
/// retroactively alters an in-flight OR completed <see cref="RegulatoryReportRun"/> - see
/// <see cref="CaptureSnapshot"/>, which every run calls exactly once, at enqueue time, to freeze its
/// own immutable copy. This aggregate itself carries no notion of "runs currently referencing me" -
/// isolation is achieved entirely by the RUN holding its own frozen copy, never by this aggregate
/// blocking or coordinating with in-flight runs (the "block edits while in-flight" alternative was
/// explicitly rejected - see design-decisions.md).
/// </para>
/// </summary>
public sealed class RegulatoryReportDefinition : AggregateRoot<RegulatoryReportDefinitionId>
{
    private readonly List<ReportFieldSelection> _fieldSelections = [];

    private RegulatoryReportDefinition()
    {
    }

    private RegulatoryReportDefinition(
        RegulatoryReportDefinitionId id,
        string name,
        RegulatoryReportCategory category,
        string filtersJson,
        string sourceQueryReferencesJson,
        RegulatoryReportFormat supportedFormats,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        Id = id;
        Name = name;
        Category = category;
        FiltersJson = filtersJson;
        SourceQueryReferencesJson = sourceQueryReferencesJson;
        SupportedFormats = supportedFormats;
        CreatedByUserId = createdByUserId;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Name { get; private set; } = string.Empty;

    public RegulatoryReportCategory Category { get; private set; }

    public IReadOnlyList<ReportFieldSelection> FieldSelections => _fieldSelections.AsReadOnly();

    public string FiltersJson { get; private set; } = "{}";

    public string SourceQueryReferencesJson { get; private set; } = "[]";

    public RegulatoryReportFormat SupportedFormats { get; private set; }

    public bool IsActive { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Result<RegulatoryReportDefinition> Create(
        string name,
        RegulatoryReportCategory category,
        IReadOnlyList<(string FieldKey, string Label)> fields,
        string filtersJson,
        string sourceQueryReferencesJson,
        RegulatoryReportFormat supportedFormats,
        Guid createdByUserId,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("regulatory_report_definition.name_required", "A RegulatoryReportDefinition name is required.");
        }

        if (fields.Count == 0)
        {
            return Error.Validation("regulatory_report_definition.fields_required", "At least one field selection is required.");
        }

        if (supportedFormats == RegulatoryReportFormat.None)
        {
            return Error.Validation("regulatory_report_definition.format_required", "At least one output format must be supported.");
        }

        var definition = new RegulatoryReportDefinition(
            RegulatoryReportDefinitionId.New(),
            name.Trim(),
            category,
            string.IsNullOrWhiteSpace(filtersJson) ? "{}" : filtersJson,
            string.IsNullOrWhiteSpace(sourceQueryReferencesJson) ? "[]" : sourceQueryReferencesJson,
            supportedFormats,
            createdByUserId,
            now);

        definition.ReplaceFieldSelections(fields);
        return definition;
    }

    /// <summary>requirement-spec.md §2.3: "Admin can create/clone/edit a RegulatoryReportDefinition through an API" - affects future runs only (see class remarks).</summary>
    public Result UpdateConfiguration(
        string name,
        IReadOnlyList<(string FieldKey, string Label)> fields,
        string filtersJson,
        string sourceQueryReferencesJson,
        RegulatoryReportFormat supportedFormats,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("regulatory_report_definition.name_required", "A RegulatoryReportDefinition name is required.");
        }

        if (fields.Count == 0)
        {
            return Error.Validation("regulatory_report_definition.fields_required", "At least one field selection is required.");
        }

        if (supportedFormats == RegulatoryReportFormat.None)
        {
            return Error.Validation("regulatory_report_definition.format_required", "At least one output format must be supported.");
        }

        Name = name.Trim();
        FiltersJson = string.IsNullOrWhiteSpace(filtersJson) ? "{}" : filtersJson;
        SourceQueryReferencesJson = string.IsNullOrWhiteSpace(sourceQueryReferencesJson) ? "[]" : sourceQueryReferencesJson;
        SupportedFormats = supportedFormats;
        UpdatedAt = now;
        ReplaceFieldSelections(fields);
        return Result.Success();
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    /// <summary>design-decisions.md "RegulatoryReportDefinition Snapshot-by-Reference": called exactly once, by the application service enqueueing a new <see cref="RegulatoryReportRun"/>, to freeze this definition's exact current shape into that run's own <c>DefinitionSnapshotJson</c>.</summary>
    public RegulatoryReportDefinitionSnapshot CaptureSnapshot() =>
        new(
            Id.Value,
            Name,
            Category,
            _fieldSelections.OrderBy(f => f.Ordinal).Select(f => new ReportFieldSelectionSnapshot(f.FieldKey, f.Label, f.Ordinal)).ToList(),
            FiltersJson,
            SourceQueryReferencesJson,
            SupportedFormats);

    private void ReplaceFieldSelections(IReadOnlyList<(string FieldKey, string Label)> fields)
    {
        _fieldSelections.Clear();
        for (var i = 0; i < fields.Count; i++)
        {
            _fieldSelections.Add(new ReportFieldSelection(fields[i].FieldKey, fields[i].Label, i));
        }
    }
}

/// <summary>The immutable, JSON-serializable copy of a <see cref="RegulatoryReportDefinition"/>'s configuration a <see cref="RegulatoryReportRun"/> freezes at enqueue time.</summary>
public sealed record RegulatoryReportDefinitionSnapshot(
    Guid DefinitionId,
    string Name,
    RegulatoryReportCategory Category,
    IReadOnlyList<ReportFieldSelectionSnapshot> FieldSelections,
    string FiltersJson,
    string SourceQueryReferencesJson,
    RegulatoryReportFormat SupportedFormats);

public sealed record ReportFieldSelectionSnapshot(string FieldKey, string Label, int Ordinal);
