using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Common;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Shared.Audit;

namespace UMS.Modules.Career.Application.Applications;

/// <summary>
/// CAR-15: the in-process handler for `CampusRecruitmentDriveCancelled` - the IDENTICAL cascade
/// mechanism as <see cref="InternshipWithdrawalCascadeHandler"/> (design-decisions.md's resolved
/// bullet: "one cascade mechanism, two triggering events, not two bespoke implementations"),
/// additionally releasing any booked `InterviewSlot`'s capacity atomically for a
/// `CareerApplication` that had reached `InterviewScheduled` (edge-cases.md's resolved bullet).
/// </summary>
public sealed class DriveCancellationCascadeHandler(
    ICareerApplicationRepository applications,
    IInterviewSlotRepository slots,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    ICareerNotificationPublisher notifications,
    IClock clock)
{
    public async Task<int> HandleAsync(Guid driveId, string correlationId, CancellationToken cancellationToken = default)
    {
        var affected = await applications.ListNonTerminalByDriveAsync(new CampusRecruitmentDriveId(driveId), cancellationToken).ConfigureAwait(false);
        var cancelledCount = 0;

        foreach (var application in affected)
        {
            var bookedSlotId = application.InterviewSlotId;
            application.CancelDueToPostingWithdrawal(clock.UtcNow);

            await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            if (bookedSlotId is { } slotId)
            {
                await slots.ReleaseSlotAsync(new InterviewSlotId(slotId), cancellationToken).ConfigureAwait(false);
            }

            var auditRequest = AuditContext.ForSystemJob(
                "drive-cancellation-cascade",
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
                    new Dictionary<string, string> { ["driveId"] = driveId.ToString(), ["reason"] = "drive_cancelled" }),
                cancellationToken).ConfigureAwait(false);
        }

        return cancelledCount;
    }
}
