namespace UMS.Modules.Reporting.Domain.RegulatoryReports;

/// <summary>
/// requirement-spec.md §2.3(d): "output format(s) - PDF, Excel, CSV, per report definition (a
/// definition may support multiple formats; the requester picks one per run)." Flags-shaped so
/// <see cref="RegulatoryReportDefinition.SupportedFormats"/> can combine multiple values; a
/// <see cref="RegulatoryReportRun"/> always carries exactly one.
///
/// <para>
/// <b>Documented gap:</b> <see cref="Excel"/> is a supported VALUE (a definition may declare it,
/// and a run may request it) but has no real generation path in this build - no spreadsheet-writing
/// library exists anywhere in this repo's dependencies, and adding one is a genuinely new
/// dependency decision out of proportion for this "base" flow. A run requesting <see cref="Excel"/>
/// deterministically fails with a clear, named error (see <c>RegulatoryReportRunExecutionService</c>)
/// rather than silently producing a wrong or empty file - a documented, deferred capability, not a
/// silently broken one.
/// </para>
/// </summary>
[Flags]
public enum RegulatoryReportFormat
{
    None = 0,
    Pdf = 1,
    Csv = 2,
    Excel = 4,
}
