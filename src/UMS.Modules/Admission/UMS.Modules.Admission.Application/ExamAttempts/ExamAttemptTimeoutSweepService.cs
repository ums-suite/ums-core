using UMS.Modules.Admission.Application.Abstractions;

namespace UMS.Modules.Admission.Application.ExamAttempts;

/// <summary>ADM-13/edge-cases.md "Time expires mid-question": the server-side timeout sweep - auto-submits any attempt still `InProgress` past its own <c>ExpiresAt</c>, converging on the identical conditional write a manual submit uses (design-decisions.md).</summary>
public sealed class ExamAttemptTimeoutSweepService(IExamAttemptRepository attempts, ExamAttemptService examAttemptService, IClock clock)
{
    public async Task<int> SweepAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var expired = await attempts.GetExpiredInProgressAsync(clock.UtcNow, batchSize, cancellationToken).ConfigureAwait(false);
        var processed = 0;

        foreach (var attemptId in expired)
        {
            await examAttemptService.TryLockAndEvaluateAsync(attemptId.Value, "timeout-sweep", $"system:timeout-sweep:{attemptId.Value}", cancellationToken).ConfigureAwait(false);
            processed++;
        }

        return processed;
    }
}
