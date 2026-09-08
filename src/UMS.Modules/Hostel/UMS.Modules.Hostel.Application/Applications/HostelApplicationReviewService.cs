using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Allocations;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.Applications;

/// <summary>
/// HOS-5/HOS-6: <c>GET /applications</c> (Officer ranked/filterable queue) and
/// <c>POST /applications/{id}/review</c> (requirement-spec.md §2 step 5, §6). The
/// <c>Approve</c> decision delegates to <see cref="AllocationService.ApproveAndAllocateAsync"/>,
/// which owns the HostelApplication-then-Bed lock ordering (design-decisions.md) - this class only
/// owns the simpler, single-aggregate <c>Waitlist</c>/<c>Reject</c> transitions.
/// </summary>
public sealed class HostelApplicationReviewService(
    IHostelApplicationRepository applications,
    AllocationService allocationService,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public async Task<Result<HostelApplicationDto>> ReviewAsync(Guid applicationId, ReviewHostelApplicationRequest request, Guid officerUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        if (string.Equals(request.Decision, "Approve", StringComparison.OrdinalIgnoreCase))
        {
            var allocated = await allocationService.ApproveAndAllocateAsync(applicationId, officerUserId, correlationId, cancellationToken).ConfigureAwait(false);
            if (allocated.IsFailure)
            {
                return allocated.Error!;
            }

            var refreshed = await applications.GetByIdAsync(new HostelApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
            return refreshed is null
                ? Error.NotFound("hostel_application.not_found", $"No HostelApplication exists with id '{applicationId}'.")
                : HostelApplicationService.ToDto(refreshed);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var application = await applications.GetByIdForUpdateAsync(new HostelApplicationId(applicationId), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Error.NotFound("hostel_application.not_found", $"No HostelApplication exists with id '{applicationId}'.");
        }

        var statusBefore = application.Status;
        var now = clock.UtcNow;

        var transitioned = string.Equals(request.Decision, "Waitlist", StringComparison.OrdinalIgnoreCase)
            ? application.Waitlist(now)
            : string.Equals(request.Decision, "Reject", StringComparison.OrdinalIgnoreCase)
                ? application.Reject(request.Reason ?? string.Empty, now)
                : Result.Failure(Error.Validation("hostel_application.invalid_decision", $"'{request.Decision}' is not a recognized review decision. Use Approve, Waitlist, or Reject."));

        if (transitioned.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return transitioned.Error!;
        }

        var audit = new AuditContext(officerUserId, ActorIpAddress: null, correlationId)
            .ToRequest("HostelApplication", application.Id.Value.ToString(), AuditActions.Update, $"{{\"status\":\"{statusBefore}\"}}", $"{{\"status\":\"{application.Status}\"}}", request.Reason);

        var committed = await Common.TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : HostelApplicationService.ToDto(application);
    }

    public async Task<IReadOnlyList<HostelApplicationDto>> GetQueueAsync(Guid applicationWindowId, string status, CancellationToken cancellationToken = default) =>
        (await applications.GetByWindowAndStatusAsync(applicationWindowId, ParseStatus(status), cancellationToken).ConfigureAwait(false))
            .OrderBy(a => a.RankPosition ?? int.MaxValue)
            .Select(HostelApplicationService.ToDto)
            .ToList();

    private static HostelApplicationStatus ParseStatus(string status) =>
        Enum.TryParse<HostelApplicationStatus>(status, ignoreCase: true, out var parsed) ? parsed : HostelApplicationStatus.Ranked;
}
