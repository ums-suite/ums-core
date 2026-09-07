using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Integrations;

namespace UMS.Modules.Admission.Infrastructure.Proctoring;

/// <summary>
/// ADR-0018: "provider selection ... is a defensible engineering default to make at Admission's own
/// build time" - no real vendor credentials exist in this environment, so this is a fake, in-process
/// implementation, the same "real abstraction, fake terminal handler" posture every other external
/// integration in this codebase already takes (Finance's <c>IPaymentGateway</c>, Notifications'
/// channel adapters). Identity verification always reports success (a real vendor's photo/ID match
/// is not reproducible here); anomaly reporting is a pure pass-through, recorded verbatim - this
/// build never synthesizes anomalies on its own, since nothing in this environment simulates a real
/// webcam/audio signal, and doing so would risk looking like real detected fraud.
/// </summary>
internal sealed class FakeProctoringProvider : IProctoringProvider
{
    public Task<Result<ProctoringSessionHandle>> StartSessionAsync(StartProctoringSessionCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var providerSessionId = $"fake-session-{command.AttemptId:N}";
        var identityVerified = !string.IsNullOrWhiteSpace(command.ReferencePhotoUrl);
        return Task.FromResult(Result.Success(new ProctoringSessionHandle(providerSessionId, identityVerified, LockdownEnforced: true)));
    }

    public Task<Result<ProctoringAnomalyReport>> ReportAnomalyAsync(ReportProctoringAnomalyCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return Task.FromResult(Result.Success(new ProctoringAnomalyReport(command.AnomalyType, ConfidenceScore: 1.0m, command.OccurredAt)));
    }
}
