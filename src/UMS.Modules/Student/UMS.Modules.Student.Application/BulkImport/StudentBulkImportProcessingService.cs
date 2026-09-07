using System.Text.Json;
using Microsoft.Extensions.Logging;
using UMS.Modules.Student.Application.Abstractions;
using UMS.Modules.Student.Application.Common;
using UMS.Modules.Student.Application.Students;
using UMS.Modules.Student.Domain.BulkImport;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Student.Application.BulkImport;

/// <summary>
/// STU-15: the async half of the bulk-import job - drives one <see cref="Domain.BulkImport.StudentBulkImportJob"/>
/// forward one bounded batch per call (<c>StudentBulkImportRelayWorker</c> calls this repeatedly
/// until the job reaches a terminal status), mirroring Admission's own
/// <c>PublishJobService.ProcessNextBatchAsync</c>/Documents' <c>BulkGenerationRelayWorker</c> shape
/// exactly.
///
/// <para>
/// <b>Row processing reuses the exact single-row services, never duplicates their logic.</b> A
/// CREATE row (no <c>StudentNumber</c>) is processed through the SAME
/// <see cref="CreateStudentRecordService.CreateAsync"/> STU-1 already uses - identical idempotency
/// (retried/duplicated <c>OriginatingApplicationId</c> resolves to the existing Student, never a
/// second one) and StudentNumber-generation-collision handling, for free. An UPDATE row (
/// <c>StudentNumber</c> present) applies the SAME field-scoped
/// <c>Student.UpdateSelfServiceProfile</c> STU-6's own self-service endpoint uses, with an
/// additional explicit optimistic-concurrency check against the row's own captured
/// <c>ExpectedVersion</c> (design-decisions.md "Bulk-Import Concurrency &amp; Field-Scoping Design" -
/// edge-cases.md's "bulk-import row upsert races a student's own concurrent self-service profile
/// edit").
/// </para>
///
/// <para>
/// <b>Per-row commit, immediately after each row resolves</b> - never batched at the end of a
/// chunk - so a mid-batch crash/restart never re-processes an already-resolved row (mirroring
/// Documents' own <c>BulkGenerationRelayWorker.ProcessOneItemAsync</c>'s per-item
/// <c>SaveChangesAsync</c> discipline). <b>Known, documented gap:</b> if the worker crashes AFTER an
/// UPDATE row's own Student write commits but BEFORE this row's own bookkeeping
/// (<see cref="StudentBulkImportRow.MarkSucceeded"/>/<see cref="StudentBulkImportJob.RecordRowOutcome"/>)
/// is saved, the row is re-attempted on resume against its now-stale captured
/// <c>ExpectedVersion</c> and surfaces as a false-negative version conflict (Failed) even though the
/// underlying update genuinely already happened - a narrower, single-row instance of the same class
/// of at-least-once-retry risk `CreateStudentRecordService`'s own remarks already document
/// platform-wide for cross-module command calls; not eliminated here since doing so would require a
/// two-phase-commit-shaped write this platform's ADR-0001 posture deliberately avoids.
/// </para>
/// </summary>
public sealed class StudentBulkImportProcessingService(
    IStudentBulkImportJobRepository jobs,
    IStudentBulkImportRowRepository rows,
    IStudentRepository students,
    CreateStudentRecordService createStudentRecordService,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<StudentBulkImportProcessingService> logger)
{
    public async Task<Result> ProcessNextBatchAsync(Guid jobId, int batchSize, CancellationToken cancellationToken = default)
    {
        var job = await jobs.GetByIdAsync(new StudentBulkImportJobId(jobId), cancellationToken).ConfigureAwait(false);
        if (job is null)
        {
            return Result.Failure(Error.NotFound("student_bulk_import_job.not_found", $"No bulk-import job exists with id '{jobId}'."));
        }

        if (job.Status is Domain.BulkImport.StudentBulkImportJobStatus.Completed or Domain.BulkImport.StudentBulkImportJobStatus.CompletedWithErrors)
        {
            return Result.Success();
        }

        var markProcessing = job.MarkProcessing();
        if (markProcessing.IsFailure)
        {
            return markProcessing;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var batch = await rows.GetUnresolvedBatchAsync(job.Id, batchSize, cancellationToken).ConfigureAwait(false);
        if (batch.Count == 0)
        {
            var complete = job.Complete(clock.UtcNow);
            if (complete.IsFailure)
            {
                return complete;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }

        foreach (var row in batch)
        {
            await ProcessRowAsync(job, row, jobId, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task ProcessRowAsync(Domain.BulkImport.StudentBulkImportJob job, Domain.BulkImport.StudentBulkImportRow row, Guid jobId, CancellationToken cancellationToken)
    {
        row.MarkProcessing();

        try
        {
            var input = JsonSerializer.Deserialize<StudentBulkImportRowInput>(row.PayloadJson)
                ?? throw new InvalidOperationException("Empty bulk-import row payload.");

            var outcome = string.IsNullOrWhiteSpace(input.StudentNumber)
                ? await ProcessCreateRowAsync(jobId, input, cancellationToken).ConfigureAwait(false)
                : await ProcessUpdateRowAsync(input, cancellationToken).ConfigureAwait(false);

            if (outcome.IsSuccess)
            {
                row.MarkSucceeded(outcome.Value);
                job.RecordRowOutcome(succeeded: true);
            }
            else
            {
                row.MarkFailed(outcome.Error!.Message);
                job.RecordRowOutcome(succeeded: false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            row.MarkFailed($"Unexpected error: {ex.Message}");
            job.RecordRowOutcome(succeeded: false);
            logger.LogWarning(ex, "StudentBulkImportJob {JobId} row {RowNumber} failed unexpectedly.", job.Id.Value, row.RowNumber);
        }
    }

    private async Task<Result<Guid>> ProcessCreateRowAsync(Guid jobId, StudentBulkImportRowInput input, CancellationToken cancellationToken)
    {
        var command = new CreateStudentRecordRequest(
            input.OriginatingApplicationId!.Value,
            input.AdmissionYear!.Value,
            input.FacultyCode!,
            input.DepartmentId!.Value,
            input.ProgramId!.Value,
            input.GivenName!,
            input.FamilyName!,
            input.GivenNameBn,
            input.FamilyNameBn,
            input.Email!,
            input.Mobile,
            input.DateOfBirth!.Value,
            input.NationalId);

        var audit = AuditContext.ForSystem($"bulk-import:{jobId}");
        var result = await createStudentRecordService.CreateAsync(command, audit, cancellationToken).ConfigureAwait(false);
        return result.Match<Result<Guid>>(dto => dto.Id, error => error);
    }

    private async Task<Result<Guid>> ProcessUpdateRowAsync(StudentBulkImportRowInput input, CancellationToken cancellationToken)
    {
        var student = await students.GetByStudentNumberAsync(input.StudentNumber!, cancellationToken).ConfigureAwait(false);
        if (student is null)
        {
            return Error.NotFound("student_bulk_import_row.student_not_found", $"No Student exists with StudentNumber '{input.StudentNumber}'.");
        }

        // Field-scoped, column-level conditional write ONLY (never a full-row overwrite) - the
        // exact same self-service subset STU-6's own PUT /students/me touches, so a bulk-import
        // update row can never clobber an identity-bearing field a self-service edit could not
        // have touched either way (design-decisions.md).
        student.UpdateSelfServiceProfile(input.ContactEmail, input.ContactPhone, input.PhotoUrl);
        unitOfWork.SetExpectedVersion(student, input.ExpectedVersion!.Value);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            // Must run before this shared scope's NEXT SaveChangesAsync (the row/job bookkeeping
            // save in ProcessNextBatchAsync's loop) - see IUnitOfWork.DiscardChanges's own remarks.
            unitOfWork.DiscardChanges(student);
            return Error.Conflict("student_bulk_import_row.version_conflict", $"{ex.Message} A concurrent self-service edit (or another import row) already changed this Student - re-export its current version and re-submit this row.");
        }

        return student.Id.Value;
    }
}
