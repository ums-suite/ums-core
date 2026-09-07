using System.Text.Json;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Domain.BulkImport;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.BulkImport;

/// <summary>
/// STU-15/STU-16: the synchronous half of the bulk-import job (requirement-spec.md student §2 Bulk
/// Import's own "Upload -&gt; Validate -&gt; Preview errors -&gt; Approve" steps) plus the STU-16 report
/// query. The remaining "Process -&gt; Generate report" steps are the ASYNC half
/// (<see cref="StudentBulkImportProcessingService"/>, driven by <c>StudentBulkImportRelayWorker</c>) -
/// requirement-spec.md §5's own async-import NFR is about the POTENTIALLY LARGE, per-row work
/// (creating/updating many Students with their own side effects) never blocking interactive
/// traffic, not about banning all synchronous work outright; Upload/Validate here is a fixed,
/// cheap, structural-only pass (no cross-module existence checks, no Student writes at all) over
/// the input rows, deliberately kept off the worker so STU-16's "Preview errors" step is available
/// to the caller immediately, without a poll loop.
/// </summary>
public sealed class StudentBulkImportService(
    IStudentBulkImportJobRepository jobs,
    IStudentBulkImportRowRepository rows,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<StudentBulkImportJobDto>> UploadAsync(Guid requestedByUserId, UploadStudentBulkImportRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Rows.Count == 0)
        {
            return Error.Validation("student_bulk_import.rows_required", "At least one row is required.");
        }

        var jobResult = StudentBulkImportJob.Create(requestedByUserId, request.Rows.Count, clock.UtcNow);
        if (jobResult.IsFailure)
        {
            return jobResult.Error!;
        }

        var job = jobResult.Value;
        jobs.Add(job);

        var rowEntities = new List<Domain.BulkImport.StudentBulkImportRow>(request.Rows.Count);
        var validCount = 0;
        var invalidCount = 0;
        var rowNumber = 0;

        foreach (var rowInput in request.Rows)
        {
            rowNumber++;
            var payloadJson = JsonSerializer.Serialize(rowInput);
            var validationError = ValidateRow(rowInput);

            rowEntities.Add(validationError is null
                ? Domain.BulkImport.StudentBulkImportRow.CreateValid(job.Id, rowNumber, payloadJson)
                : Domain.BulkImport.StudentBulkImportRow.CreateInvalid(job.Id, rowNumber, payloadJson, validationError));

            if (validationError is null)
            {
                validCount++;
            }
            else
            {
                invalidCount++;
            }
        }

        job.RecordValidation(validCount, invalidCount);
        rows.AddRange(rowEntities);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(job);
    }

    /// <summary>The STU-15 Approve gate - a deliberate, documented addition beyond requirement-spec.md §6's literal 2-endpoint table (mirroring Faculty's/Learning's own documented endpoint-table deviations): §2's own stated flow explicitly names "Approve" as a step between Preview-errors and Process, which a bare Upload+Poll shape would have no way to express.</summary>
    public async Task<Result<StudentBulkImportJobDto>> ApproveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(new StudentBulkImportJobId(jobId), cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Error.NotFound("student_bulk_import_job.not_found", $"No bulk-import job exists with id '{jobId}'.");
        }

        var approveResult = job.Approve(clock.UtcNow);
        if (approveResult.IsFailure)
        {
            return approveResult.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(job);
    }

    /// <summary>STU-16: job status plus the full per-row report - also what a caller polls after Approve to watch Process complete.</summary>
    public async Task<Result<StudentBulkImportJobReportDto>> GetReportAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(new StudentBulkImportJobId(jobId), cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Error.NotFound("student_bulk_import_job.not_found", $"No bulk-import job exists with id '{jobId}'.");
        }

        var jobRows = await rows.ListByJobAsync(new StudentBulkImportJobId(jobId), cancellationToken).ConfigureAwait(false);
        var rowDtos = jobRows.Select(r => new StudentBulkImportRowReportDto(r.RowNumber, r.Status.ToString(), r.ErrorMessage, r.ResultStudentId)).ToList();

        return new StudentBulkImportJobReportDto(ToDto(job), rowDtos);
    }

    internal static StudentBulkImportJobDto ToDto(StudentBulkImportJob job) => new(
        job.Id.Value,
        job.RequestedByUserId,
        job.Status.ToString(),
        job.TotalRows,
        job.ValidRowCount,
        job.InvalidRowCount,
        job.ProcessedCount,
        job.SucceededCount,
        job.FailedCount,
        job.CreatedAt,
        job.ApprovedAt,
        job.CompletedAt);

    /// <summary>Structural validation only (field presence/shape) - a row's Department/Program existence and a genuine version conflict on an update row are cross-module/concurrency concerns only detectable at Process time (<see cref="StudentBulkImportProcessingService"/>), never here.</summary>
    private static string? ValidateRow(StudentBulkImportRowInput row)
    {
        if (!string.IsNullOrWhiteSpace(row.StudentNumber))
        {
            return row.ExpectedVersion is null
                ? "An update row (StudentNumber present) requires ExpectedVersion for optimistic concurrency."
                : null;
        }

        var missing = new List<string>();
        if (row.OriginatingApplicationId is null || row.OriginatingApplicationId == Guid.Empty)
        {
            missing.Add(nameof(row.OriginatingApplicationId));
        }

        if (row.AdmissionYear is null)
        {
            missing.Add(nameof(row.AdmissionYear));
        }

        if (string.IsNullOrWhiteSpace(row.FacultyCode))
        {
            missing.Add(nameof(row.FacultyCode));
        }

        if (row.DepartmentId is null || row.DepartmentId == Guid.Empty)
        {
            missing.Add(nameof(row.DepartmentId));
        }

        if (row.ProgramId is null || row.ProgramId == Guid.Empty)
        {
            missing.Add(nameof(row.ProgramId));
        }

        if (string.IsNullOrWhiteSpace(row.GivenName))
        {
            missing.Add(nameof(row.GivenName));
        }

        if (string.IsNullOrWhiteSpace(row.FamilyName))
        {
            missing.Add(nameof(row.FamilyName));
        }

        if (string.IsNullOrWhiteSpace(row.Email))
        {
            missing.Add(nameof(row.Email));
        }

        if (row.DateOfBirth is null)
        {
            missing.Add(nameof(row.DateOfBirth));
        }

        return missing.Count == 0 ? null : $"Missing required field(s): {string.Join(", ", missing)}.";
    }
}
