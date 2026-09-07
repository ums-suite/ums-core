using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Submissions;

namespace UMS.Modules.Learning.Application.Abstractions;

public interface ISubmissionRepository
{
    public Task<Submission?> GetByIdAsync(SubmissionId id, CancellationToken cancellationToken = default);

    /// <summary>LRN-7/LRN-10: the one non-superseded attempt that counts for this <c>(Assignment, Student)</c> pair, or <see langword="null"/> if the Student has not submitted yet.</summary>
    public Task<Submission?> GetCountedForStudentAsync(AssignmentId assignmentId, Guid studentId, CancellationToken cancellationToken = default);

    /// <summary>LRN-10: the Instructor's grading queue - every attempt for this Assignment, superseded ones included, ordered oldest-first so a caller can reconstruct each Student's chain.</summary>
    public Task<IReadOnlyList<Submission>> GetByAssignmentAsync(AssignmentId assignmentId, CancellationToken cancellationToken = default);

    /// <summary>LRN-9's <c>hardCloseAt</c> sweep: the counted attempts for this Assignment that have no Completed PlagiarismCheck yet.</summary>
    public Task<IReadOnlyList<Submission>> GetCountedWithoutCompletedCheckAsync(AssignmentId assignmentId, CancellationToken cancellationToken = default);

    /// <summary>LRN-8: the checks the dispatch worker has to actually drive against the provider.</summary>
    public Task<IReadOnlyList<Submission>> GetWithQueuedPlagiarismChecksAsync(int batchSize, CancellationToken cancellationToken = default);

    public void Add(Submission submission);
}
