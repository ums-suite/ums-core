using Microsoft.EntityFrameworkCore;
using UMS.Modules.Learning.Application.Abstractions;
using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.PlagiarismChecks;
using UMS.Modules.Learning.Domain.Submissions;

namespace UMS.Modules.Learning.Infrastructure.Persistence.Repositories;

internal sealed class SubmissionRepository(LearningDbContext context) : ISubmissionRepository
{
    public Task<Submission?> GetByIdAsync(SubmissionId id, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <summary>LRN-7: exactly one non-Superseded row exists per (Assignment, Student) at any instant - see SubmissionConfiguration's own remarks on why that is a transactional invariant rather than a partial unique index.</summary>
    public Task<Submission?> GetCountedForStudentAsync(AssignmentId assignmentId, Guid studentId, CancellationToken cancellationToken = default) =>
        Query().FirstOrDefaultAsync(
            s => s.AssignmentId == assignmentId && s.StudentId == studentId && s.Status == SubmissionStatus.Submitted,
            cancellationToken);

    public async Task<IReadOnlyList<Submission>> GetByAssignmentAsync(AssignmentId assignmentId, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(s => s.AssignmentId == assignmentId)
            .OrderBy(s => s.SubmittedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Submission>> GetCountedWithoutCompletedCheckAsync(AssignmentId assignmentId, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(s => s.AssignmentId == assignmentId
                && s.Status == SubmissionStatus.Submitted
                && !s.PlagiarismChecks.Any(c => c.Status == PlagiarismCheckStatus.Completed))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Submission>> GetWithQueuedPlagiarismChecksAsync(int batchSize, CancellationToken cancellationToken = default) =>
        await Query()
            .Where(s => s.PlagiarismChecks.Any(c => c.Status == PlagiarismCheckStatus.Queued))
            .OrderBy(s => s.SubmittedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Submission submission) => context.Submissions.Add(submission);

    private IQueryable<Submission> Query() =>
        context.Submissions.Include(s => s.Files).Include(s => s.PlagiarismChecks);
}
