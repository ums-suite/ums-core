namespace UMS.Modules.Student.Domain.BulkImport;

/// <summary>requirement-spec.md student §2 Bulk Import: "Upload -&gt; Validate -&gt; Preview errors -&gt; Approve -&gt; Process -&gt; Generate report" - each named step is one of these statuses (Validate/Preview-errors both resolve into <see cref="Validated"/>, which the STU-16 report endpoint exposes for review before <see cref="Approved"/>).</summary>
public enum StudentBulkImportJobStatus
{
    Uploaded = 0,
    Validated = 1,
    Approved = 2,
    Processing = 3,
    Completed = 4,
    CompletedWithErrors = 5,
}
