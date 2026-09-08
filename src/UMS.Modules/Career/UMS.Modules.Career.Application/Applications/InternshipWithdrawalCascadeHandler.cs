using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Common;
using UMS.Modules.Career.Domain.Internships;
using UMS.Shared.Audit;

namespace UMS.Modules.Career.Application.Applications;

/// <summary>
/// CAR-9: the in-process handler for `InternshipWithdrawn` (design-decisions.md "Internship/Drive
/// Withdrawal Cascade to CareerApplication"). Invoked by the Api layer AFTER
/// `InternshipService.WithdrawAsync` has ALREADY committed - the posting's own state transition is
/// never held hostage by however many `CareerApplication`s it happens to have. Iterates every
/// non-terminal `CareerApplication` and cancels each via the dedicated
/// <c>CancelDueToPostingWithdrawal()</c> method, in its OWN transaction (never one giant transaction
/// spanning the whole fan-out), with a mandatory `NotificationRequest` and an audited
/// `AuditLogEntry` per affected Student (requirement-spec.md §2.6, §5).
/// </summary>
public sealed class InternshipWithdrawalCascadeHandler(
    ICareerApplicationRepository applications,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    ICareerNotificationPublisher notifications,
    IClock clock)
{
    public async Task<int> HandleAsync(Guid internshipId, string correlationId, CancellationToken cancellationToken = default)
    {
        var affected = await applications.ListNonTerminalByInternshipAsync(new InternshipId(internshipId), cancellationToken).ConfigureAwait(false);
        var cancelledCount = 0;

        foreach (var application in affected)
        {
            application.CancelDueToPostingWithdrawal(clock.UtcNow);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var auditRequest = AuditContext.ForSystemJob(
                "internship-withdrawal-cascade",
                correlationId,
                "CareerApplication",
                application.Id.Value.ToString(),
                AuditActions.Update,
                beforeValueJson: null,
                afterValueJson: """{"status":"Cancelled"}""");

            var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
            if (committed.IsFailure)
            {
                continue;
            }

            cancelledCount++;
            await notifications.PublishAsync(
                new CareerNotificationRequest(
                    "CareerApplicationCancelled",
                    application.Id.Value.ToString(),
                    application.StudentId,
                    new Dictionary<string, string> { ["internshipId"] = internshipId.ToString(), ["reason"] = "internship_withdrawn" }),
                cancellationToken).ConfigureAwait(false);
        }

        return cancelledCount;
    }
}
