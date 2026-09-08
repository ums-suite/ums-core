using UMS.Modules.Alumni.Application.Abstractions;
using UMS.Modules.Alumni.Application.Common;
using UMS.Modules.Alumni.Domain.Jobs;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Alumni.Application.Jobs;

/// <summary>
/// ALM-5: JobPosting lifecycle (requirement-spec.md §2.3). design-decisions.md "Job-Posting
/// Moderation-Queue Concurrency Control": every write here (moderation OR a poster's own edit)
/// carries an expected version, mirroring Content's own Notice/Banner concurrency handling exactly -
/// a stale-version write is rejected as a conflict, never silently applied.
/// </summary>
public sealed class JobPostingService(
    IJobPostingRepository postings,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static JobPostingDto ToDto(JobPosting posting) => new(
        posting.Id.Value,
        posting.PosterUserId,
        posting.PosterIsAlumnus,
        posting.PosterAlumnusId,
        posting.Title,
        posting.Company,
        posting.Description,
        posting.Location,
        posting.ContactMethod,
        posting.ExpiresAt,
        posting.Status.ToString(),
        posting.ModerationReason,
        posting.CreatedAt,
        posting.PublishedAt,
        posting.Version);

    /// <summary>requirement-spec.md §9: alumnus-posted jobs auto-publish; non-alumnus employer postings are pre-moderated.</summary>
    public async Task<Result<JobPostingDto>> PostAsync(Guid posterUserId, bool posterIsAlumnus, Guid? posterAlumnusId, PostJobRequest request, CancellationToken cancellationToken = default)
    {
        JobPosting posting;
        try
        {
            posting = JobPosting.Post(posterUserId, posterIsAlumnus, posterAlumnusId, request.Title, request.Company, request.Description, request.Location, request.ContactMethod, request.ExpiresAt, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("jobposting.invalid", ex.Message);
        }

        postings.Add(posting);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(posting);
    }

    /// <summary>The poster's own edit - ownership is enforced by the calling Api layer.</summary>
    public async Task<Result<JobPostingDto>> EditAsync(Guid id, EditJobPostingRequest request, CancellationToken cancellationToken = default)
    {
        var posting = await postings.GetByIdAsync(new JobPostingId(id), cancellationToken).ConfigureAwait(false);
        if (posting is null)
        {
            return Error.NotFound("jobposting.not_found", $"No JobPosting exists with id '{id}'.");
        }

        try
        {
            posting.Edit(request.Title, request.Company, request.Description, request.Location, request.ContactMethod, request.ExpiresAt, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("jobposting.invalid", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("jobposting.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(posting, request.Version);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("jobposting.concurrency_conflict", ex.Message);
        }

        return ToDto(posting);
    }

    public async Task<Result<JobPostingDto>> WithdrawAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var posting = await postings.GetByIdAsync(new JobPostingId(id), cancellationToken).ConfigureAwait(false);
        if (posting is null)
        {
            return Error.NotFound("jobposting.not_found", $"No JobPosting exists with id '{id}'.");
        }

        try
        {
            posting.Withdraw();
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("jobposting.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(posting, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("jobposting.concurrency_conflict", ex.Message);
        }

        return ToDto(posting);
    }

    /// <summary>ALM-5/ALM-16: Admin approve/reject a PendingModeration posting - audited (requirement-spec.md §5).</summary>
    public async Task<Result<JobPostingDto>> ModerateAsync(Guid id, ModerateJobPostingRequest request, Common.AuditContext audit, CancellationToken cancellationToken = default)
    {
        var posting = await postings.GetByIdAsync(new JobPostingId(id), cancellationToken).ConfigureAwait(false);
        if (posting is null)
        {
            return Error.NotFound("jobposting.not_found", $"No JobPosting exists with id '{id}'.");
        }

        var beforeStatus = posting.Status.ToString();

        try
        {
            if (request.Approve)
            {
                posting.Approve(clock.UtcNow);
            }
            else
            {
                posting.Reject(request.Reason ?? "rejected");
            }
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("jobposting.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(posting, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "JobPosting",
            posting.Id.Value.ToString(),
            request.Approve ? AuditActions.Approve : AuditActions.Reject,
            $"{{\"status\":\"{beforeStatus}\"}}",
            $"{{\"status\":\"{posting.Status}\"}}",
            reason: request.Reason);

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(posting);
    }

    /// <summary>requirement-spec.md §2.3 last bullet / edge-cases.md: Admin can remove any posting at any time, even after publication - audited.</summary>
    public async Task<Result<JobPostingDto>> RemoveAsync(Guid id, RemoveJobPostingRequest request, Common.AuditContext audit, CancellationToken cancellationToken = default)
    {
        var posting = await postings.GetByIdAsync(new JobPostingId(id), cancellationToken).ConfigureAwait(false);
        if (posting is null)
        {
            return Error.NotFound("jobposting.not_found", $"No JobPosting exists with id '{id}'.");
        }

        var beforeStatus = posting.Status.ToString();
        posting.Remove(request.Reason);
        unitOfWork.SetExpectedVersion(posting, request.Version);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "JobPosting",
            posting.Id.Value.ToString(),
            AuditActions.Delete,
            $"{{\"status\":\"{beforeStatus}\"}}",
            $"{{\"status\":\"{posting.Status}\"}}",
            reason: request.Reason);

        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(posting);
    }

    public async Task<Result<JobPostingDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var posting = await postings.GetByIdAsync(new JobPostingId(id), cancellationToken).ConfigureAwait(false);
        return posting is null ? Error.NotFound("jobposting.not_found", $"No JobPosting exists with id '{id}'.") : ToDto(posting);
    }

    public async Task<IReadOnlyList<JobPostingDto>> ListAsync(string? status, Guid? posterUserId, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        JobPostingStatus? parsedStatus = Enum.TryParse<JobPostingStatus>(status, ignoreCase: true, out var s) ? s : JobPostingStatus.Published;
        if (string.IsNullOrWhiteSpace(status))
        {
            parsedStatus = JobPostingStatus.Published;
        }

        var items = await postings.ListAsync(parsedStatus, posterUserId, skip, take, cancellationToken).ConfigureAwait(false);
        return items.Select(ToDto).ToList();
    }
}
