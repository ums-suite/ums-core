using UMS.Modules.Admission.Domain.Common;

namespace UMS.Modules.Admission.Application.Abstractions;

/// <summary>
/// Lets an application service enqueue a domain event directly for a write that bypasses an
/// aggregate's own change-tracked <c>Raise</c> - Admission's own state-guarded conditional
/// <c>ExecuteUpdateAsync</c> transitions (<c>Application.Lock</c>/<c>ExamAttempt.Submit</c>/
/// <c>AdmissionResult</c>'s transitions) all go through the repository directly, so the calling
/// service enqueues the corresponding event here immediately after a successful conditional write.
/// Mirrors every other module's own <c>IDomainEventRecorder</c> exactly.
/// </summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
