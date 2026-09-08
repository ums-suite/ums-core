namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>
/// requirement-spec.md §2.3(a): "field selection - which computed/derived fields appear and in
/// what order/label." An owned child of <see cref="RegulatoryReportDefinition"/>, never its own
/// aggregate root. <see cref="Ordinal"/> is the field's display order - assigned positionally by
/// <see cref="RegulatoryReportDefinition"/> itself (0, 1, 2, ...) at construction/update time, NOT
/// a caller-supplied business identity, precisely to sidestep the known EF Core 10.0.11 owned-
/// collection bug (misfires UPDATE instead of INSERT for a brand-new owned-collection child row
/// keyed on a caller-supplied business value on an already-tracked/persisted parent) - see
/// <c>Infrastructure.Persistence.Configurations.RegulatoryReportDefinitionConfiguration</c>'s own
/// remarks for the shadow-key mapping this ordinal-assignment discipline is paired with.
/// </summary>
public sealed class ReportFieldSelection
{
    internal ReportFieldSelection(string fieldKey, string label, int ordinal)
    {
        FieldKey = fieldKey;
        Label = label;
        Ordinal = ordinal;
    }

    private ReportFieldSelection()
    {
    }

    public string FieldKey { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public int Ordinal { get; private set; }
}
