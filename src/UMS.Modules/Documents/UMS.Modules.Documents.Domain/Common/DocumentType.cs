namespace UMS.Modules.Documents.Domain.Common;

/// <summary>
/// The document types in scope for v1 (requirement-spec.md documents §1's table), each with its
/// own triggering module and sync/async default enforced at the Application layer, not here -
/// this enum only names the type, it does not encode which path a given request must take.
/// </summary>
public enum DocumentType
{
    AdmitCard = 0,
    MeritList = 1,
    Transcript = 2,
    Certificate = 3,
    IdCard = 4,
    Receipt = 5,

    /// <summary>
    /// reporting requirement-spec.md §2.3(d)/§7: RPT-13's PDF-format regulatory-report output,
    /// requested via <c>IDocumentGenerationRequester</c> exactly like every other module's own
    /// template-driven document (mechanism #5(a) of Reporting's own build guidance). CSV-format
    /// regulatory reports do NOT go through Documents at all - see
    /// <c>UMS.Modules.Reporting.Domain.RegulatoryReports.RegulatoryReportRun.ResultCsvContent</c>'s
    /// own remarks for that documented, separate mechanism.
    /// </summary>
    RegulatoryReport = 6,
}
