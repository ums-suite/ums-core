namespace UMS.Modules.Audit.Domain.Exports;

/// <summary>Requirement-spec.md audit §2/§6: "Exports (CSV/PDF) of a filtered audit view".</summary>
public enum ExportFormat
{
    Csv = 0,
    Pdf = 1,
}
