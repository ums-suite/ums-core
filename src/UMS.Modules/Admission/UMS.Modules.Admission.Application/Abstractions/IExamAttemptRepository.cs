using UMS.Modules.Admission.Domain.ExamAttempts;

namespace UMS.Modules.Admission.Application.Abstractions;

public interface IExamAttemptRepository
{
    public Task<ExamAttempt?> GetByIdAsync(ExamAttemptId id, CancellationToken cancellationToken = default);

    public Task<ExamAttempt?> GetByApplicantAndTestAsync(Guid applicantId, Guid admissionTestId, CancellationToken cancellationToken = default);

    public void Add(ExamAttempt attempt);

    /// <summary>design-decisions.md "ExamAttempt Locking &amp; Single-Submission Mechanism": one converging conditional <c>UPDATE ... WHERE status = 'InProgress'</c> for manual submit, retried submit, AND the timeout sweep alike.</summary>
    public Task<bool> TryLockAsync(ExamAttemptId id, string source, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>ADM-13's timeout sweep: every attempt still `InProgress` past its own <c>ExpiresAt</c>.</summary>
    public Task<IReadOnlyList<ExamAttemptId>> GetExpiredInProgressAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken = default);
}
