using Microsoft.Extensions.Logging;
using UMS.Modules.Career.Application.Abstractions;
using UMS.Modules.Career.Application.Common;
using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Shared.Audit;
using UMS.Shared.Student;

namespace UMS.Modules.Career.Application.Applications;

/// <summary>
/// CAR-15: the in-process handler for `CampusRecruitmentDriveCancelled` - the IDENTICAL cascade
/// mechanism as <see cref="InternshipWithdrawalCascadeHandler"/> (design-decisions.md's resolved
/// bullet: "one cascade mechanism, two triggering events, not two bespoke implementations"),
/// additionally releasing any booked `InterviewSlot`'s capacity atomically for a
/// `CareerApplication` that had reached `InterviewScheduled` (edge-cases.md's resolved bullet).
///
/// <para>
/// `NotificationRequest.RecipientId` must be the Identity <c>UserId</c> - resolved fresh per
/// affected Student via `IStudentStatusChecker.GetByStudentIdAsync`, mirroring
/// <see cref="InternshipWithdrawalCascadeHandler"/>'s own resolution exactly.
/// </para>
/// </summary>
public sealed class DriveCancellationCascadeHandler(
    ICareerApplicationRepository applications,
    IInterviewSlotRepository slots,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    ICareerNotificationPublisher notifications,
    IStudentStatusChecker studentStatusChecker,
    IClock clock,
    ILogger<DriveCancellationCascadeHandler> logger)
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

            var standing = await studentStatusChecker.GetByStudentIdAsync(application.StudentId, cancellationToken).ConfigureAwait(false);
            if (standing?.IdentityUserId is { } recipientId)
            {
                await notifications.PublishAsync(
                    new CareerNotificationRequest(
                        "CareerApplicationCancelled",
                        application.Id.Value.ToString(),
                        recipientId,
                        new Dictionary<string, string> { ["driveId"] = driveId.ToString(), ["reason"] = "drive_cancelled" }),
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                logger.LogWarning("Drive cancellation cascade: CareerApplication {CareerApplicationId} has no resolvable Identity recipient for Student {StudentId} - skipping the mandatory cancellation notice.", application.Id.Value, application.StudentId);
            }
        }

        return cancelledCount;
    }
}
