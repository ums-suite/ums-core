using Microsoft.EntityFrameworkCore;
using UMS.Modules.Admission.Application.Abstractions;
using UMS.Modules.Admission.Domain.ExamAttempts;

namespace UMS.Modules.Admission.Infrastructure.Persistence.Repositories;

/// <summary>design-decisions.md "ExamAttempt Locking &amp; Single-Submission Mechanism": <see cref="TryLockAsync"/> is the one converging state-guarded conditional write for manual submit, retried submit, and the timeout sweep alike. <see cref="GetByIdAsync"/>'s own reload-after-bypass mechanism mirrors <c>ApplicationRepository</c>'s own remarks exactly - the identical bug class caught during this build's manual verification pass.</summary>
internal sealed class ExamAttemptRepository(AdmissionDbContext context) : IExamAttemptRepository
{
    public async Task<ExamAttempt?> GetByIdAsync(ExamAttemptId id, CancellationToken cancellationToken = default)
    {
        var tracked = context.ChangeTracker.Entries<ExamAttempt>().FirstOrDefault(e => e.Entity.Id == id);
        if (tracked is not null)
        {
            await tracked.ReloadAsync(cancellationToken).ConfigureAwait(false);
            return tracked.Entity;
        }

        return await context.ExamAttempts
            .Include(a => a.Answers)
            .Include(a => a.IntegrityFlags)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<ExamAttempt?> GetByApplicantAndTestAsync(Guid applicantId, Guid admissionTestId, CancellationToken cancellationToken = default) =>
        context.ExamAttempts
            .Include(a => a.Answers)
            .Include(a => a.IntegrityFlags)
            .FirstOrDefaultAsync(a => a.ApplicantId == applicantId && a.AdmissionTestId == admissionTestId, cancellationToken);

    public void Add(ExamAttempt attempt) => context.ExamAttempts.Add(attempt);

    public async Task<bool> TryLockAsync(ExamAttemptId id, string source, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var affected = await context.ExamAttempts
            .Where(a => a.Id == id && a.Status == ExamAttemptStatus.InProgress)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(a => a.Status, ExamAttemptStatus.Submitted)
                    .SetProperty(a => a.SubmittedAt, now)
                    .SetProperty(a => a.SubmissionSource, source),
                cancellationToken)
            .ConfigureAwait(false);
        return affected == 1;
    }

    public async Task<IReadOnlyList<ExamAttemptId>> GetExpiredInProgressAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default) =>
        await context.ExamAttempts
            .Where(a => a.Status == ExamAttemptStatus.InProgress && a.ExpiresAt <= now)
            .OrderBy(a => a.ExpiresAt)
            .Take(batchSize)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
