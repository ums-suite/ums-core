namespace UMS.Modules.Student.Domain.BulkImport;

/// <summary>
/// design-decisions.md "Bulk-Import Concurrency &amp; Field-Scoping Design"'s per-row partial-commit
/// mechanism, applied at the row level: <see cref="Invalid"/> rows never reach <see cref="Processing"/>
/// at all (surfaced in the job's error report at the Validate/Preview-errors step, §9 decision 4);
/// <see cref="Valid"/> rows are the ones <c>StudentBulkImportRelayWorker</c> actually processes, one
/// at a time, each independently landing on <see cref="Succeeded"/> or <see cref="Failed"/> without
/// affecting any other row - the same per-item independence Documents' own
/// <c>BulkGenerationJobItemStatus</c> established for its own bulk job.
/// </summary>
public enum StudentBulkImportRowStatus
{
    Valid = 0,
    Invalid = 1,
    Processing = 2,
    Succeeded = 3,
    Failed = 4,
}
