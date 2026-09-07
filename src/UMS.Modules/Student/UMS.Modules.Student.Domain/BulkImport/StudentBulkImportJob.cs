using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Domain.BulkImport;

/// <summary>
/// STU-15/STU-16: tracks one async bulk-import batch (requirement-spec.md student §2 Bulk Import,
/// ADR-0014). Mirrors Documents' own <c>BulkGenerationJob</c> shape exactly, including deliberately
/// NOT holding its <see cref="StudentBulkImportRow"/> children as an in-memory navigation collection
/// - rows are their own EF-mapped entity with their own repository, keyed by <see cref="Id"/> as a
/// foreign key, so a large cohort file never forces the whole batch into memory for a single status
/// read (STU-16).
/// </summary>
public sealed class StudentBulkImportJob
{
    private StudentBulkImportJob()
    {
    }

    public StudentBulkImportJobId Id { get; private init; }

    public Guid RequestedByUserId { get; private init; }

    public StudentBulkImportJobStatus Status { get; private set; }

    public int TotalRows { get; private init; }

    public int ValidRowCount { get; private set; }

    public int InvalidRowCount { get; private set; }

    /// <summary>Rows resolved (Succeeded or Failed) out of <see cref="ValidRowCount"/> - never counts <see cref="StudentBulkImportRowStatus.Invalid"/> rows, which never reach processing at all.</summary>
    public int ProcessedCount { get; private set; }

    public int SucceededCount { get; private set; }

    public int FailedCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public static Result<StudentBulkImportJob> Create(Guid requestedByUserId, int totalRows, DateTimeOffset now)
    {
        if (totalRows < 1)
        {
            return Error.Validation("student_bulk_import_job.rows_required", "A bulk import job requires at least one row.");
        }

        return new StudentBulkImportJob
        {
            Id = StudentBulkImportJobId.New(),
            RequestedByUserId = requestedByUserId,
            Status = StudentBulkImportJobStatus.Uploaded,
            TotalRows = totalRows,
            CreatedAt = now,
        };
    }

    /// <summary>The Validate/Preview-errors steps resolve synchronously at upload time (structural, single-file validation - no cross-module existence checks yet, those run per-row at Process time) - called once, immediately after every row has been classified.</summary>
    public void RecordValidation(int validCount, int invalidCount)
    {
        ValidRowCount = validCount;
        InvalidRowCount = invalidCount;
        Status = StudentBulkImportJobStatus.Validated;
    }

    /// <summary>STU-15's own Approve gate - an admin confirms proceeding (typically after reviewing the Preview-errors report) before any row is actually processed.</summary>
    public Result Approve(DateTimeOffset now)
    {
        if (Status != StudentBulkImportJobStatus.Validated)
        {
            return Result.Failure(Error.Conflict("student_bulk_import_job.not_awaiting_approval", $"Cannot approve a job in status '{Status}' - it must be Validated first."));
        }

        if (ValidRowCount == 0)
        {
            return Result.Failure(Error.Validation("student_bulk_import_job.no_valid_rows", "Cannot approve a job with no valid rows to process."));
        }

        Status = StudentBulkImportJobStatus.Approved;
        ApprovedAt = now;
        return Result.Success();
    }

    public Result MarkProcessing()
    {
        if (Status is StudentBulkImportJobStatus.Completed or StudentBulkImportJobStatus.CompletedWithErrors)
        {
            return Result.Failure(Error.Conflict("student_bulk_import_job.already_terminal", $"Cannot resume processing a job already in terminal status '{Status}'."));
        }

        Status = StudentBulkImportJobStatus.Processing;
        return Result.Success();
    }

    /// <summary>Advances the running counters as the worker resolves rows - called once per row, so a resumed job's counters always reflect its own row-tracking table exactly (the same resumability discipline Documents' <c>BulkGenerationJob.RecordItemOutcome</c> established).</summary>
    public void RecordRowOutcome(bool succeeded)
    {
        ProcessedCount++;
        if (succeeded)
        {
            SucceededCount++;
        }
        else
        {
            FailedCount++;
        }
    }

    /// <summary>Called once every valid row has resolved (Succeeded or Failed) - never before.</summary>
    public Result Complete(DateTimeOffset now)
    {
        if (ProcessedCount < ValidRowCount)
        {
            return Result.Failure(Error.Conflict("student_bulk_import_job.incomplete", $"Cannot complete a job with {ProcessedCount}/{ValidRowCount} valid rows resolved."));
        }

        Status = FailedCount > 0 ? StudentBulkImportJobStatus.CompletedWithErrors : StudentBulkImportJobStatus.Completed;
        CompletedAt = now;
        return Result.Success();
    }
}
