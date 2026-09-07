namespace UMS.Modules.Student.Domain.BulkImport;

/// <summary>
/// design-decisions.md "Bulk-Import Concurrency &amp; Field-Scoping Design": one row per input record,
/// independently validated/processed/reported - the resume mechanism (<c>StudentBulkImportRelayWorker</c>)
/// queries this table directly for not-yet-resolved <see cref="StudentBulkImportRowStatus.Valid"/>
/// rows, mirroring Documents' own <c>BulkGenerationJobItem</c> exactly.
/// </summary>
public sealed class StudentBulkImportRow
{
    private StudentBulkImportRow()
    {
    }

    public StudentBulkImportRowId Id { get; private init; }

    public StudentBulkImportJobId JobId { get; private init; }

    public int RowNumber { get; private init; }

    /// <summary>The raw input row, serialized (<c>StudentBulkImportRowInput</c>) - re-parsed by the worker at process time so a row's own data never needs a second round trip from the caller.</summary>
    public string PayloadJson { get; private init; } = "{}";

    public StudentBulkImportRowStatus Status { get; private set; }

    /// <summary>Validation failure (Invalid rows) or processing failure (Failed rows) - never set for a Succeeded row.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>The Student created or updated by this row, once resolved - <see langword="null"/> until <see cref="MarkSucceeded"/>.</summary>
    public Guid? ResultStudentId { get; private set; }

    public static StudentBulkImportRow CreateValid(StudentBulkImportJobId jobId, int rowNumber, string payloadJson) => new()
    {
        Id = StudentBulkImportRowId.New(),
        JobId = jobId,
        RowNumber = rowNumber,
        PayloadJson = payloadJson,
        Status = StudentBulkImportRowStatus.Valid,
    };

    public static StudentBulkImportRow CreateInvalid(StudentBulkImportJobId jobId, int rowNumber, string payloadJson, string errorMessage) => new()
    {
        Id = StudentBulkImportRowId.New(),
        JobId = jobId,
        RowNumber = rowNumber,
        PayloadJson = payloadJson,
        Status = StudentBulkImportRowStatus.Invalid,
        ErrorMessage = errorMessage,
    };

    public void MarkProcessing() => Status = StudentBulkImportRowStatus.Processing;

    public void MarkSucceeded(Guid studentId)
    {
        Status = StudentBulkImportRowStatus.Succeeded;
        ResultStudentId = studentId;
        ErrorMessage = null;
    }

    /// <summary>No retry loop (unlike Documents' 3-attempt <c>BulkGenerationJobItem.RecordFailure</c>) - a bulk-import row's failure is almost always a genuine, non-transient data problem (bad FK reference, concurrent version conflict on an update row) that a same-input retry would not resolve; the row is surfaced in the job's error report for the admin to correct and re-submit as a NEW row/job instead (requirement-spec.md §9 decision 4's own "for correction and re-submission" framing).</summary>
    public void MarkFailed(string errorMessage)
    {
        Status = StudentBulkImportRowStatus.Failed;
        ErrorMessage = errorMessage;
    }
}
